#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using Nuke.Common.Tools.Git;

namespace Release;

/// <summary>
/// <see cref="IGitTagChecker"/> backed by the local git CLI (mirrors <c>Utilities.Git.CreateTag</c>'s
/// use of <c>GitTasks.Git</c>). CI checks out full history (<c>fetch-depth: 0</c>), so the local
/// tag list reflects the remote.
/// </summary>
public sealed class GitCliTagChecker : IGitTagChecker
{
    public Task<string?> FindTagCommitAsync(ReleaseVersion version)
    {
        var tag = version.ToTagString();

        try
        {
            var output = GitTasks.Git($"rev-parse \"{tag}^{{commit}}\"", logOutput: false, logInvocation: false);
            var sha = output.Select(o => o.Text?.Trim()).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            return Task.FromResult<string?>(sha);
        }
        catch (Exception)
        {
            // git rev-parse exits non-zero when the tag does not exist.
            return Task.FromResult<string?>(null);
        }
    }
}
