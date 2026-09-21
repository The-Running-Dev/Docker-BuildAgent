#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Update;

/// <summary>
/// Parses the `update` command surface (contract § Global tool):
/// <c>update &lt;container&gt; [--image &lt;reference&gt;] [--health-timeout &lt;duration&gt;] [--no-restore] [--notify &lt;url&gt;]</c>
/// and <c>update --clear-lock &lt;container&gt;</c>. Hand-rolled rather than built on a CLI-parsing package: the
/// grammar is five flags and a positional argument, and a hand-rolled parser is both simpler and easier to test
/// than adjusting to a prerelease library's API from memory.
/// </summary>
public abstract record UpdateCommand
{
    public sealed record Run(
        string ContainerName,
        string? ImageReference,
        TimeSpan HealthTimeout,
        bool RestoreOnFailure,
        string? NotifyUrl) : UpdateCommand;

    public sealed record ClearLock(string ContainerName) : UpdateCommand;

    private UpdateCommand()
    {
    }
}

public sealed class UpdateCommandLineException : Exception
{
    public UpdateCommandLineException(string message) : base(message)
    {
    }
}

public static class UpdateCommandLine
{
    public static readonly TimeSpan DefaultHealthTimeout = TimeSpan.FromSeconds(120);

    private static readonly Regex SuffixedDuration = new(@"^(\d+)(s|m|h)$", RegexOptions.Compiled);

    public static UpdateCommand Parse(IReadOnlyList<string> args)
    {
        string? containerName = null;
        string? imageReference = null;
        var healthTimeout = DefaultHealthTimeout;
        var restoreOnFailure = true;
        string? notifyUrl = null;
        var clearLock = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--clear-lock":
                    clearLock = true;
                    break;
                case "--image":
                    imageReference = RequireValue(args, ref i, arg);
                    break;
                case "--health-timeout":
                    healthTimeout = ParseDuration(RequireValue(args, ref i, arg), arg);
                    break;
                case "--no-restore":
                    restoreOnFailure = false;
                    break;
                case "--notify":
                    notifyUrl = RequireValue(args, ref i, arg);
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new UpdateCommandLineException($"Unrecognized option '{arg}'.");
                    }

                    if (containerName != null)
                    {
                        throw new UpdateCommandLineException($"Unexpected extra argument '{arg}'.");
                    }

                    containerName = arg;
                    break;
            }
        }

        if (containerName == null)
        {
            throw new UpdateCommandLineException("A container name is required.");
        }

        if (clearLock)
        {
            if (imageReference != null || notifyUrl != null || !restoreOnFailure || healthTimeout != DefaultHealthTimeout)
            {
                throw new UpdateCommandLineException(
                    "--clear-lock takes no other action (contract § Global tool) and accepts no other option.");
            }

            return new UpdateCommand.ClearLock(containerName);
        }

        return new UpdateCommand.Run(containerName, imageReference, healthTimeout, restoreOnFailure, notifyUrl);
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
        {
            throw new UpdateCommandLineException($"Option '{optionName}' requires a value.");
        }

        index++;
        return args[index];
    }

    /// <summary>Accepts a bare integer as seconds, or an integer suffixed with s/m/h. Not specified by the
    /// contract beyond "a duration"; this is this slice's own disclosed choice of format.</summary>
    private static TimeSpan ParseDuration(string value, string optionName)
    {
        if (int.TryParse(value, out var bareSeconds))
        {
            return TimeSpan.FromSeconds(bareSeconds);
        }

        var match = SuffixedDuration.Match(value);
        if (!match.Success)
        {
            throw new UpdateCommandLineException(
                $"Option '{optionName}' expects a duration such as '120', '120s', '2m' or '1h'; got '{value}'.");
        }

        var amount = int.Parse(match.Groups[1].Value);
        return match.Groups[2].Value switch
        {
            "s" => TimeSpan.FromSeconds(amount),
            "m" => TimeSpan.FromMinutes(amount),
            "h" => TimeSpan.FromHours(amount),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }
}

/// <summary>The exit-status table (contract § Global tool, Exit statuses). 10/11/12 (restore outcomes) are S4's
/// and unused by anything S2 raises; a <see cref="UpdateErrorCode.ReplacementCreateFailed"/> falls to 1
/// ("Any other failure") because S2 does not implement the restore that would otherwise select 10, 11 or 12.</summary>
public static class UpdateExitCode
{
    public static int ForOutcome(UpdateOutcome outcome) => outcome switch
    {
        UpdateOutcome.Succeeded or UpdateOutcome.AlreadyCurrent => 0,
        UpdateOutcome.RestoredAfterUnhealthy => 10,
        UpdateOutcome.UnhealthyNotRestored => 11,
        UpdateOutcome.RestoreFailed => 12,
        _ => 1,
    };

    public static int ForError(UpdateErrorCode code) => code switch
    {
        UpdateErrorCode.TargetNotFound => 20,
        UpdateErrorCode.TargetAutoRemove => 20,
        UpdateErrorCode.TargetOrchestratorManaged => 20,
        UpdateErrorCode.TargetShapeUnsupported => 20,
        UpdateErrorCode.LockHeld => 21,
        UpdateErrorCode.ResiduePresent => 22,
        UpdateErrorCode.PinFailed => 23,
        UpdateErrorCode.LogUnwritable => 24,
        UpdateErrorCode.ImageUnavailable => 30,
        _ => 1,
    };
}
