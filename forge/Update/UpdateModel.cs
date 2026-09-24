#nullable enable

using System;

namespace Update;

public enum UpdateOutcome
{
    Succeeded,
    AlreadyCurrent,
    RestoredAfterUnhealthy,
    UnhealthyNotRestored,
    RestoreFailed,
    Refused,
}

public sealed record UpdateOptions(
    TimeSpan HealthTimeout,
    bool RestoreOnFailure);

public sealed record UpdateRecord(
    int RecordSchemaVersion,
    Guid UpdateId,
    string ContainerName,
    DateTimeOffset StartedAt,
    string PriorImageId,
    string TargetImageReference,
    UpdateOptions Options,
    DateTimeOffset? CompletedAt,
    UpdateOutcome? Outcome,
    string? FailureCode);

public sealed record UpdateLock(
    Guid UpdateId,
    string ContainerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset Deadline,
    string OwnerHost,
    int OwnerProcessId);
