#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Surface;

namespace Release;

/// <summary>
/// Assembles a release's notes (design/10-design.md, control flow 2, step 4): the authored notes
/// for the version when the repository has them, otherwise the commit subjects since the previous
/// release, with the breaking changes and deprecations the surface comparison detected inserted
/// into their sections. An empty section carries an explicit "None.". A section heading missing
/// from authored notes is not added: <see cref="ReleaseNotesValidator"/> then refuses the notes.
/// </summary>
public static class ReleaseNotesComposer
{
    /// <summary>Where the authored notes for a version live, relative to the repository root.</summary>
    public const string AuthoredNotesDirectory = "documentation/docs/release-notes";

    public const string None = "None.";

    public const string NoBaseline =
        "Not detected mechanically: this release has no earlier release to compare its public surface against.";

    private static readonly Regex RelativeLink = new(@"\]\((?!https?:|mailto:|#)([^)\s]+)\)", RegexOptions.Compiled);

    /// <summary>
    /// Composes the notes. <paramref name="comparison"/> is null when the release has no baseline,
    /// which only the first gated release may have.
    /// </summary>
    public static string Compose(string? authoredNotes, IReadOnlyList<string> commitSubjects, SurfaceComparison? comparison)
    {
        var body = string.IsNullOrWhiteSpace(authoredNotes) ? Generated(commitSubjects) : authoredNotes;
        var lines = body.Replace("\r\n", "\n").Split('\n').ToList();

        var differences = comparison?.All ?? Array.Empty<SurfaceDifference>();
        var breaking = differences
            .Where(d => d.Kind is not (SurfaceDifferenceKind.ItemAdded or SurfaceDifferenceKind.DeprecationAdded))
            .Select(Describe)
            .ToList();
        var deprecations = differences
            .Where(d => d.Kind == SurfaceDifferenceKind.DeprecationAdded)
            .Select(Describe)
            .ToList();

        FillSection(lines, ReleaseNotesValidator.IsBreakingChangesHeading, breaking, comparison == null ? NoBaseline : None);
        FillSection(lines, ReleaseNotesValidator.IsDeprecationsHeading, deprecations, None);

        return string.Join("\n", lines).Trim() + "\n";
    }

    /// <summary>
    /// Reads the authored notes for <paramref name="version"/>'s core version, so a release
    /// candidate carries the notes of the release it leads to. Returns null when there are none.
    /// The page's front matter, title and "Not yet released" notice are dropped, and its relative
    /// links are made absolute against <paramref name="commitSha"/>, so they resolve from the
    /// GitHub release.
    /// </summary>
    public static string? ReadAuthored(string rootDirectory, ReleaseVersion version, string repository, string commitSha)
    {
        var relativePath = $"{AuthoredNotesDirectory}/{version.Major}.{version.Minor}.{version.Patch}.md";
        var path = Path.Combine(rootDirectory, relativePath);
        if (!File.Exists(path))
        {
            return null;
        }

        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n').ToList();
        var start = 0;
        if (lines.Count > 0 && lines[0].Trim() == "---")
        {
            var end = lines.FindIndex(1, line => line.Trim() == "---");
            start = end < 0 ? 0 : end + 1;
        }

        var kept = new List<string>();
        var titleDropped = false;
        var inNotice = false;
        foreach (var line in lines.Skip(start))
        {
            var trimmed = line.Trim();
            if (inNotice)
            {
                inNotice = trimmed != ":::";
                continue;
            }

            if (trimmed.StartsWith(":::", StringComparison.Ordinal) && trimmed.Contains("Not yet released", StringComparison.OrdinalIgnoreCase))
            {
                inNotice = true;
                continue;
            }

            if (!titleDropped && ReleaseNotesValidator.HeadingLevel(line) == 1)
            {
                titleDropped = true;
                continue;
            }

            kept.Add(RelativeLink.Replace(line, match =>
                $"](https://github.com/{repository}/blob/{commitSha}/{Resolve(AuthoredNotesDirectory, match.Groups[1].Value)})"));
        }

        return string.Join("\n", kept).Trim() + "\n";
    }

    private static string Generated(IReadOnlyList<string> commitSubjects)
    {
        var changes = commitSubjects.Count == 0
            ? None
            : string.Join("\n", commitSubjects.Select(subject => $"- {subject}"));

        return $"## Changes\n\n{changes}\n\n## Breaking Changes\n\n## Deprecations\n";
    }

    private static void FillSection(List<string> lines, Func<string, bool> isHeading, IReadOnlyList<string> detected, string whenEmpty)
    {
        var heading = lines.FindIndex(line => isHeading(line));
        if (heading < 0)
        {
            return;
        }

        var level = ReleaseNotesValidator.HeadingLevel(lines[heading]);
        var end = lines.FindIndex(heading + 1, line =>
        {
            var lineLevel = ReleaseNotesValidator.HeadingLevel(line);
            return lineLevel > 0 && lineLevel <= level;
        });
        if (end < 0)
        {
            end = lines.Count;
        }

        // An authored "None." stops being true once a difference is detected, so it is dropped.
        var authored = lines.Skip(heading + 1).Take(end - heading - 1)
            .Where(line => !string.Equals(line.Trim().TrimEnd('.'), "None", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var authoredIsEmpty = authored.All(line =>
            string.IsNullOrWhiteSpace(line)
            || (line.Trim().StartsWith("<!--", StringComparison.Ordinal) && line.Trim().EndsWith("-->", StringComparison.Ordinal)));

        var section = new List<string> { string.Empty };
        if (!authoredIsEmpty)
        {
            section.AddRange(TrimBlankEdges(authored));
            section.Add(string.Empty);
        }

        if (detected.Count > 0)
        {
            section.Add("Detected in the public surface:");
            section.Add(string.Empty);
            section.AddRange(detected);
            section.Add(string.Empty);
        }
        else if (authoredIsEmpty)
        {
            section.Add(whenEmpty);
            section.Add(string.Empty);
        }

        lines.RemoveRange(heading + 1, end - heading - 1);
        lines.InsertRange(heading + 1, section);
    }

    private static IEnumerable<string> TrimBlankEdges(List<string> lines)
    {
        var first = lines.FindIndex(line => !string.IsNullOrWhiteSpace(line));
        var last = lines.FindLastIndex(line => !string.IsNullOrWhiteSpace(line));
        return first < 0 ? Enumerable.Empty<string>() : lines.Skip(first).Take(last - first + 1);
    }

    private static string Describe(SurfaceDifference d)
    {
        var change = d.Kind switch
        {
            SurfaceDifferenceKind.ItemRemoved => "removed",
            SurfaceDifferenceKind.ValueChanged => $"changed from `{d.BaselineValue}` to `{d.CandidateValue}`",
            SurfaceDifferenceKind.DeprecationAdded => $"deprecated since {d.CandidateValue}",
            SurfaceDifferenceKind.DeprecationRemoved => "no longer deprecated",
            SurfaceDifferenceKind.RemovalTargetChanged => $"removal moved from {d.BaselineValue ?? "none"} to {d.CandidateValue ?? "none"}",
            _ => d.Kind.ToString(),
        };

        return $"- {d.ItemKind} `{d.Name}`: {change}";
    }

    private static string Resolve(string directory, string relative)
    {
        var segments = directory.Split('/').ToList();
        foreach (var segment in relative.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
            }
            else if (segment != ".")
            {
                segments.Add(segment);
            }
        }

        return string.Join("/", segments);
    }
}
