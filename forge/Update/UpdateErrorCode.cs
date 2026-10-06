#nullable enable

using System;

namespace Update;

public enum UpdateErrorCode
{
    TargetNotFound,
    TargetAutoRemove,
    TargetOrchestratorManaged,
    TargetShapeUnsupported,
    ImageUnavailable,
    LockHeld,
    ResiduePresent,
    LogUnwritable,
    PinFailed,
    ReplacementCreateFailed,
    HealthCheckAbsent,
    HealthTimedOut,
    ReplacementExited,
    RestoreFailed,

    /// <summary>The process was signalled to stop after the target's first change; recorded as the failure code
    /// of the restore that follows (design/10-design.md § Process interruption).</summary>
    Interrupted,
}

public sealed class UpdateException : Exception
{
    public UpdateErrorCode Code { get; }
    public string ContainerName { get; }

    public UpdateException(UpdateErrorCode code, string containerName, string message)
        : base(message)
    {
        Code = code;
        ContainerName = containerName;
    }

    public UpdateException(UpdateErrorCode code, string containerName, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
        ContainerName = containerName;
    }
}
