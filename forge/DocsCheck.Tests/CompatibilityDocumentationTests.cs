using DocsCheck.Tests.TestSupport;
using Surface;
using Xunit;

namespace DocsCheck.Tests;

/// <summary>S11: the compatibility page and the 2.0.0 migration guide state what the contract requires them to.</summary>
public sealed class CompatibilityDocumentationTests
{
    private const string CompatibilityPath = "documentation/docs/compatibility.md";
    private const string MigrationPath = "documentation/docs/migration.md";
    private const string ReleaseNotesPath = "documentation/docs/release-notes/2.0.0.md";

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRootLocator.Find(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Normalized(string relativePath) =>
        string.Join(' ', Read(relativePath).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void AssertContains(string text, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            Assert.True(text.Contains(fragment, StringComparison.OrdinalIgnoreCase), $"Expected the text to contain '{fragment}'.");
        }
    }

    [Fact]
    public void S11_1_OnePageStatesEveryRequiredTopic_AndNamesTheContractCanonical()
    {
        var page = Normalized(CompatibilityPath);

        AssertContains(page,
            "## Protected surfaces", "## Versioning", "## Which versions receive fixes", "## Deprecation policy",
            "## Supported hosts and CI", "## Docker socket");
        AssertContains(page, "Canonical contract:", "design/20-contract.md");
        AssertContains(page, "image", "build command", "global tool", "project configuration", "Docker-template discovery", "PowerShell module");
    }

    [Fact]
    public void S11_2_StatesLatestIsMovable_AndShowsSelectingAVersionedImage()
    {
        var page = Normalized(CompatibilityPath);

        AssertContains(page, "`latest` is movable", "ghcr.io/the-running-dev/build-agent:2.0.0");
    }

    [Fact]
    public void S11_3_StatesImageContentsAndBundledToolVersionsAreProtectedOnlyWhereNamed()
    {
        var page = Normalized(CompatibilityPath);

        AssertContains(page, "image contents are protected only where the contract names them", "bundled tool versions are not protected unless");
    }

    [Fact]
    public void S11_4_DeprecationPolicyStatesMinorRelease_Warning_AndNextMajorRemoval()
    {
        var page = Normalized(CompatibilityPath);

        AssertContains(page, "deprecated in a minor release", "warns when used", "no earlier than the next major");
    }

    [Fact]
    public void S11_5_MigrationGuideCoversEveryBreakingChangeInTheReleaseNotes_AndTheMoveToAPinnedVersion()
    {
        var guide = Normalized(MigrationPath);

        AssertContains(guide, "default image is version-pinned", "Launcher failures carry stable codes",
            "Map-derived environment no longer overwrites", "## Moving from `latest` to a pinned version");

        // Every breaking-change bullet in the release notes has a migration section: the notes name three.
        var notes = Read(ReleaseNotesPath);
        var breaking = notes.Split("## Deprecations")[0];
        Assert.Equal(3, breaking.Split('\n').Count(l => l.StartsWith("- **", StringComparison.Ordinal)));
    }

    [Fact]
    public void S11_7_StatesUpdateBehaviourIsVerifiedOnLinux_AndWindowsLiveDaemonCoverageIsAGap()
    {
        var page = Normalized(CompatibilityPath);

        AssertContains(page, "verified on Linux", "Windows live-daemon", "gap");
        Assert.DoesNotContain("verified on Windows", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void S11_8_TheCompatibilityPagePassesTheDocsCheck()
    {
        var root = RepoRootLocator.Find();
        Assert.True(File.Exists(Path.Combine(root, "documentation", "docs", "compatibility.md")));
        var report = DocsChecker.Check(root, SurfaceDeriver.Derive(root, "0.0.0"));

        Assert.DoesNotContain(report.Findings, f => f.Error.Document == CompatibilityPath);
        Assert.DoesNotContain(report.Recorded, r => r.Finding.Error.Document == CompatibilityPath);
    }
}
