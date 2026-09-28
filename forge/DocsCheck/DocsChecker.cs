#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Surface;

namespace DocsCheck;

/// <summary>
/// The docs check (I45, I46): extracts the names a document claims from code spans, fenced blocks,
/// links and PowerShell help, and fails the ones the surface manifest and the tree do not have.
/// Behavioural statements are not checkable and are never judged (see <see cref="NotCovered"/>).
/// A surface that is deprecated but still in the manifest is present, so it never fails.
/// </summary>
public static class DocsChecker
{
    public static readonly IReadOnlyList<string> NotCovered = new[]
    {
        "Behavioural statements (what a command does, defaults, ordering, exit codes): not verified, and no claim is made about them.",
        "Names on the image, global tool and project-configuration surfaces (entrypoint, mounts, environment inputs, tool commands and parameters, configuration keys outside a build command): the manifest does not derive them yet.",
        "Names written in plain prose: only code spans, fenced blocks, links and module command names are extracted.",
        "Multi-line -args hashtables and PowerShell splatting.",
        "Site routes (extensionless links) and external URLs.",
        "Paths outside the repository's top-level entries, and everything under docs-template/, which is pinned and not read.",
    };

    private const string ModulePlaceholderKind = "PowerShell module";

    private static readonly string[] ProtectedSurfaces =
    {
        "image",
        "build command",
        "global tool",
        "project configuration",
        "Docker-template discovery",
        "PowerShell module",
    };

    private static readonly HashSet<string> IgnoredTopLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", "outputs", "artifacts", ".build", "test-results", "docs-template",
    };

    private static readonly HashSet<string> NotBuildCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet", "docker", "buildx", "ng", "npm", "pnpm", "yarn", "run", "npx", "make", "msbuild", "cargo", "go", "gradle", "mvn", "compose",
    };

    // Flags owned by the host tool that carries the build invocation, not by the product's surface.
    private static readonly HashSet<string> BuildScriptFlags = new(StringComparer.OrdinalIgnoreCase) { "help", "type", "root", "target", "skip", "plan" };
    private static readonly HashSet<string> NukeFlags = new(StringComparer.OrdinalIgnoreCase) { "help", "type", "root", "target", "skip", "plan", "no-logo", "profile", "host" };
    private static readonly HashSet<string> DotnetRunFlags = new(StringComparer.OrdinalIgnoreCase) { "help", "project", "configuration", "no-build", "no-restore" };

    private static readonly HashSet<string> PowerShellCommonParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "verbose", "debug", "erroraction", "warningaction", "informationaction", "errorvariable", "warningvariable",
        "outvariable", "outbuffer", "pipelinevariable", "whatif", "confirm",
    };

    private static readonly HashSet<string> LinkedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".mdx", ".ps1", ".psm1", ".psd1", ".json", ".yml", ".yaml", ".sln", ".csproj", ".cs", ".ts", ".css", ".docx", ".txt",
    };

    private static readonly Regex ModuleCommand = new(
        @"\b(?:Invoke|Set|Get|New|Remove|Import|Update|Start|Test|Add|Clear|Enable|Disable)-Build[A-Za-z]*", RegexOptions.Compiled);

    private static readonly Regex Link = new(@"\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled);
    private static readonly Regex DiscoveryLocation = new(@"^[A-Z]\w*(?:TemplatesDirectory|DockerfileByAppType)$", RegexOptions.Compiled);
    private static readonly Regex HashtableKey = new(@"(?:^|[;\s{])([A-Za-z_]\w*)\s*=", RegexOptions.Compiled);
    private static readonly Regex Hashtable = new(@"@\{([^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex BuildTypeToken = new(@"^[a-z][a-z0-9-]*$", RegexOptions.Compiled);
    private static readonly Regex LineSuffix = new(@":\d+(?:-\d+)?$", RegexOptions.Compiled);

    private static readonly Regex CanonicalContract = new(
        @"^[\s>*\-]*(?:\*\*)?Canonical contract(?:\s*\(([^)]+)\))?(?:\*\*)?\s*:\s*(?:\*\*)?\s*(\S.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CanonicalFor = new(
        @"^[\s>*\-]*(?:\*\*)?Canonical for(?:\*\*)?\s*:\s*(?:\*\*)?\s*(\S.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] ContractFiles = { "PSModule.requirements.md", "PSModule.specs.md" };

    public static DocsCheckReport Check(string root, SurfaceManifest manifest)
    {
        var index = new ManifestIndex(manifest);
        var topLevel = Directory.EnumerateFileSystemEntries(root)
            .Select(Path.GetFileName)
            .Where(n => n != null && !IgnoredTopLevel.Contains(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

        var findings = new List<DocsCheckFinding>();
        var documents = DocumentLoader.Discover(root);
        var declarations = new List<(string Document, int Line, string Surface)>();

        foreach (var path in documents)
        {
            var document = DocumentLoader.Load(root, path);
            var covered = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            CheckClaims(root, document, index, topLevel, findings, covered);
            var pointed = ReadCanonicalMarkers(document, findings, declarations);

            foreach (var (surface, line) in covered.OrderBy(c => c.Value))
            {
                if (pointed.Contains(surface) || declarations.Any(d => d.Document == path && d.Surface.Equals(surface, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                findings.Add(new DocsCheckFinding(
                    new DocsCheckError(DocsCheckErrorCode.CanonicalSourceMissing, path, surface,
                        $"'{path}' covers the {surface} surface but names no canonical contract for it (add a 'Canonical contract:' line)."),
                    line));
            }
        }

        foreach (var contract in ContractFiles)
        {
            if (!documents.Contains(contract) && File.Exists(Path.Combine(root, contract)))
            {
                ReadCanonicalMarkers(DocumentLoader.LoadMarkdownOnly(root, contract), findings, declarations);
            }
        }

        ReportConflicts(declarations, findings);

        var ordered = findings
            .GroupBy(f => (f.Error.Code, f.Error.Document, f.Error.Name, f.Line))
            .Select(g => g.First())
            .OrderBy(f => f.Error.Document, StringComparer.Ordinal)
            .ThenBy(f => f.Line)
            .ThenBy(f => f.Error.Code)
            .ToList();

        var (failing, recorded, stale) = ApplyRecords(root, ordered);
        return new DocsCheckReport(documents, failing, NotCovered, recorded, stale);
    }

    /// <summary>
    /// design/docs-check-recorded.txt: tab-separated Code, Document, Name, Reason; '#' starts a comment.
    /// A finding is set aside only when a line names it exactly and gives a reason.
    /// </summary>
    private static (List<DocsCheckFinding> Failing, List<RecordedFinding> Recorded, List<string> Stale) ApplyRecords(
        string root, List<DocsCheckFinding> findings)
    {
        var path = Path.Combine(root, "design", "docs-check-recorded.txt");
        var records = new Dictionary<(string, string, string), string>();
        if (File.Exists(path))
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split('\t');
                if (parts.Length >= 4 && parts[3].Trim().Length > 0)
                {
                    records[(parts[0].Trim(), parts[1].Trim(), parts[2].Trim())] = parts[3].Trim();
                }
            }
        }

        var failing = new List<DocsCheckFinding>();
        var recorded = new List<RecordedFinding>();
        var used = new HashSet<(string, string, string)>();
        foreach (var finding in findings)
        {
            var key = (finding.Error.Code.ToString(), finding.Error.Document, finding.Error.Name);
            if (records.TryGetValue(key, out var reason))
            {
                recorded.Add(new RecordedFinding(finding, reason));
                used.Add(key);
            }
            else
            {
                failing.Add(finding);
            }
        }

        var stale = records.Keys.Where(k => !used.Contains(k)).Select(k => $"{k.Item1} {k.Item2} {k.Item3}").OrderBy(k => k, StringComparer.Ordinal).ToList();
        return (failing, recorded, stale);
    }

    private static void CheckClaims(
        string root, Document document, ManifestIndex index, HashSet<string> topLevel,
        List<DocsCheckFinding> findings, Dictionary<string, int> covered)
    {
        void Unknown(int line, string name, string message) =>
            findings.Add(new DocsCheckFinding(new DocsCheckError(DocsCheckErrorCode.UnknownName, document.Path, name, message), line));

        void Cover(string surface, int line)
        {
            if (!covered.ContainsKey(surface))
            {
                covered[surface] = line;
            }
        }

        foreach (var segment in document.Segments)
        {
            foreach (Match match in ModuleCommand.Matches(segment.Text))
            {
                Cover(ModulePlaceholderKind, segment.Line);
                if (!index.ModuleCommands.Contains(match.Value))
                {
                    Unknown(segment.Line, match.Value, $"'{match.Value}' is not an exported command of the PowerShell module.");
                }
            }

            if (segment.Kind == SegmentKind.Prose)
            {
                CheckLinks(root, document, segment, Unknown);
                continue;
            }

            var tokens = Tokenize(segment.Text);

            CheckPaths(root, tokens, topLevel, segment, Unknown);

            foreach (var token in tokens)
            {
                if (DiscoveryLocation.IsMatch(token))
                {
                    Cover("Docker-template discovery", segment.Line);
                    if (!index.TemplateLocations.Contains(token))
                    {
                        Unknown(segment.Line, token, $"'{token}' is not a Docker template discovery location.");
                    }
                }
            }

            // Lines inside a block that do not start like a command (commit messages, error text, comments in
            // other languages) are prose, not invocations.
            if (segment.Kind != SegmentKind.CodeStatement || StartsLikeCommand(tokens))
            {
                CheckBuildInvocation(tokens, segment, index, Unknown, Cover);
            }

            CheckModuleInvocation(tokens, segment, index, Unknown);
        }
    }

    private static readonly HashSet<string> CommandStarters = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", "build.ps1", "docker", "nuke", "dotnet", "pwsh", "powershell", "bash", "sh", "run:", "ng", "sudo", "podman",
    };

    private static bool StartsLikeCommand(List<string> tokens)
    {
        var first = tokens.FirstOrDefault(t => t != "&" && t != "$" && t != ">" && t != ".");
        return first != null && CommandStarters.Contains(BaseName(first));
    }

    private static void CheckBuildInvocation(
        List<string> tokens, Segment segment, ManifestIndex index,
        Action<int, string, string> unknown, Action<string, int> cover)
    {
        var flagStart = -1;
        var hostFlags = BuildScriptFlags;

        for (var i = 0; i < tokens.Count && flagStart < 0; i++)
        {
            var name = BaseName(tokens[i]);
            var isBuild = name == "build" || name == "build.ps1";
            if (isBuild && (i == 0 || !NotBuildCommands.Contains(BaseName(tokens[i - 1]))))
            {
                var typeToken = i + 1 < tokens.Count ? tokens[i + 1] : null;
                var hasType = typeToken != null && BuildTypeToken.IsMatch(typeToken);

                // A bare `build <word>` in a span is ambiguous with prose ("the build process"); only a
                // statement with more to it, or a block, is a command.
                var ambiguous = i == 0 && segment.Kind == SegmentKind.CodeSpan && tokens.Count < 3;
                if (hasType && !ambiguous)
                {
                    cover("build command", segment.Line);
                    if (!index.BuildTypes.Contains(typeToken!))
                    {
                        unknown(segment.Line, typeToken!, $"'{typeToken}' is not an accepted build type.");
                    }
                }

                if (!(ambiguous && hasType))
                {
                    flagStart = hasType ? i + 2 : i + 1;
                }
            }
            else if (i == 0 && name.Equals("nuke", StringComparison.OrdinalIgnoreCase))
            {
                flagStart = 1;
                hostFlags = NukeFlags;
                for (var j = 1; j + 1 < tokens.Count; j++)
                {
                    if (tokens[j] == "--type")
                    {
                        cover("build command", segment.Line);
                        if (!index.BuildTypes.Contains(tokens[j + 1]))
                        {
                            unknown(segment.Line, tokens[j + 1], $"'{tokens[j + 1]}' is not an accepted build type.");
                        }
                    }
                }
            }
            else if (name.Equals("run", StringComparison.OrdinalIgnoreCase) && tokens.Skip(i).Any(t => t.Replace('\\', '/').StartsWith("forge/", StringComparison.Ordinal))
                     && i > 0 && BaseName(tokens[i - 1]).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                var separator = tokens.IndexOf("--", i);
                flagStart = separator >= 0 ? separator + 1 : tokens.Count;
                hostFlags = DotnetRunFlags;
            }
        }

        if (flagStart < 0)
        {
            return;
        }

        for (var i = flagStart; i < tokens.Count; i++)
        {
            var token = tokens[i];
            string? flag = null;

            if (token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2 && char.IsLetter(token[2]))
            {
                flag = token.Substring(2).Split('=')[0];
            }
            else if (token.StartsWith("-", StringComparison.Ordinal) && token.Length > 2 && char.IsLetter(token[1]))
            {
                flag = token.Substring(1).Split('=', ':')[0];
                if (PowerShellCommonParameters.Contains(flag))
                {
                    continue;
                }
            }

            if (flag == null || hostFlags.Contains(flag))
            {
                continue;
            }

            cover("build command", segment.Line);
            if (!index.BuildParameters.Contains(flag.Replace("-", string.Empty)))
            {
                unknown(segment.Line, flag, $"'{flag}' is not a build parameter.");
            }
        }
    }

    private static void CheckModuleInvocation(List<string> tokens, Segment segment, ManifestIndex index, Action<int, string, string> unknown)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!ModuleCommand.IsMatch(tokens[i]) || !index.ModuleCommandNames.TryGetValue(tokens[i], out var command))
            {
                continue;
            }

            for (var j = i + 1; j < tokens.Count && !ModuleCommand.IsMatch(tokens[j]); j++)
            {
                var token = tokens[j];
                if (!token.StartsWith("-", StringComparison.Ordinal) || token.Length < 2 || !char.IsLetter(token[1]))
                {
                    continue;
                }

                var parameter = token.Substring(1).Split(':')[0];
                if (PowerShellCommonParameters.Contains(parameter))
                {
                    continue;
                }

                if (!index.ModuleParameters.Contains($"{command}.{parameter}"))
                {
                    unknown(segment.Line, $"{command}.{parameter}", $"'{parameter}' is not a parameter of '{command}'.");
                }
            }

            if (command.Equals("Invoke-Build", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Match table in Hashtable.Matches(segment.Text))
                {
                    foreach (Match key in HashtableKey.Matches(table.Groups[1].Value))
                    {
                        var name = key.Groups[1].Value;
                        if (!index.BuildParameters.Contains(name))
                        {
                            unknown(segment.Line, name, $"'{name}' is not a build parameter.");
                        }
                    }
                }
            }
        }
    }

    private static void CheckPaths(
        string root, List<string> tokens, HashSet<string> topLevel, Segment segment, Action<int, string, string> unknown)
    {
        foreach (var token in tokens)
        {
            var candidate = NormalisePath(token);
            if (candidate == null)
            {
                continue;
            }

            var first = candidate.Split('/')[0];
            if (!topLevel.Contains(first))
            {
                continue;
            }

            var full = Path.Combine(root, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full) && !Directory.Exists(full))
            {
                unknown(segment.Line, token.Trim('`', '"', '\'', ',', ';', '(', ')').TrimEnd('.'), $"'{candidate}' does not exist in the repository.");
            }
        }
    }

    private static string? NormalisePath(string token)
    {
        var text = token.Trim('`', '"', '\'', ',', ';', '(', ')');
        if (text.Contains("://") || text.IndexOfAny(new[] { '*', '<', '>', '{', '}', '$', '%', '|', '@', '[', ']' }) >= 0 || text.Contains(".."))
        {
            return null;
        }

        text = text.Replace('\\', '/');
        while (text.StartsWith("./", StringComparison.Ordinal))
        {
            text = text.Substring(2);
        }

        text = LineSuffix.Replace(text, string.Empty).TrimEnd('/', '.', ',', ':');
        var hash = text.IndexOf('#');
        if (hash >= 0)
        {
            text = text.Substring(0, hash);
        }

        return text.Contains('/') ? text : null;
    }

    private static void CheckLinks(string root, Document document, Segment segment, Action<int, string, string> unknown)
    {
        foreach (Match match in Link.Matches(segment.Text))
        {
            var target = match.Groups[1].Value;
            if (target.Contains("://") || target.StartsWith("#", StringComparison.Ordinal) || target.StartsWith("/", StringComparison.Ordinal)
                || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var withoutAnchor = target.Split('#', '?')[0];
            if (withoutAnchor.Length == 0 || !LinkedFileExtensions.Contains(Path.GetExtension(withoutAnchor)))
            {
                continue;
            }

            var documentDirectory = Path.GetDirectoryName(Path.Combine(root, document.Path.Replace('/', Path.DirectorySeparatorChar)))!;
            var full = Path.GetFullPath(Path.Combine(documentDirectory, withoutAnchor.Replace('/', Path.DirectorySeparatorChar)));
            var relative = DocumentLoader.Relative(root, full);

            if (relative.StartsWith("docs-template/", StringComparison.Ordinal) || relative == "docs-template")
            {
                continue;
            }

            if (!File.Exists(full) && !Directory.Exists(full))
            {
                unknown(segment.Line, target, $"The link target '{target}' does not exist.");
            }
        }
    }

    private static HashSet<string> ReadCanonicalMarkers(
        Document document, List<DocsCheckFinding> findings, List<(string Document, int Line, string Surface)> declarations)
    {
        var pointed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in document.Segments.Where(s => s.Kind == SegmentKind.Prose))
        {
            var pointer = CanonicalContract.Match(segment.Text);
            if (pointer.Success)
            {
                if (pointer.Groups[1].Success)
                {
                    foreach (var surface in Surfaces(pointer.Groups[1].Value))
                    {
                        pointed.Add(surface);
                    }
                }
                else
                {
                    pointed.UnionWith(ProtectedSurfaces);
                }

                continue;
            }

            var declaration = CanonicalFor.Match(segment.Text);
            if (!declaration.Success)
            {
                continue;
            }

            foreach (var name in declaration.Groups[1].Value.Split(',').Select(n => n.Trim().TrimEnd('.')).Where(n => n.Length > 0))
            {
                var surface = ProtectedSurfaces.FirstOrDefault(s => s.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (surface == null)
                {
                    findings.Add(new DocsCheckFinding(
                        new DocsCheckError(DocsCheckErrorCode.UnknownName, document.Path, name, $"'{name}' is not a protected surface."),
                        segment.Line));
                    continue;
                }

                declarations.Add((document.Path, segment.Line, surface));
            }
        }

        return pointed;
    }

    private static IEnumerable<string> Surfaces(string list) =>
        list.Split(',')
            .Select(n => n.Trim())
            .Select(n => ProtectedSurfaces.FirstOrDefault(s => s.Equals(n, StringComparison.OrdinalIgnoreCase)))
            .Where(s => s != null)
            .Select(s => s!);

    private static void ReportConflicts(List<(string Document, int Line, string Surface)> declarations, List<DocsCheckFinding> findings)
    {
        foreach (var group in declarations.GroupBy(d => d.Surface))
        {
            var claimants = group.Select(d => d.Document).Distinct(StringComparer.Ordinal).ToList();
            if (claimants.Count < 2)
            {
                continue;
            }

            foreach (var claim in group.GroupBy(d => d.Document).Select(g => g.First()))
            {
                var others = string.Join(", ", claimants.Where(c => c != claim.Document).Select(c => $"'{c}'"));
                findings.Add(new DocsCheckFinding(
                    new DocsCheckError(DocsCheckErrorCode.CanonicalSourceConflict, claim.Document, group.Key,
                        $"'{claim.Document}' claims to be canonical for the {group.Key} surface, and so does {others}."),
                    claim.Line));
            }
        }
    }

    private static List<string> Tokenize(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim('"', '\'', '`', ',', ';', '(', ')'))
            .Where(t => t.Length > 0)
            .ToList();

    private static string BaseName(string token)
    {
        var slash = Math.Max(token.LastIndexOf('/'), token.LastIndexOf('\\'));
        return slash >= 0 ? token.Substring(slash + 1) : token;
    }

    private sealed class ManifestIndex
    {
        public HashSet<string> BuildTypes { get; }
        public HashSet<string> BuildParameters { get; }
        public HashSet<string> ModuleCommands { get; }
        public Dictionary<string, string> ModuleCommandNames { get; }
        public HashSet<string> ModuleParameters { get; }
        public HashSet<string> TemplateLocations { get; }

        public ManifestIndex(SurfaceManifest manifest)
        {
            HashSet<string> Names(SurfaceItemKind kind, Func<string, string>? map = null) =>
                manifest.Items.Where(i => i.Kind == kind).Select(i => map?.Invoke(i.Name) ?? i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            BuildTypes = Names(SurfaceItemKind.BuildType);
            BuildParameters = Names(SurfaceItemKind.BuildParameter, n => n.Replace("-", string.Empty));
            ModuleCommands = Names(SurfaceItemKind.ModuleCommand);
            ModuleParameters = Names(SurfaceItemKind.ModuleParameter);
            TemplateLocations = Names(SurfaceItemKind.TemplateLocation);
            ModuleCommandNames = manifest.Items.Where(i => i.Kind == SurfaceItemKind.ModuleCommand)
                .ToDictionary(i => i.Name, i => i.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}
