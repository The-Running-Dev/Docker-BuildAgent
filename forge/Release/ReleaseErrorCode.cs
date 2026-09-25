#nullable enable

using System;

namespace Release;

public enum ReleaseErrorCode
{
    VersionAlreadyExists,
    MajorBelowCurrent,
    TagPointsElsewhere,
    NotesSectionMissing,
    SurfaceGateFailed,
    ClaimCreationFailed,
    SinkPublishFailed,
    NotPublishedByCi,
}

/// <summary>
/// ReleaseError(ReleaseErrorCode Code, ReleaseSink? Sink, string Message) — see
/// design/20-contract.md, "Release pipeline". <see cref="Sink"/> is populated only for
/// <see cref="ReleaseErrorCode.SinkPublishFailed"/>.
/// </summary>
public sealed class ReleaseException : Exception
{
    public ReleaseErrorCode Code { get; }

    public ReleaseSink? Sink { get; }

    public ReleaseException(ReleaseErrorCode code, ReleaseSink? sink, string message)
        : base(message)
    {
        Code = code;
        Sink = sink;
    }

    public ReleaseException(ReleaseErrorCode code, ReleaseSink? sink, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
        Sink = sink;
    }
}
