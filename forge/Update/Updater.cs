#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Update;

public interface IUpdateClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Waits out <paramref name="delay"/>. A production clock really waits; a fake one can advance its
    /// own virtual <see cref="UtcNow"/> by <paramref name="delay"/> instead, so a health-wait loop is testable
    /// without a real wall-clock delay.</summary>
    Task Delay(TimeSpan delay);
}

public sealed class SystemUpdateClock : IUpdateClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public Task Delay(TimeSpan delay) => Task.Delay(delay);
}

/// <summary>Who is running this update, recorded on the lock so a later refusal can name an owner (S2.5).</summary>
public sealed record UpdaterIdentity(string OwnerHost, int OwnerProcessId)
{
    public static UpdaterIdentity Current() => new(Environment.MachineName, Environment.ProcessId);
}

/// <summary>
/// Moves a running container to a new image, or refuses without touching it (design/30-slices.md § S2), and
/// waits for the replacement to report healthy before letting it stand — restoring the prior container when it
/// does not (design/30-slices.md § S4).
/// </summary>
public sealed class Updater
{
    // The design names the components of the deadline (contract § I32, 10-design.md line 92) but not their
    // values; the deadline is diagnostic only (I32), never an authorization, so the exact margin is not
    // load-bearing for correctness.
    private static readonly TimeSpan LockStopGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LockDeadlineMargin = TimeSpan.FromSeconds(30);

    // Not enumerated by the design (10-design.md defers "which shapes" to a contract item it does not itself
    // supply for this check) or by the contract. This is this slice's own minimal, disclosed choice, distinct
    // from S2.8's two design-mandated shapes (anonymous volume, legacy link).
    private static readonly IReadOnlyList<string> OrchestratorLabelKeys = new[]
    {
        "com.docker.compose.project",
        "com.docker.swarm.service.id",
        "com.docker.stack.namespace",
        "io.kubernetes.pod.name",
    };

    private readonly IDockerRuntime _runtime;
    private readonly UpdateLogStore _log;
    private readonly IUpdateClock _clock;
    private readonly UpdaterIdentity _identity;

    public Updater(IDockerRuntime runtime, UpdateLogStore log, IUpdateClock clock, UpdaterIdentity identity)
    {
        _runtime = runtime;
        _log = log;
        _clock = clock;
        _identity = identity;
    }

    public async Task<UpdateOutcome> RunAsync(string containerName, string? imageReference, UpdateOptions options)
    {
        // Refuses every update on the host until corrected (S2.19) — checked first and unconditionally.
        var initialLogState = _log.ReadState();
        if (initialLogState.IsCorrupt)
        {
            throw CorruptLogException(containerName, initialLogState.CorruptLineNumber!.Value);
        }

        // Checked by name alone, before inspecting the target: the lock survives the target's id moving to a
        // different name mid-update (S2.2), and a stale lock refuses even if the target already looks idle (S2.5).
        var lockName = UpdateNaming.LockContainerName(containerName);
        var existingLock = await _runtime.InspectAsync(lockName);
        if (existingLock != null)
        {
            throw BuildLockHeldException(containerName, existingLock);
        }

        var target = await _runtime.InspectAsync(containerName)
            ?? throw new UpdateException(UpdateErrorCode.TargetNotFound, containerName,
                $"No container named '{containerName}' was found.");

        RefuseUnsupportedShape(containerName, target);

        var requestedReference = imageReference ?? target.ImageReference
            ?? throw new UpdateException(UpdateErrorCode.ImageUnavailable, containerName,
                $"Container '{containerName}' has no known image reference to resolve.");

        string resolvedImageId;
        try
        {
            resolvedImageId = await _runtime.ResolveImageIdAsync(requestedReference);
        }
        catch (DockerRuntimeException ex)
        {
            throw new UpdateException(UpdateErrorCode.ImageUnavailable, containerName,
                $"Image '{requestedReference}' could not be obtained: {ex.Message}", ex);
        }

        if (string.Equals(resolvedImageId, target.ImageId, StringComparison.Ordinal))
        {
            return UpdateOutcome.AlreadyCurrent;
        }

        var updateId = Guid.NewGuid();
        var now = _clock.UtcNow;
        var deadline = now + LockStopGrace + options.HealthTimeout + LockDeadlineMargin;
        var lockLabels = new Dictionary<string, string>
        {
            [UpdateNaming.LabelRole] = UpdateNaming.RoleLock,
            [UpdateNaming.LabelContainer] = containerName,
            [UpdateNaming.LabelUpdateId] = updateId.ToString(),
            ["com.buildagent.owner-host"] = _identity.OwnerHost,
            ["com.buildagent.owner-pid"] = _identity.OwnerProcessId.ToString(CultureInfo.InvariantCulture),
            ["com.buildagent.created-at"] = now.ToString("O", CultureInfo.InvariantCulture),
            ["com.buildagent.deadline"] = deadline.ToString("O", CultureInfo.InvariantCulture),
        };

        try
        {
            // The lock's own image is the target's current one — already local, so acquiring it pulls nothing (I33, S2.3).
            await _runtime.CreateMarkerAsync(lockName, target.ImageId, lockLabels);
        }
        catch (DockerRuntimeException)
        {
            // Only a lock that now exists means another updater won the race; any other create failure is the
            // daemon's own and propagates as it is, rather than being reported as LockHeld.
            var raced = await _runtime.InspectAsync(lockName);
            if (raced != null)
            {
                throw BuildLockHeldException(containerName, raced);
            }

            throw;
        }

        // Refusals from here through the log write are before the target's first change (I41): the lock this
        // attempt just created must not survive them.
        try
        {
            await CheckResidueAsync(containerName);

            var pinTag = UpdateNaming.PriorImageTag(containerName);
            try
            {
                await _runtime.TagImageAsync(target.ImageId, pinTag);
            }
            catch (DockerRuntimeException ex)
            {
                throw new UpdateException(UpdateErrorCode.PinFailed, containerName,
                    $"The prior image for '{containerName}' could not be pinned as '{pinTag}': {ex.Message}", ex);
            }

            var startRecord = new UpdateRecord(
                RecordSchemaVersion: 1,
                UpdateId: updateId,
                ContainerName: containerName,
                StartedAt: now,
                PriorImageId: target.ImageId,
                TargetImageReference: requestedReference,
                Options: options,
                CompletedAt: null,
                Outcome: null,
                FailureCode: null);

            try
            {
                _log.Append(startRecord);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                await TryRemoveImageTagAsync(pinTag);
                throw new UpdateException(UpdateErrorCode.LogUnwritable, containerName,
                    $"The start entry for '{containerName}' could not be written to the update log: {ex.Message}", ex);
            }

            // The first change to the target happens here. A failure past this point is not covered by I41:
            // it is S4's job, from here on, to bring the target back rather than leave residue for the next
            // update to refuse on.
            var priorName = UpdateNaming.PriorContainerName(containerName);
            try
            {
                await _runtime.StopAsync(containerName);
                await _runtime.RenameAsync(containerName, priorName);
                var spec = BuildReplacementSpec(containerName, target, resolvedImageId);
                await _runtime.CreateReplacementAsync(spec);
                await _runtime.StartAsync(containerName);
            }
            catch (DockerRuntimeException ex)
            {
                // S4.10: no updated container exists to keep, so restore is unconditional here regardless of
                // --no-restore — there is nothing "unhealthy" to leave in place, only a failed creation attempt.
                await Console.Error.WriteLineAsync(
                    $"The replacement for '{containerName}' could not be created: {ex.Message}");
                var (restoreOutcome, restoreDetail) = await AttemptRestoreAsync(containerName, priorName);
                if (restoreDetail != null)
                {
                    await Console.Error.WriteLineAsync(restoreDetail);
                }

                var code = restoreOutcome == UpdateOutcome.RestoreFailed
                    ? UpdateErrorCode.RestoreFailed
                    : UpdateErrorCode.ReplacementCreateFailed;
                return await FinalizeAsync(startRecord, lockName, restoreOutcome, code);
            }

            var healthOutcome = await WaitForHealthAsync(containerName, options.HealthTimeout);
            if (healthOutcome == HealthWaitOutcome.Healthy)
            {
                var outcome = await FinalizeAsync(startRecord, lockName, UpdateOutcome.Succeeded, null);
                await TryRemoveContainerAsync(priorName);
                return outcome;
            }

            var failureCode = healthOutcome switch
            {
                HealthWaitOutcome.TimedOut => UpdateErrorCode.HealthTimedOut,
                HealthWaitOutcome.Absent => UpdateErrorCode.HealthCheckAbsent,
                HealthWaitOutcome.Exited => UpdateErrorCode.ReplacementExited,
                _ => throw new InvalidOperationException("unreachable"),
            };

            if (!options.RestoreOnFailure)
            {
                // S4.5: left in place for manual recovery — the replacement, the prior container and its pin
                // are none of them touched.
                return await FinalizeAsync(startRecord, lockName, UpdateOutcome.UnhealthyNotRestored, failureCode);
            }

            var (restored, restoredDetail) = await AttemptRestoreAsync(containerName, priorName);
            if (restoredDetail != null)
            {
                await Console.Error.WriteLineAsync(restoredDetail);
            }

            return await FinalizeAsync(startRecord, lockName, restored,
                restored == UpdateOutcome.RestoreFailed ? UpdateErrorCode.RestoreFailed : failureCode);
        }
        catch (UpdateException ex) when (ex.Code is UpdateErrorCode.ResiduePresent or UpdateErrorCode.PinFailed or UpdateErrorCode.LogUnwritable)
        {
            await TryRemoveContainerAsync(lockName);
            throw;
        }
    }

    private enum HealthWaitOutcome
    {
        Healthy,
        Absent,
        TimedOut,
        Exited,
    }

    // Not specified by the contract beyond "the timeout" (S4.7); this slice's own disclosed poll cadence.
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromSeconds(1);

    private async Task<HealthWaitOutcome> WaitForHealthAsync(string containerName, TimeSpan timeout)
    {
        var deadline = _clock.UtcNow + timeout;

        while (true)
        {
            var inspection = await _runtime.InspectAsync(containerName);
            if (inspection == null || !inspection.Running)
            {
                return HealthWaitOutcome.Exited;
            }

            if (inspection.HealthStatus == null)
            {
                return HealthWaitOutcome.Absent;
            }

            if (inspection.HealthStatus == "healthy")
            {
                return HealthWaitOutcome.Healthy;
            }

            if (_clock.UtcNow >= deadline)
            {
                return HealthWaitOutcome.TimedOut;
            }

            await _clock.Delay(HealthPollInterval);
        }
    }

    /// <summary>Returns the prior container to <paramref name="containerName"/> and starts it (S4.2, S4.8).
    /// Removes whatever currently occupies that name first, if anything (an unhealthy or half-created
    /// replacement). Never reconstructs the prior container from stored inspection data (S4.8) — only an actual
    /// rename of the retained container counts, so a prior container removed out from under this update fails
    /// with <see cref="UpdateOutcome.RestoreFailed"/> rather than approximating one.</summary>
    private async Task<(UpdateOutcome Outcome, string? FailureDetail)> AttemptRestoreAsync(string containerName, string priorName)
    {
        var current = await _runtime.InspectAsync(containerName);
        if (current != null)
        {
            try
            {
                await _runtime.RemoveAsync(containerName, force: true);
            }
            catch (DockerRuntimeException ex)
            {
                return (UpdateOutcome.RestoreFailed,
                    $"The replacement '{containerName}' could not be removed to make way for restore: {ex.Message}");
            }
        }

        var prior = await _runtime.InspectAsync(priorName);
        if (prior == null)
        {
            return (UpdateOutcome.RestoreFailed,
                $"The prior container '{priorName}' is no longer present; restore cannot proceed.");
        }

        try
        {
            await _runtime.RenameAsync(priorName, containerName);
            await _runtime.StartAsync(containerName);
        }
        catch (DockerRuntimeException ex)
        {
            return (UpdateOutcome.RestoreFailed,
                $"The prior container '{priorName}' (labels: {FormatLabels(prior.Labels)}) could not be restored to '{containerName}': {ex.Message}");
        }

        return (UpdateOutcome.RestoredAfterUnhealthy, null);
    }

    private static string FormatLabels(IReadOnlyDictionary<string, string> labels) =>
        labels.Count == 0 ? "(none)" : string.Join(", ", labels.Select(kv => $"{kv.Key}={kv.Value}"));

    /// <summary>Writes the outcome record and releases the lock. A log-append failure here does not change the
    /// returned outcome (S4.11): it is reported on stderr, and the stranded start record this leaves behind is
    /// what the next update's residue check (S2.9) detects and refuses on.</summary>
    private async Task<UpdateOutcome> FinalizeAsync(UpdateRecord startRecord, string lockName, UpdateOutcome outcome, UpdateErrorCode? failureCode)
    {
        var completedRecord = startRecord with
        {
            CompletedAt = _clock.UtcNow,
            Outcome = outcome,
            FailureCode = failureCode?.ToString(),
        };

        try
        {
            _log.Append(completedRecord);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync(
                $"The outcome ({outcome}) for update {startRecord.UpdateId} of '{startRecord.ContainerName}' " +
                $"could not be written to the update log at '{_log.Path}': {ex.Message}. The stranded start " +
                "record will be detected as residue by the next update.");
        }

        await TryRemoveContainerAsync(lockName);
        return outcome;
    }

    private void RefuseUnsupportedShape(string containerName, ContainerInspection target)
    {
        if (target.AutoRemove)
        {
            throw new UpdateException(UpdateErrorCode.TargetAutoRemove, containerName,
                $"Container '{containerName}' runs with --rm; nothing of it could be preserved.");
        }

        var orchestratorLabel = OrchestratorLabelKeys.FirstOrDefault(k => target.Labels.ContainsKey(k));
        if (orchestratorLabel != null)
        {
            throw new UpdateException(UpdateErrorCode.TargetOrchestratorManaged, containerName,
                $"Container '{containerName}' carries orchestrator ownership label '{orchestratorLabel}'; the orchestrator owns its updates.");
        }

        if (target.Links.Count > 0)
        {
            throw new UpdateException(UpdateErrorCode.TargetShapeUnsupported, containerName,
                $"Container '{containerName}' uses a legacy container link ('{target.Links[0]}'), which cannot be reproduced exactly.");
        }

        var anonymousVolume = target.Mounts.FirstOrDefault(m => m.IsAnonymousVolume);
        if (anonymousVolume != null)
        {
            throw new UpdateException(UpdateErrorCode.TargetShapeUnsupported, containerName,
                $"Container '{containerName}' has an anonymous volume mounted at '{anonymousVolume.Destination}', which cannot be reproduced exactly.");
        }
    }

    private async Task CheckResidueAsync(string containerName)
    {
        var priorName = UpdateNaming.PriorContainerName(containerName);
        var priorInspection = await _runtime.InspectAsync(priorName);
        if (priorInspection != null)
        {
            throw new UpdateException(UpdateErrorCode.ResiduePresent, containerName,
                $"A prior container '{priorName}' from an earlier update of '{containerName}' is still present; " +
                $"remove it with 'docker rm {priorName}' (after confirming it is not needed to restore) before updating again.");
        }

        var logState = _log.ReadState();
        if (logState.IsCorrupt)
        {
            throw CorruptLogException(containerName, logState.CorruptLineNumber!.Value);
        }

        var open = logState.OpenRecords.FirstOrDefault(r => r.ContainerName == containerName);
        if (open != null)
        {
            throw new UpdateException(UpdateErrorCode.ResiduePresent, containerName,
                $"An update of '{containerName}' (update {open.UpdateId}) has a start entry in the update log " +
                $"at '{_log.Path}' with no outcome; after confirming the target's actual state, clear it by " +
                $"removing that update's line from the log (or appending a completing outcome line for update " +
                $"{open.UpdateId}) before updating again.");
        }
    }

    private UpdateException CorruptLogException(string containerName, int lineNumber) =>
        new(UpdateErrorCode.ResiduePresent, containerName,
            $"The update log at '{_log.Path}' has a line (line {lineNumber}) that cannot be parsed; " +
            "every update on this host is refused until an operator corrects or removes it.");

    private UpdateException BuildLockHeldException(string containerName, ContainerInspection lockInspection)
    {
        var labels = lockInspection.Labels;
        var ownerHost = labels.GetValueOrDefault("com.buildagent.owner-host", "an unknown host");
        var ownerPid = labels.GetValueOrDefault("com.buildagent.owner-pid", "an unknown process");

        DateTimeOffset? createdAt = labels.TryGetValue("com.buildagent.created-at", out var createdAtText) &&
            DateTimeOffset.TryParse(createdAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedCreatedAt)
                ? parsedCreatedAt
                : null;

        DateTimeOffset? deadline = labels.TryGetValue("com.buildagent.deadline", out var deadlineText) &&
            DateTimeOffset.TryParse(deadlineText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDeadline)
                ? parsedDeadline
                : null;

        var now = _clock.UtcNow;
        var ageText = createdAt.HasValue ? FormatAge(now - createdAt.Value) : "an unknown age";

        var message = $"Container '{containerName}' is locked by host '{ownerHost}', process {ownerPid}, held for {ageText}.";
        if (deadline.HasValue && now > deadline.Value)
        {
            message += " The lock's deadline has passed; a passed deadline does not authorise taking it over.";
        }

        return new UpdateException(UpdateErrorCode.LockHeld, containerName, message);
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return age.TotalHours >= 1
            ? $"{(int)age.TotalHours}h{age.Minutes}m"
            : age.TotalMinutes >= 1
                ? $"{(int)age.TotalMinutes}m{age.Seconds}s"
                : $"{age.Seconds}s";
    }

    /// <summary>Internal rather than private so <c>RoundTripProbeTests</c> (S2.22) can build a replacement spec
    /// from a live inspection through the exact path the Updater itself uses, rather than a re-implementation
    /// that could drift from it.</summary>
    internal static ContainerCreateSpec BuildReplacementSpec(string containerName, ContainerInspection target, string newImageId)
    {
        var labels = target.Labels
            .Where(kv => !kv.Key.StartsWith("com.buildagent.", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return new ContainerCreateSpec(
            Name: containerName,
            ImageId: newImageId,
            Labels: labels,
            Env: target.Env,
            Command: target.Command,
            Entrypoint: target.Entrypoint,
            Mounts: target.Mounts,
            Ports: target.Ports,
            RestartPolicy: target.RestartPolicy,
            Networks: target.Networks);
    }

    private async Task TryRemoveContainerAsync(string name)
    {
        try
        {
            await _runtime.RemoveAsync(name, force: true);
        }
        catch (DockerRuntimeException)
        {
        }
    }

    private async Task TryRemoveImageTagAsync(string tag)
    {
        try
        {
            await _runtime.RemoveImageTagAsync(tag);
        }
        catch (DockerRuntimeException)
        {
        }
    }
}
