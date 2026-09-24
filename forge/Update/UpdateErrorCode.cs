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
