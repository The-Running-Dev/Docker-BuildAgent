#nullable enable

using System;
using System.Linq;

namespace Release;

/// <summary>
/// The product version. One value per release (I1), semantic, without build metadata.
/// </summary>
public sealed record ReleaseVersion(int Major, int Minor, int Patch, string? PreRelease) : IComparable<ReleaseVersion>
{
    /// <summary>True for a pre-release version, such as 2.1.0-rc1.</summary>
    public bool IsPreRelease => PreRelease != null;

    /// <summary>
    /// Parses a version from either a git/GitHub tag form ("v2.0.0", "v2.0.0-beta.1") or a bare
    /// package form ("2.0.0", "2.0.0-beta.1") — the two forms GitVersion's own output and a
    /// hand-typed tag both take.
    /// </summary>
    public static ReleaseVersion Parse(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new FormatException("Release version cannot be empty.");
        }

        var withoutPrefix = (trimmed[0] == 'v' || trimmed[0] == 'V') ? trimmed[1..] : trimmed;

        var dashIndex = withoutPrefix.IndexOf('-');
        var core = dashIndex >= 0 ? withoutPrefix[..dashIndex] : withoutPrefix;
        var preRelease = dashIndex >= 0 ? withoutPrefix[(dashIndex + 1)..] : null;

        // Canonical forms only, so Parse(x).ToTagString() names the same tag x did: numeric parts are
        // plain digits without leading zeros, and the pre-release carries no build metadata ('+').
        var parts = core.Split('.');
        if (parts.Length != 3
            || !TryParseNumericPart(parts[0], out var major)
            || !TryParseNumericPart(parts[1], out var minor)
            || !TryParseNumericPart(parts[2], out var patch)
            || (preRelease != null && !IsValidPreRelease(preRelease)))
        {
            throw new FormatException($"'{value}' is not a valid release version (expected MAJOR.MINOR.PATCH[-PRERELEASE], optionally 'v'-prefixed, without build metadata).");
        }

        return new ReleaseVersion(major, minor, patch, preRelease);
    }

    private static bool TryParseNumericPart(string part, out int result)
    {
        result = 0;
        return part.Length > 0
            && part.All(char.IsAsciiDigit)
            && (part.Length == 1 || part[0] != '0')
            && int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static bool IsValidPreRelease(string preRelease) =>
        preRelease.Split('.').All(identifier =>
            identifier.Length > 0 && identifier.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));

    /// <summary>
    /// Semantic-version precedence: the numeric parts in order, then a pre-release below its
    /// release, then the pre-release identifiers one by one (numeric identifiers compare as
    /// numbers and sort below alphanumeric ones; a shorter list sorts below a longer one it prefixes).
    /// </summary>
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var core = Major.CompareTo(other.Major);
        if (core == 0)
        {
            core = Minor.CompareTo(other.Minor);
        }

        if (core == 0)
        {
            core = Patch.CompareTo(other.Patch);
        }

        if (core != 0)
        {
            return core;
        }

        if (PreRelease is null || other.PreRelease is null)
        {
            return (PreRelease is null ? 1 : 0) - (other.PreRelease is null ? 1 : 0);
        }

        var mine = PreRelease.Split('.');
        var theirs = other.PreRelease.Split('.');
        for (var i = 0; i < Math.Min(mine.Length, theirs.Length); i++)
        {
            var compared = CompareIdentifier(mine[i], theirs[i]);
            if (compared != 0)
            {
                return compared;
            }
        }

        return mine.Length.CompareTo(theirs.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = left.All(char.IsAsciiDigit);
        var rightNumeric = right.All(char.IsAsciiDigit);
        if (leftNumeric && rightNumeric)
        {
            // Compare as numbers without overflowing: a longer digit string is larger.
            var byLength = left.TrimStart('0').Length.CompareTo(right.TrimStart('0').Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(left.TrimStart('0'), right.TrimStart('0'));
        }

        if (leftNumeric != rightNumeric)
        {
            return leftNumeric ? -1 : 1;
        }

        return Math.Sign(string.CompareOrdinal(left, right));
    }

    /// <summary>Produces the git tag and the GitHub release tag, e.g. "v2.0.0".</summary>
    public string ToTagString() => $"v{ToPackageString()}";

    /// <summary>
    /// Produces the image tag, the tool package version and the module manifest version, e.g.
    /// "2.0.0". Differs from <see cref="ToTagString"/> only by the "v" prefix — a change to
    /// either form is a change to both.
    /// </summary>
    public string ToPackageString() => PreRelease is null
        ? $"{Major}.{Minor}.{Patch}"
        : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}
