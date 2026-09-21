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
        await runtime.RemoveAsync(lockName, force: true);
    }
    catch (DockerRuntimeException)
    {
        // Nothing to clear is not a failure (contract § Global tool: "--clear-lock ... never touches the target").
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
}
