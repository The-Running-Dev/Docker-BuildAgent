#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Release.Tests.TestSupport;

/// <summary>
/// An <see cref="ICommandRunner"/> that records each call and answers from the first rule whose
/// argument prefix matches; a call no rule matches succeeds with no output.
/// </summary>
public sealed class FakeCommandRunner : ICommandRunner
{
    private readonly List<(string[] Prefix, Func<IReadOnlyList<string>, CommandResult> Answer)> _rules = new();

    public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = new();

    public FakeCommandRunner On(string[] argumentPrefix, Func<IReadOnlyList<string>, CommandResult> answer)
    {
        _rules.Add((argumentPrefix, answer));
        return this;
    }

    public FakeCommandRunner On(string[] argumentPrefix, CommandResult result) => On(argumentPrefix, _ => result);

    public Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments)
    {
        Calls.Add((fileName, arguments.ToArray()));
        foreach (var (prefix, answer) in _rules)
        {
            if (arguments.Count >= prefix.Length && prefix.SequenceEqual(arguments.Take(prefix.Length)))
            {
                return Task.FromResult(answer(arguments));
            }
        }

        return Task.FromResult(new CommandResult(0, string.Empty, string.Empty));
    }

    public static CommandResult Ok(string output = "") => new(0, output, string.Empty);

    public static CommandResult Fail(string error, int exitCode = 1) => new(exitCode, string.Empty, error);
}
