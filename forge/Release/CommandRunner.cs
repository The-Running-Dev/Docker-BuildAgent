#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Release;

/// <summary>The outcome of one command: its exit code and everything it wrote.</summary>
public sealed record CommandResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Standard output and standard error together, for matching a tool's own messages.</summary>
    public string Combined => $"{Output}\n{Error}";
}

/// <summary>
/// Runs an external command (docker, dotnet) for a release sink. Arguments are passed one by one,
/// never through a shell string. An implementation never logs the arguments: a push carries the
/// feed's API key among them.
/// </summary>
public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments);
}

/// <summary>The production <see cref="ICommandRunner"/>, backed by <see cref="Process"/>.</summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new CommandResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }
}

public static class CommandRunnerExtensions
{
    /// <summary>
    /// Runs the command and throws when it exits non-zero. The message names the command by its
    /// first two arguments only (such as <c>dotnet nuget push</c>), so a secret among the later
    /// arguments never reaches a log.
    /// </summary>
    public static async Task<CommandResult> RunCheckedAsync(this ICommandRunner runner, string fileName, IReadOnlyList<string> arguments)
    {
        var result = await runner.RunAsync(fileName, arguments).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{Describe(fileName, arguments)} exited {result.ExitCode}: {result.Error.Trim()}");
        }

        return result;
    }

    internal static string Describe(string fileName, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { fileName }.Concat(arguments.Take(2)));
}
