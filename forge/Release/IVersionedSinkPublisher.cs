#nullable enable

using System.Threading.Tasks;

namespace Release;

/// <summary>
/// The publisher of a versioned sink: the image versioned tag, the global tool or the PowerShell
/// module. Besides writing, it reports the identity of the artifact this run built and the identity
/// of the artifact the sink holds under a version, so a resume skips a sink only on a match (I5).
/// </summary>
public interface IVersionedSinkPublisher : IReleaseSinkPublisher
{
    /// <summary>
    /// The identity of the artifact this run built for the sink: the image digest, or the
    /// package's <see cref="PackageContentHash"/>. Computed by the build, before the claim.
    /// </summary>
    string BuiltIdentity { get; }

    /// <summary>
    /// The identity of the artifact the sink holds under <paramref name="version"/>, or null when it
    /// does not hold the version. Fails closed: a sink that cannot be read throws rather than
    /// reporting the version absent.
    /// </summary>
    Task<string?> FindPublishedIdentityAsync(ReleaseVersion version);
}
