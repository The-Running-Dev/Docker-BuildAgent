#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

using Release;
using Surface;

// The release entry point (design/10-design.md, control flow 2). The Release and Release-from-Tag
// workflows run it; it publishes nothing outside CI (I14).
//
// Usage: Publish (--tag v<version> | --core <MAJOR.MINOR.PATCH> [--label <label>])
//                [--repository owner/repo] [--image registry/name] [--root dir] [--work dir]
// Environment: GITHUB_TOKEN, NUGET_API_KEY, PSGALLERY_API_KEY; the registry login is the workflow's.
// Exit 0: published, or an already published release completed. Exit 1: the release was refused or
// failed. Exit 2: the request was malformed.
const string DefaultImage = "ghcr.io/the-running-dev/build-agent";

Dictionary<string, string> options;
ReleaseVersion version;
string repository;
string owner;
string repo;
string githubToken;
string nugetKey;
string galleryKey;
try
{
    options = ParseOptions(args);
    version = ReleaseVersionResolver.Resolve(Option("tag"), Option("core"), Option("label"));
    repository = Option("repository") ?? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
        ?? throw new ArgumentException("Give --repository owner/repo, or run where GITHUB_REPOSITORY is set.");
    var parts = repository.Split('/');
    if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
    {
        throw new ArgumentException($"Repository '{repository}' must have the form owner/repo.");
    }

    (owner, repo) = (parts[0], parts[1]);
    githubToken = RequiredEnvironment("GITHUB_TOKEN");
    nugetKey = RequiredEnvironment("NUGET_API_KEY");
    galleryKey = RequiredEnvironment("PSGALLERY_API_KEY");
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"::error::{ex.Message}");
    return 2;
}
catch (ReleaseException ex)
{
    Console.Error.WriteLine($"::error::{ex.Code}: {ex.Message}");
    return 1;
}

var root = Path.GetFullPath(Option("root") ?? FindRoot(Directory.GetCurrentDirectory()));
var work = Path.GetFullPath(Option("work") ?? Path.Combine(Path.GetTempPath(), "build-agent-release"));
var image = Option("image") ?? DefaultImage;
var runner = new ProcessCommandRunner();
using var http = new HttpClient();

try
{
    // I14: only CI publishes, so nothing is read or built outside it.
    if (!new EnvironmentCiPublishingContext().IsCiPublishing)
    {
        throw new ReleaseException(ReleaseErrorCode.NotPublishedByCi, null, "This run is not the CI publishing context; refusing to publish.");
    }

    var commit = (await runner.RunCheckedAsync("git", new[] { "rev-parse", "HEAD" })).Output.Trim();
    Console.WriteLine($"Releasing {version.ToTagString()} from {commit}.");

    // Steps 1-5 write nothing: the gate, the notes and the builds.
    var gate = await SurfaceGate.EvaluateAsync(root, version.ToPackageString(), new GitHubReleaseManifestSource(owner, repo, githubToken));
    var manifest = ReleaseSurfaceCheck.Accept(gate, version);
    Console.WriteLine(gate.Success ? "Surface gate passed." : $"Surface gate: no baseline; {version.ToPackageString()} is the first gated release.");

    var notes = ReleaseNotesComposer.Compose(
        ReleaseNotesComposer.ReadAuthored(root, version, repository, commit),
        await CommitSubjectsAsync(runner, version),
        gate.Comparison);

    var imagePublisher = await ImageVersionedTagPublisher.BuildAsync(image, version, root, work, runner);
    var toolPackage = await GlobalToolPackager.PackAsync(
        Path.Combine(root, "forge", "Tool", "Tool.csproj"), version, Path.Combine(work, "tool"), runner);
    var modulePackage = PowerShellModulePackager.Pack(
        Path.Combine(root, "scripts", "powershell-module"), version, Path.Combine(work, "module"));

    var pipeline = new ReleasePipeline(
        new GitHubReleaseClaimStore(owner, repo, githubToken),
        new DockerRegistryImageTagChecker(string.Empty, image),
        new GitCliTagChecker(),
        new GitHubHighestPublishedVersionSource(owner, repo, githubToken),
        new EnvironmentCiPublishingContext(),
        new IReleaseSinkPublisher[]
        {
            imagePublisher,
            new PackageSinkPublisher(ReleaseSink.GlobalTool, toolPackage, GlobalToolPackager.PackageId, version, PackageFeed.NuGetOrg, nugetKey, runner, http),
            new PackageSinkPublisher(ReleaseSink.PowerShellModule, modulePackage, PowerShellModulePackager.ModuleName, version, PackageFeed.PowerShellGallery, galleryKey, runner, http),
            new ImageLatestTagPublisher(image, runner),
        });

    var claim = await pipeline.PublishAsync(version, commit, notes, manifest);
    Console.WriteLine($"Released {claim.Version.ToTagString()} ({claim.State}).");
    return 0;
}
catch (ReleaseException ex)
{
    var sink = ex.Sink is { } named ? $" [{named}]" : string.Empty;
    Console.Error.WriteLine($"::error::{ex.Code}{sink}: {ex.Message}");
    return 1;
}
catch (SurfaceException ex)
{
    Console.Error.WriteLine($"::error::{ex.Code}: {ex.Message}");
    return 1;
}
catch (Exception ex) when (ex is InvalidOperationException or IOException or HttpRequestException)
{
    Console.Error.WriteLine($"::error::{ex.Message}");
    return 1;
}

string? Option(string name) => options.TryGetValue(name, out var value) ? value : null;

static Dictionary<string, string> ParseOptions(string[] arguments)
{
    var known = new HashSet<string> { "tag", "core", "label", "repository", "image", "root", "work" };
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < arguments.Length; i++)
    {
        var name = arguments[i].StartsWith("--", StringComparison.Ordinal) ? arguments[i][2..] : null;
        if (name == null || !known.Contains(name))
        {
            throw new ArgumentException($"Unknown argument '{arguments[i]}'.");
        }

        if (i + 1 >= arguments.Length)
        {
            throw new ArgumentException($"--{name} needs a value.");
        }

        if (!parsed.TryAdd(name, arguments[++i]))
        {
            throw new ArgumentException($"--{name} is given twice.");
        }
    }

    return parsed;
}

static string RequiredEnvironment(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new ArgumentException($"{name} is not set.");

// The subjects of the commits since the previous release tag, oldest first, for notes that have no
// authored page.
static async Task<IReadOnlyList<string>> CommitSubjectsAsync(ICommandRunner runner, ReleaseVersion version)
{
    var previous = await runner.RunAsync("git", new[] { "describe", "--tags", "--abbrev=0", "--match", "v*", "--exclude", version.ToTagString(), "HEAD" });
    var range = previous.Succeeded ? $"{previous.Output.Trim()}..HEAD" : "HEAD";
    var log = await runner.RunCheckedAsync("git", new[] { "log", "--no-merges", "--reverse", "--format=%s", range });
    return log.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string FindRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "forge", "Forge.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return start;
}
