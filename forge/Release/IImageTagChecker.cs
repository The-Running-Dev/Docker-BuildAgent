#nullable enable

using System.Threading.Tasks;

namespace Release;

/// <summary>Existence check against the container registry's versioned image tags (I5).</summary>
public interface IImageTagChecker
{
    Task<bool> ExistsAsync(ReleaseVersion version);
}
