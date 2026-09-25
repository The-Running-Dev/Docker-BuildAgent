#nullable enable

using System;

namespace Release;

/// <summary>
/// The product version. One value per release (I1), semantic, without build metadata.
/// </summary>
public sealed record ReleaseVersion(int Major, int Minor, int Patch, string? PreRelease)
{
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

        var parts = core.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            throw new FormatException($"'{value}' is not a valid release version (expected MAJOR.MINOR.PATCH[-PRERELEASE], optionally 'v'-prefixed).");
        }

        return new ReleaseVersion(major, minor, patch, string.IsNullOrEmpty(preRelease) ? null : preRelease);
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
