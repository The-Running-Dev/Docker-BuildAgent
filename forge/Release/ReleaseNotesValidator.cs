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

    private static bool HasHeading(string notes, string[] keywords)
    {
        using var reader = new System.IO.StringReader(notes);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (keywords.Any(keyword => trimmed.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
