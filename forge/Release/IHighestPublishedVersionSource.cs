#nullable enable

using System.Threading.Tasks;

namespace Release;

/// <summary>The highest published version, used for the major-never-decreases check (I6).</summary>
public interface IHighestPublishedVersionSource
{
    Task<ReleaseVersion?> GetHighestPublishedAsync();
}
