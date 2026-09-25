#nullable enable

namespace Release;

/// <summary>
/// Whether this run is the CI publishing context (I14). Only CI publishes; no sink accepts a
/// write from a developer machine.
/// </summary>
public interface ICiPublishingContext
{
    bool IsCiPublishing { get; }
}

/// <summary>
/// The production implementation: GitHub Actions sets both <c>CI</c> and <c>GITHUB_ACTIONS</c> to
/// "true" for every workflow run, including PR validation. Publishing is further scoped to the
/// three publishing workflows themselves, whose jobs alone pass <c>CreateGitHubRelease</c> /
/// invoke the release pipeline at all — a PR-validation run never calls <see cref="ReleasePipeline"/>.
/// This class exists so the "developer machine never publishes" half of I14 is enforced in code,
/// not only by workflow gating, and is therefore testable without CI.
/// </summary>
public sealed class EnvironmentCiPublishingContext : ICiPublishingContext
{
    private readonly System.Func<string, string?> _getEnvironmentVariable;

    public EnvironmentCiPublishingContext()
        : this(System.Environment.GetEnvironmentVariable)
    {
    }

    public EnvironmentCiPublishingContext(System.Func<string, string?> getEnvironmentVariable)
    {
        _getEnvironmentVariable = getEnvironmentVariable;
    }

    public bool IsCiPublishing =>
        IsTrue(_getEnvironmentVariable("CI")) && IsTrue(_getEnvironmentVariable("GITHUB_ACTIONS"));

    private static bool IsTrue(string? value) => string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);
}
