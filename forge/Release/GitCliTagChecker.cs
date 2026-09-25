#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using Nuke.Common.Tooling;

namespace Release;

/// <summary>
/// <see cref="IGitTagChecker"/> backed by the local git CLI (mirrors <c>Utilities.Git.GetLastTag</c>'s
/// exit-code handling). CI checks out full history (<c>fetch-depth: 0</c>), so the local tag list
/// reflects the remote. The lookup is qualified with <c>refs/tags/</c> so a branch of the same name
/// can never answer for the tag, and fails closed: only <c>--verify --quiet</c>'s "not found" exit
/// (1) means absent; any other failure throws rather than reporting the version free (I5).
/// </summary>
public sealed class GitCliTagChecker : IGitTagChecker
{
    private const int NotFoundExitCode = 1;

    public Task<string?> FindTagCommitAsync(ReleaseVersion version)
    {
        var tag = version.ToTagString();

        var process = ProcessTasks.StartProcess(
            "git",
            $"rev-parse --verify --quiet \"refs/tags/{tag}^{{commit}}\"",
            logOutput: false,
            logInvocation: false);
        process.WaitForExit();

        if (process.ExitCode == NotFoundExitCode)
        {
            return Task.FromResult<string?>(null);
        }

        if (process.ExitCode != 0)
        {
            var output = string.Join("\n", process.Output.Select(o => o.Text));
            throw new InvalidOperationException(
                $"Could not determine whether git tag '{tag}' exists (git exit {process.ExitCode}): {output}");
        }

        var sha = process.Output
            .Where(o => o.Type == OutputType.Std)
            .Select(o => o.Text?.Trim())
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return Task.FromResult<string?>(sha);
    }
}
