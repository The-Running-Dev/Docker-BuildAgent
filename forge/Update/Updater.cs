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
}

public sealed class SystemUpdateClock : IUpdateClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Who is running this update, recorded on the lock so a later refusal can name an owner (S2.5).</summary>
public sealed record UpdaterIdentity(string OwnerHost, int OwnerProcessId)
{
    public static UpdaterIdentity Current() => new(Environment.MachineName, Environment.ProcessId);
}

/// <summary>
/// Moves a running container to a new image, or refuses without touching it (design/30-slices.md § S2).
///
/// Scope note: health verification, restore and the restore exit statuses are S4's (Out of scope, S2). This
/// orchestrator therefore ends the swap at "the replacement is running" and records <see
/// cref="UpdateOutcome.Succeeded"/> immediately — it never waits on health and never restores. That leaves the
/// prior container and its image pin in place after a success, which is why an immediate second update of the
/// same container refuses with <see cref="UpdateErrorCode.ResiduePresent"/> (S2's own out-of-scope note:
/// "until [S4] lands a failure mid-swap leaves residue that the next update refuses on" — true here even for a
/// success, since S2 never reaches the health-gated cleanup step that would remove the prior container).
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
            // S2 leaves whatever residue results for the next update to refuse on (Out of scope, S2 — S4 owns
            // restore) and, per I37, does not release the lock without attempting the outcome write, which a
            // failure here never reaches.
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
                throw new UpdateException(UpdateErrorCode.ReplacementCreateFailed, containerName,
                    $"The replacement for '{containerName}' could not be created: {ex.Message}", ex);
            }

            var completedRecord = startRecord with { CompletedAt = _clock.UtcNow, Outcome = UpdateOutcome.Succeeded };
            _log.Append(completedRecord);
            await TryRemoveLockAsync(lockName);
            return UpdateOutcome.Succeeded;
        }
        catch (UpdateException ex) when (ex.Code is UpdateErrorCode.ResiduePresent or UpdateErrorCode.PinFailed or UpdateErrorCode.LogUnwritable)
        {
            await TryRemoveLockAsync(lockName);
            throw;
        }
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

    private async Task TryRemoveLockAsync(string lockName)
    {
        try
        {
            await _runtime.RemoveAsync(lockName, force: true);
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
