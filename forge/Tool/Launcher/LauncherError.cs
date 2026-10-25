#nullable enable

using System;

namespace Launcher;

/// <summary>design/20-contract.md § Launchers — the failures a launcher raises around the build.</summary>
public enum LauncherErrorCode
{
    DockerUnavailable,
    ImageUnavailable,
    WorkspaceInvalid,
    BuildFailed,
}

public sealed record LauncherError(LauncherErrorCode Code, string Message);

/// <summary>Raised before the build container starts; <see cref="ExitStatus"/> is what the tool exits with.</summary>
public sealed class LauncherException : Exception
{
    public LauncherException(LauncherError error)
        : base(error.Message)
    {
        Error = error;
    }

    public LauncherError Error { get; }

    public int ExitStatus => Error.Code switch
    {
        LauncherErrorCode.DockerUnavailable or LauncherErrorCode.ImageUnavailable => 5,
        LauncherErrorCode.WorkspaceInvalid => 3,
        _ => 1,
    };
}
