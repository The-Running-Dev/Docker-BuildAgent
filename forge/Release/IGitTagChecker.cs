#nullable enable

using System.Threading.Tasks;

namespace Release;

/// <summary>Existence check against git tags (I5). Returns the commit SHA the tag points at, or null.</summary>
public interface IGitTagChecker
{
    Task<string?> FindTagCommitAsync(ReleaseVersion version);
}
