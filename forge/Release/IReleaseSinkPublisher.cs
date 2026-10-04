#nullable enable

using System.Threading.Tasks;

namespace Release;

/// <summary>
/// Writes one sink's copy of a claimed version. <see cref="ReleasePipeline"/> runs the versioned
/// publishers (each an <see cref="IVersionedSinkPublisher"/>) in <see cref="ReleaseSink"/> numeric
/// order before the release is published, and the
/// <see cref="ReleaseSink.ImageLatestTag"/> publisher once, after the release is published (I4).
/// A publisher throwing surfaces as <see cref="ReleaseErrorCode.SinkPublishFailed"/> naming
/// <see cref="Sink"/>; nothing already written is deleted.
/// </summary>
public interface IReleaseSinkPublisher
{
    ReleaseSink Sink { get; }

    Task PublishAsync(ReleaseClaim claim);
}
