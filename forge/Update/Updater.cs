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
                return await RestoreAndFinalizeAsync(startRecord, lockName, containerName, priorName, target.Id,
                    UpdateErrorCode.ReplacementCreateFailed);
            }

            var healthOutcome = await WaitForHealthAsync(containerName, options.HealthTimeout);
            if (healthOutcome == HealthWaitOutcome.Healthy)
            {
                // Design step 10: the prior container is removed before the outcome is written and the lock
                // released. A removal the daemon refuses does not undo a healthy update, but it is reported,
                // since the next update of this container refuses on the prior container as residue (S2.9).
                try
                {
                    await _runtime.RemoveAsync(priorName, force: true);
                }
                catch (DockerRuntimeException ex)
                {
                    await Console.Error.WriteLineAsync(
                        $"'{containerName}' was updated, but its prior container '{priorName}' could not be removed: " +
                        $"{ex.Message}. The next update of '{containerName}' refuses until it is removed.");
                }

                return await FinalizeAsync(startRecord, lockName, UpdateOutcome.Succeeded, null);
            }

            var (failureCode, failureMessage) = healthOutcome switch
            {
                HealthWaitOutcome.TimedOut => (UpdateErrorCode.HealthTimedOut,
                    $"The replacement '{containerName}' did not report healthy within {options.HealthTimeout}."),
                HealthWaitOutcome.Absent => (UpdateErrorCode.HealthCheckAbsent,
                    $"The replacement '{containerName}' declares no health check, so it cannot be verified."),
                HealthWaitOutcome.Exited => (UpdateErrorCode.ReplacementExited,
                    $"The replacement '{containerName}' stopped running before it reported healthy."),
                _ => throw new InvalidOperationException("unreachable"),
            };
            await Console.Error.WriteLineAsync($"{failureMessage} ({failureCode})");

            if (!options.RestoreOnFailure)
            {
                // S4.5: left in place for manual recovery — the replacement, the prior container and its pin
                // are none of them touched.
                return await FinalizeAsync(startRecord, lockName, UpdateOutcome.UnhealthyNotRestored, failureCode);
            }

            return await RestoreAndFinalizeAsync(startRecord, lockName, containerName, priorName, target.Id, failureCode);
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
            var verdict = await PollHealthAsync(containerName);
            if (verdict.HasValue)
            {
                return verdict.Value;
            }

            if (_clock.UtcNow >= deadline)
            {
                return HealthWaitOutcome.TimedOut;
            }

            await _clock.Delay(HealthPollInterval);
        }
    }

    /// <summary>One health poll; null means "not healthy yet, keep waiting".</summary>
    private async Task<HealthWaitOutcome?> PollHealthAsync(string containerName)
    {
        ContainerInspection? inspection;
        try
        {
            inspection = await _runtime.InspectAsync(containerName);
        }
        catch (DockerRuntimeException)
        {
            // A daemon error while polling is no verdict on the replacement. Letting it escape would skip
            // restore, the outcome record and the lock release, so it counts as "not healthy yet".
            return null;
        }

        if (inspection == null || !inspection.Running)
        {
            return HealthWaitOutcome.Exited;
        }

        if (inspection.HealthStatus == null)
        {
            return HealthWaitOutcome.Absent;
        }

        return inspection.HealthStatus == "healthy" ? HealthWaitOutcome.Healthy : null;
    }

    /// <summary>Restores, then writes the outcome and releases the lock: <see cref="UpdateOutcome.RestoredAfterUnhealthy"/>
    /// carrying <paramref name="failureCode"/>, or <see cref="UpdateOutcome.RestoreFailed"/> with the reason on stderr.</summary>
    private async Task<UpdateOutcome> RestoreAndFinalizeAsync(UpdateRecord startRecord, string lockName, string containerName,
        string priorName, string originalId, UpdateErrorCode failureCode)
    {
        string? restoreFailure;
        try
        {
            restoreFailure = await AttemptRestoreAsync(containerName, priorName, originalId);
        }
        catch (DockerRuntimeException ex)
        {
            restoreFailure = $"Restoring '{containerName}' from its prior container '{priorName}' could not proceed: {ex.Message}";
        }

        if (restoreFailure == null)
        {
            return await FinalizeAsync(startRecord, lockName, UpdateOutcome.RestoredAfterUnhealthy, failureCode);
        }

        await Console.Error.WriteLineAsync(restoreFailure);
        return await FinalizeAsync(startRecord, lockName, UpdateOutcome.RestoreFailed, UpdateErrorCode.RestoreFailed);
    }

    /// <summary>Returns the prior container to <paramref name="containerName"/> and starts it (S4.2, S4.8), or
    /// returns why it could not (null on success). Removes whatever replacement occupies that name first — but
    /// only once the prior container is known to exist, so a restore that cannot succeed never takes down the
    /// one container still standing. Never reconstructs the prior container from stored inspection data
    /// (S4.8) — only an actual rename of the retained container counts, so a prior container removed out from
    /// under this update fails with <see cref="UpdateOutcome.RestoreFailed"/> rather than approximating one.</summary>
    private async Task<string?> AttemptRestoreAsync(string containerName, string priorName, string originalId)
    {
        var current = await _runtime.InspectAsync(containerName);
        if (current != null && current.Id == originalId)
        {
            // The swap failed before the target was renamed (stop or rename rejected): the original still holds
            // its own name, so restoring it is starting it again — never removing it.
            try
            {
                await _runtime.StartAsync(containerName);
                return null;
            }
            catch (DockerRuntimeException ex)
            {
                return $"The original container '{containerName}' could not be started again: {ex.Message}";
            }
        }

        var prior = await _runtime.InspectAsync(priorName);
        if (prior == null)
        {
            return current == null
                ? $"The prior container '{priorName}' is no longer present; restore cannot proceed."
                : $"The prior container '{priorName}' is no longer present; restore cannot proceed, and the " +
                  $"replacement '{containerName}' is left in place.";
        }

        var priorDescription = $"The prior container '{priorName}' (labels: {FormatLabels(prior.Labels)})";
        if (current != null)
        {
            try
            {
                await _runtime.RemoveAsync(containerName, force: true);
            }
            catch (DockerRuntimeException ex)
            {
                return $"{priorDescription} could not be restored: the replacement '{containerName}' could not be removed to make way for it: {ex.Message}";
            }
        }

        try
        {
            await _runtime.RenameAsync(priorName, containerName);
        }
        catch (DockerRuntimeException ex)
        {
            return $"{priorDescription} could not be renamed back to '{containerName}': {ex.Message}";
        }

        try
        {
            await _runtime.StartAsync(containerName);
        }
        catch (DockerRuntimeException ex)
        {
            // S4.9: put it back under its prior name, so the next update refuses on it rather than updating over
            // a stopped prior container it would take for an ordinary target.
            try
            {
                await _runtime.RenameAsync(containerName, priorName);
            }
            catch (DockerRuntimeException)
            {
            }

            return $"{priorDescription} could not be started as '{containerName}': {ex.Message}";
        }

        return null;
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
