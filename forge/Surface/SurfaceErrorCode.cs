#nullable enable

using System;

namespace Surface;

public enum SurfaceErrorCode
{
    BaselineMissing,
    BaselineUnreadable,
    ManifestSchemaUnsupported,
    DerivationFailed,
    DuplicateItem,
    BlockingDifference,
}

public sealed class SurfaceException : Exception
{
    public SurfaceErrorCode Code { get; }

    public SurfaceException(SurfaceErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public SurfaceException(SurfaceErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
