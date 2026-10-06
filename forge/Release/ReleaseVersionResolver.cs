#nullable enable

using System;

namespace Release;

/// <summary>
/// Decides the version a release run publishes: the pushed tag on a tag push ("v2.1.0-rc1"), or
/// the core version GitVersion computed for the commit plus an optional pre-release label on a
/// manual dispatch. A version is never typed by hand, so a dispatch cannot choose a core version
/// GitVersion did not compute.
/// </summary>
public static class ReleaseVersionResolver
{
    /// <summary>
    /// Resolves the version from exactly one of <paramref name="tag"/> or <paramref name="core"/>.
    /// Throws <see cref="ArgumentException"/> for a malformed request and a
    /// <see cref="ReleaseException"/> with <see cref="ReleaseErrorCode.PreReleaseLabelUnsupported"/>
    /// for a label every sink cannot hold.
    /// </summary>
    public static ReleaseVersion Resolve(string? tag, string? core, string? label)
    {
        var hasTag = !string.IsNullOrWhiteSpace(tag);
        var hasCore = !string.IsNullOrWhiteSpace(core);
        var hasLabel = !string.IsNullOrWhiteSpace(label);

        if (hasTag == hasCore)
        {
            throw new ArgumentException("Give either the release tag or the core version, not both and not neither.");
        }

        ReleaseVersion version;
        if (hasTag)
        {
            if (hasLabel)
            {
                throw new ArgumentException("A tag carries its own pre-release label; do not give a label with a tag.");
            }

            var trimmed = tag!.Trim();
            if (!trimmed.StartsWith("v", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Release tag '{trimmed}' must have the form v<MAJOR>.<MINOR>.<PATCH>[-<label>].");
            }

            version = Parse(trimmed);
        }
        else
        {
            var computed = Parse(core!);
            if (computed.IsPreRelease)
            {
                throw new ArgumentException($"Core version '{core!.Trim()}' must be MAJOR.MINOR.PATCH; give a pre-release label separately.");
            }

            version = hasLabel ? computed with { PreRelease = label!.Trim() } : computed;
        }

        ReleasePipeline.EnsureSupportedPreRelease(version);
        return version;
    }

    private static ReleaseVersion Parse(string value)
    {
        try
        {
            return ReleaseVersion.Parse(value);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(ex.Message, ex);
        }
    }
}
