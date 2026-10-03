#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocsCheck;

/// <summary>A piece of a document with its 1-based line: prose, an inline code span, or a fenced-block statement.</summary>
internal sealed record Segment(int Line, string Text, SegmentKind Kind);

internal enum SegmentKind
{
    Prose,
    CodeSpan,
    CodeStatement,
}

/// <summary>A defect in the document's own text (an unclosed code fence, corrupted characters) rather than in a name it uses.</summary>
internal sealed record StructuralIssue(int Line, DocsCheckErrorCode Code, string Name, string Message);

internal sealed record Document(string Path, IReadOnlyList<Segment> Segments, IReadOnlyList<StructuralIssue> Issues);

/// <summary>
/// Finds the documents the check covers (S10.5: documentation site sources, the README, the PowerShell
/// help) and splits each into segments. Nothing under docs-template/ is ever listed or read (S10.9).
/// </summary>
internal static class DocumentLoader
{
    private static readonly Regex InlineCode = new("`([^`\\r\\n]+)`", RegexOptions.Compiled);
    private static readonly Regex TrailingComment = new(@"(^|\s)(#|//).*$", RegexOptions.Compiled);
    private static readonly Regex HelpKeyword = new(@"^\s*\.[A-Z]+\b", RegexOptions.Compiled);
    private static readonly Regex PowerShellPrompt = new(@"^\s*PS[^>]*>\s*", RegexOptions.Compiled);

    // U+FFFD, or UTF-8 text that was decoded as Windows-1252 or Latin-1 and saved again (an em dash, an accented letter or an emoji turned into two or three odd characters).
    private static readonly Regex CorruptedText = new(
        $"{(char)0xFFFD}|{(char)0xE2}{(char)0x20AC}|[{(char)0xC3}{(char)0xC2}][\x80-\xBF]|{(char)0xF0}{(char)0x178}", RegexOptions.Compiled);

    public static IReadOnlyList<string> Discover(string root)
    {
        var found = new List<string>();

        AddIfFile(found, root, "README.md");
        AddTree(found, root, "documentation/docs", "*.md", "*.mdx");
        AddTree(found, root, "documentation/src/pages", "*.md", "*.mdx");

        var moduleDirectory = System.IO.Path.Combine(root, "scripts", "powershell-module");
        if (Directory.Exists(moduleDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(moduleDirectory, "*.ps*1"))
            {
                var name = System.IO.Path.GetFileName(file);
                var extension = System.IO.Path.GetExtension(file);
                if (name.EndsWith(".Tests.ps1", StringComparison.OrdinalIgnoreCase)
                    || !(extension.Equals(".psm1", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                found.Add(Relative(root, file));
            }
        }

        return found.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    public static Document Load(string root, string relativePath)
    {
        var lines = File.ReadAllLines(System.IO.Path.Combine(root, relativePath));
        var isPowerShell = relativePath.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);

        var issues = new List<StructuralIssue>();
        var segments = isPowerShell ? SplitPowerShellHelp(lines) : SplitMarkdown(lines, issues);
        FindCorruptedText(lines, issues);

        return new Document(relativePath, segments, issues);
    }

    /// <summary>Reads only the canonical-contract markers of a file that is not itself checked.</summary>
    public static Document LoadMarkdownOnly(string root, string relativePath) => Load(root, relativePath);

    private static void FindCorruptedText(string[] lines, List<StructuralIssue> issues)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var match = CorruptedText.Match(lines[i]);
            if (match.Success)
            {
                issues.Add(new StructuralIssue(i + 1, DocsCheckErrorCode.CorruptedText, match.Value,
                    $"The text contains corrupted characters ('{match.Value}'): the file was saved with the wrong encoding."));
            }
        }
    }

    private static List<Segment> SplitMarkdown(string[] lines, List<StructuralIssue> issues)
    {
        var segments = new List<Segment>();
        var inFence = false;
        var fenceStart = 0;
        var fenceMarker = string.Empty;
        var pending = new List<string>();
        var pendingStart = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            var marker = trimmed.StartsWith("```", StringComparison.Ordinal) ? "```" : trimmed.StartsWith("~~~", StringComparison.Ordinal) ? "~~~" : string.Empty;
            if (marker.Length > 0 && (!inFence || marker == fenceMarker))
            {
                if (inFence)
                {
                    FlushStatement(segments, pending, pendingStart);
                }
                else
                {
                    fenceStart = i + 1;
                    fenceMarker = marker;
                }

                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                if (pending.Count == 0)
                {
                    pendingStart = i + 1;
                }

                var continues = EndsWithContinuation(line);
                pending.Add(continues ? StripContinuation(line) : line);
                if (!continues)
                {
                    FlushStatement(segments, pending, pendingStart);
                }

                continue;
            }

            segments.Add(new Segment(i + 1, line, SegmentKind.Prose));
            foreach (Match match in InlineCode.Matches(line))
            {
                segments.Add(new Segment(i + 1, match.Groups[1].Value, SegmentKind.CodeSpan));
            }
        }

        FlushStatement(segments, pending, pendingStart);
        if (inFence)
        {
            issues.Add(new StructuralIssue(fenceStart, DocsCheckErrorCode.UnclosedCodeFence, fenceMarker,
                $"The code fence opened at line {fenceStart} is never closed, so everything after it renders as code."));
        }

        return segments;
    }

    private static List<Segment> SplitPowerShellHelp(string[] lines)
    {
        var segments = new List<Segment>();
        var inHelp = false;
        var inExample = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (!inHelp)
            {
                var open = line.IndexOf("<#", StringComparison.Ordinal);
                if (open < 0)
                {
                    continue;
                }

                inHelp = true;
                inExample = false;
                line = line.Substring(open + 2);
            }

            var close = line.IndexOf("#>", StringComparison.Ordinal);
            var content = close >= 0 ? line.Substring(0, close) : line;

            if (HelpKeyword.IsMatch(content))
            {
                inExample = content.TrimStart().StartsWith(".EXAMPLE", StringComparison.OrdinalIgnoreCase);
            }
            else if (content.Trim().Length > 0)
            {
                segments.Add(new Segment(i + 1, content, SegmentKind.Prose));
                foreach (Match match in InlineCode.Matches(content))
                {
                    segments.Add(new Segment(i + 1, match.Groups[1].Value, SegmentKind.CodeSpan));
                }

                if (inExample)
                {
                    segments.Add(new Segment(i + 1, PowerShellPrompt.Replace(content, string.Empty), SegmentKind.CodeStatement));
                }
            }

            if (close >= 0)
            {
                inHelp = false;
                inExample = false;
            }
        }

        return segments;
    }

    private static void FlushStatement(List<Segment> segments, List<string> pending, int startLine)
    {
        if (pending.Count == 0)
        {
            return;
        }

        var text = string.Join(" ", pending.Select(p => StripComment(p).Trim()));
        pending.Clear();

        // Directory-tree drawings are not commands.
        if (text.Length > 0 && text.IndexOfAny(new[] { '├', '│', '└' }) < 0)
        {
            segments.Add(new Segment(startLine, text, SegmentKind.CodeStatement));
        }
    }

    private static string StripComment(string line) => TrailingComment.Replace(line, string.Empty);

    private static bool EndsWithContinuation(string line)
    {
        var trimmed = line.TrimEnd();
        return trimmed.EndsWith("\\", StringComparison.Ordinal) || trimmed.EndsWith("`", StringComparison.Ordinal);
    }

    private static string StripContinuation(string line) => line.TrimEnd().TrimEnd('\\', '`');

    private static void AddIfFile(List<string> found, string root, string relative)
    {
        if (File.Exists(System.IO.Path.Combine(root, relative)))
        {
            found.Add(relative);
        }
    }

    private static void AddTree(List<string> found, string root, string relativeDirectory, params string[] patterns)
    {
        var directory = System.IO.Path.Combine(root, relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var pattern in patterns)
        {
            foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
            {
                found.Add(Relative(root, file));
            }
        }
    }

    internal static string Relative(string root, string path) =>
        System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
}
