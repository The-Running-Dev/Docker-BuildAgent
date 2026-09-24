#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Update;

var arguments = args;
if (arguments.Length == 0 || arguments[0] != "update")
{
    Console.Error.WriteLine("Usage: <tool> update <container> [--image <reference>] [--health-timeout <duration>] [--no-restore] [--notify <url>]");
    Console.Error.WriteLine("       <tool> update --clear-lock <container>");
    return 1;
}

UpdateCommand command;
try
{
    command = UpdateCommandLine.Parse(arguments.Skip(1).ToList());
}
catch (UpdateCommandLineException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var runtime = new CliDockerRuntime();
var log = new UpdateLogStore();

switch (command)
{
    case UpdateCommand.ClearLock clearLock:
        return await RunClearLockAsync(runtime, clearLock.ContainerName);

    case UpdateCommand.Run run:
        return await RunUpdateAsync(runtime, log, run);

    default:
        throw new InvalidOperationException("unreachable");
}

static async Task<int> RunClearLockAsync(IDockerRuntime runtime, string containerName)
{
    var lockName = UpdateNaming.LockContainerName(containerName);
    try
    {
        // Nothing to clear is not a failure (contract § Global tool: "--clear-lock ... never touches the target"),
        // but a daemon that cannot be reached, or a removal it refuses, is.
        if (await runtime.InspectAsync(lockName) != null)
        {
            await runtime.RemoveAsync(lockName, force: true);
        }
    }
    catch (Exception ex) when (ex is DockerRuntimeException or System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    return 0;
}

static async Task<int> RunUpdateAsync(IDockerRuntime runtime, UpdateLogStore log, UpdateCommand.Run run)
{
    var updater = new Updater(runtime, log, new SystemUpdateClock(), UpdaterIdentity.Current());
    var options = new UpdateOptions(run.HealthTimeout, run.RestoreOnFailure);

    try
    {
        var outcome = await updater.RunAsync(run.ContainerName, run.ImageReference, options);
        return UpdateExitCode.ForOutcome(outcome);
    }
    catch (UpdateException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return UpdateExitCode.ForError(ex.Code);
    }
    catch (Exception ex) when (ex is DockerRuntimeException or System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
    {
        // "Any other failure" (contract § Global tool, Exit statuses): a daemon error outside a mapped step, an
        // update log that cannot be read or appended to after the swap, or no `docker` on PATH.
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}
