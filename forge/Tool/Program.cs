#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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
    var updater = new Updater(runtime, new UpdateLogStore(), new SystemUpdateClock(), UpdaterIdentity.Current());

    try
    {
        // Nothing to clear is not a failure (contract § Global tool: "--clear-lock ... never touches the target").
        var cleared = await updater.ClearLockAsync(containerName);
        if (cleared == null)
        {
            Console.WriteLine($"No lock was held for '{containerName}'.");
            return 0;
        }

        var age = Updater.FormatAge(DateTimeOffset.UtcNow - cleared.CreatedAt);
        Console.WriteLine(
            $"Cleared the lock on '{containerName}': update {cleared.UpdateId}, held by host '{cleared.OwnerHost}', " +
            $"process {cleared.OwnerProcessId}, acquired at {cleared.CreatedAt:O}, held for {age}.");
        return 0;
    }
    catch (Exception ex) when (ex is DockerRuntimeException or System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static async Task<int> RunUpdateAsync(IDockerRuntime runtime, UpdateLogStore log, UpdateCommand.Run run)
{
    var updater = new Updater(runtime, log, new SystemUpdateClock(), UpdaterIdentity.Current());
    var options = new UpdateOptions(run.HealthTimeout, run.RestoreOnFailure);

    // design/10-design.md § Process interruption: the first SIGINT/SIGTERM is turned into cancellation so the
    // Updater can release its lock (before the target changes) or restore (after); a second one terminates as
    // the platform default does, for an operator who will not wait.
    using var interrupted = new CancellationTokenSource();
    var registrations = new List<PosixSignalRegistration>();
    foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM })
    {
        try
        {
            registrations.Add(PosixSignalRegistration.Create(signal, context =>
            {
                if (interrupted.IsCancellationRequested)
                {
                    return;
                }

                context.Cancel = true;
                Console.Error.WriteLine($"Interrupted ({context.Signal}); stopping the update safely. Signal again to terminate now.");
                interrupted.Cancel();
            }));
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    try
    {
        var outcome = await updater.RunAsync(run.ContainerName, run.ImageReference, options, run.NotifyUrl, interrupted.Token);
        return UpdateExitCode.ForOutcome(outcome);
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine($"The update of '{run.ContainerName}' was interrupted before it changed anything; the lock it took was released.");
        return 1;
    }
    catch (UpdateException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return UpdateExitCode.ForError(ex.Code);
    }
    catch (Exception ex)
    {
        // "Any other failure" (contract § Global tool, Exit statuses): a daemon error outside a mapped step, an
        // update log that cannot be read, no `docker` on PATH, or a fault of any other kind — all exit 1, never
        // the runtime's own crash status.
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
    finally
    {
        foreach (var registration in registrations)
        {
            registration.Dispose();
        }
    }
}
