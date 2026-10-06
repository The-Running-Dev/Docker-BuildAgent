#nullable enable

using System;

/// <summary>
/// Carries a contract exit status (see <c>design/20-contract.md</c>, exit statuses) out of a failing target.
/// </summary>
/// <remarks>NUKE catches every exception a target throws and returns its own non-zero status, so the status
/// cannot travel on the exception alone. Constructing the exception records the status in
/// <see cref="BuildFailure"/>; <c>Build&lt;T&gt;</c> returns it when the build fails.</remarks>
public sealed class BuildFailureException : Exception
{
    public BuildFailureException(int exitStatus, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitStatus = exitStatus;
        BuildFailure.Record(exitStatus);
    }

    /// <summary>The contract exit status this failure ends the build with.</summary>
    public int ExitStatus { get; }
}

/// <summary>
/// Records the contract exit status of the first classified failure in this process.
/// </summary>
public static class BuildFailure
{
    /// <summary>The Docker daemon was unavailable or rejected the request.</summary>
    public const int DockerDaemon = 5;

    /// <summary>A registry operation failed.</summary>
    public const int Registry = 6;

    private static int? _status;

    /// <summary>The status of the first recorded failure, or null when none was recorded.</summary>
    public static int? Status => _status;

    /// <summary>Records <paramref name="status"/> unless an earlier failure was already recorded.</summary>
    public static void Record(int status)
    {
        _status ??= status;
    }

    /// <summary>The status the build ends with: a failed run returns the recorded status when there is one.</summary>
    public static int Resolve(int executeStatus) =>
        executeStatus != 0 && _status is { } recorded ? recorded : executeStatus;

    internal static void Reset()
    {
        _status = null;
    }
}
