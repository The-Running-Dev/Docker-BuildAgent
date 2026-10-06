#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Release;

/// <summary>
/// I7: every published release's notes contain a breaking-changes section and a deprecations
/// section, present when empty. A heading is enough — an empty body under it passes.
/// </summary>
public static class ReleaseNotesValidator
{
    private static readonly string[] BreakingChangeKeywords = { "breaking change", "breaking-change" };
    private static readonly string[] DeprecationKeywords = { "deprecation" };

    public static void Validate(string notes)
    {
        notes ??= string.Empty;

        var hasBreakingChanges = HasHeading(notes, BreakingChangeKeywords);
        var hasDeprecations = HasHeading(notes, DeprecationKeywords);

        if (hasBreakingChanges && hasDeprecations)
        {
            return;
        }

        var missing = new List<string>();
        if (!hasBreakingChanges)
        {
            missing.Add("breaking-changes");
        }

        if (!hasDeprecations)
        {
            missing.Add("deprecations");
        }

        throw new ReleaseException(
            ReleaseErrorCode.NotesSectionMissing,
            null,
            $"Release notes are missing required section(s): {string.Join(", ", missing)}.");
    }

    /// <summary>True when <paramref name="line"/> is the heading of the breaking-changes section.</summary>
    internal static bool IsBreakingChangesHeading(string line) => IsSectionHeading(line, BreakingChangeKeywords);

    /// <summary>True when <paramref name="line"/> is the heading of the deprecations section.</summary>
    internal static bool IsDeprecationsHeading(string line) => IsSectionHeading(line, DeprecationKeywords);

    /// <summary>The ATX heading level of <paramref name="line"/>, or 0 when it is not a heading.</summary>
    internal static int HeadingLevel(string line)
    {
        // A Markdown ATX heading: 1-6 '#' followed by a space. "#42 breaking change" is an
        // issue reference, not a section.
        var trimmed = line.TrimStart();
        var level = trimmed.TakeWhile(c => c == '#').Count();
        return level is < 1 or > 6 || level == trimmed.Length || !char.IsWhiteSpace(trimmed[level]) ? 0 : level;
    }

    private static bool HasHeading(string notes, string[] keywords)
    {
        using var reader = new System.IO.StringReader(notes);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (IsSectionHeading(line, keywords))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSectionHeading(string line, string[] keywords) =>
        HeadingLevel(line) > 0 && keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
