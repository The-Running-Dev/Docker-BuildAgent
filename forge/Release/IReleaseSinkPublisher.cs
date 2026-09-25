#nullable enable

using System.Threading.Tasks;

namespace Release;

/// <summary>
/// Writes one sink's copy of a claimed version. Publishers are run by <see cref="ReleasePipeline"/>
/// in <see cref="ReleaseSink"/> numeric order, so <see cref="ReleaseSink.ImageLatestTag"/> always
/// runs last (I4). A publisher throwing surfaces as <see cref="ReleaseErrorCode.SinkPublishFailed"/>
/// naming <see cref="Sink"/>; the claim is left open for a resume rather than deleted.
/// </summary>
public interface IReleaseSinkPublisher
{
    ReleaseSink Sink { get; }

    Task PublishAsync(ReleaseClaim claim);
}
