#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Update;
using Xunit;

namespace Update.Tests;

public sealed class UpdaterTests : IDisposable
{
    private readonly string _logPath;
    private readonly UpdateLogStore _log;
    private readonly FakeDockerRuntime _runtime;
    private readonly FakeUpdateClock _clock;
    private readonly UpdaterIdentity _identity;
    private readonly Updater _updater;

    public UpdaterTests()
    {
        _logPath = Path.Combine(Path.GetTempPath(), $"updates-{Guid.NewGuid():N}.jsonl");
        _log = new UpdateLogStore(_logPath);
        _runtime = new FakeDockerRuntime();
        _clock = new FakeUpdateClock();
        _identity = new UpdaterIdentity("test-host", 4321);
        _updater = new Updater(_runtime, _log, _clock, _identity);
    }

    public void Dispose()
    {
        if (File.Exists(_logPath))
        {
            File.Delete(_logPath);
        }
    }

    private static ContainerInspection Target(
        string name,
        string imageId,
        string imageReference,
        IReadOnlyDictionary<string, string>? labels = null,
        IReadOnlyList<MountSpec>? mounts = null,
        IReadOnlyList<string>? links = null,
        bool autoRemove = false,
        IReadOnlyList<string>? env = null) => new(
        Id: Guid.NewGuid().ToString("N"),
        Name: name,
        ImageId: imageId,
        ImageReference: imageReference,
        Running: true,
        AutoRemove: autoRemove,
        Labels: labels ?? new Dictionary<string, string> { ["com.example.owner"] = "team-a" },
        Env: env ?? new[] { "FOO=bar" },
        Command: Array.Empty<string>(),
        Entrypoint: Array.Empty<string>(),
        Mounts: mounts ?? Array.Empty<MountSpec>(),
        Ports: Array.Empty<PortSpec>(),
        RestartPolicy: "unless-stopped",
        Networks: new[] { "bridge" },
        Links: links ?? Array.Empty<string>());

    private void SeedTargetAndImages(ContainerInspection target, string newImageId, string newImageReference)
    {
        _runtime.SeedContainer(target);
        _runtime.SeedImage(target.ImageReference!, target.ImageId, local: true);
        _runtime.SeedImage(newImageReference, newImageId, local: true);
    }

    // S2.21
    [Fact]
    public async Task TargetNotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("ghost", null, new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.TargetNotFound, ex.Code);
    }

    [Fact]
    public async Task TargetAutoRemove_Throws()
    {
        var target = Target("web", "sha256:old", "web:1.0", autoRemove: true);
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.TargetAutoRemove, ex.Code);
    }

    [Theory]
    [InlineData("com.docker.compose.project")]
    [InlineData("com.docker.swarm.service.id")]
    [InlineData("com.docker.stack.namespace")]
    [InlineData("io.kubernetes.pod.name")]
    public async Task TargetOrchestratorManaged_Throws(string orchestratorLabelKey)
    {
        var labels = new Dictionary<string, string> { [orchestratorLabelKey] = "x" };
        var target = Target("web", "sha256:old", "web:1.0", labels: labels);
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.TargetOrchestratorManaged, ex.Code);
    }

    // S2.8
    [Fact]
    public async Task TargetShapeUnsupported_LegacyLink_Throws()
    {
        var target = Target("web", "sha256:old", "web:1.0", links: new[] { "db:db" });
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.TargetShapeUnsupported, ex.Code);
    }

    // S2.8
    [Fact]
    public async Task TargetShapeUnsupported_AnonymousVolume_Throws()
    {
        var anonymousMount = new MountSpec(MountKind.Volume, Source: "a1b2c3", Destination: "/data", ReadOnly: false, IsAnonymousVolume: true);
        var target = Target("web", "sha256:old", "web:1.0", mounts: new[] { anonymousMount });
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.TargetShapeUnsupported, ex.Code);
    }

    // S2.4
    [Fact]
    public async Task ImageUnavailable_WhenNoReferenceCanBeResolved()
    {
        var target = Target("web", "sha256:old", imageReference: null!);
        _runtime.SeedContainer(target);

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", null, new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.ImageUnavailable, ex.Code);
    }

    [Fact]
    public async Task ImageUnavailable_WhenRegistryUnreachableForANonLocalImage()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        _runtime.SeedContainer(target);
        _runtime.SeedImage("web:1.0", "sha256:old");
        _runtime.SeedImage("web:2.0", "sha256:new", local: false);
        _runtime.RegistryReachable = false;

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.ImageUnavailable, ex.Code);
    }

    [Fact]
    public async Task AlreadyCurrent_WhenResolvedImageMatchesTarget()
    {
        var target = Target("web", "sha256:same", "web:1.0");
        _runtime.SeedContainer(target);
        _runtime.SeedImage("web:1.0", "sha256:same");

        var outcome = await _updater.RunAsync("web", "web:1.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));
        Assert.Equal(UpdateOutcome.AlreadyCurrent, outcome);
    }

    // S2.3: the lock's own image is the target's already-local image, so acquiring it pulls nothing; the
    // registry is unreachable here and the update still proceeds because the *target* image is requested
    // and is already local.
    [Fact]
    public async Task Success_WhenReplacementImageAlreadyLocal_NeedsNoRegistry()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.RegistryReachable = false;

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));
        Assert.Equal(UpdateOutcome.Succeeded, outcome);
    }

    [Fact]
    public async Task Success_ReplacementRunsUnderOriginalName_WithNewImage()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        var replacement = _runtime.Get("web");
        Assert.NotNull(replacement);
        Assert.True(replacement!.Running);
        Assert.Equal("sha256:new", replacement.ImageId);
    }

    // I34: configuration reproduced exactly, buildagent-owned labels excluded.
    [Fact]
    public async Task Success_ReplacementReproducesConfiguration_ExceptBuildagentLabels()
    {
        var labels = new Dictionary<string, string> { ["com.example.owner"] = "team-a", ["com.buildagent.role"] = "should-not-copy" };
        var target = Target("web", "sha256:old", "web:1.0", labels: labels, env: new[] { "FOO=bar", "BAZ=qux" });
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        var replacement = _runtime.Get("web")!;
        Assert.Equal("team-a", replacement.Labels["com.example.owner"]);
        Assert.False(replacement.Labels.ContainsKey("com.buildagent.role"));
        Assert.Equal(new[] { "FOO=bar", "BAZ=qux" }, replacement.Env);
        Assert.Equal("unless-stopped", replacement.RestartPolicy);
        Assert.Equal(new[] { "bridge" }, replacement.Networks);
    }

    [Fact]
    public async Task Success_RemovesTheLockItCreated()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
    }

    // S4.1: a healthy replacement's prior container is removed, closing the gap S2 left open — a second
    // update of the same container no longer refuses with ResiduePresent.
    [Fact]
    public async Task Success_RemovesThePriorContainer_SoASecondUpdateNoLongerRefusesWithResidue()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.SeedImage("web:3.0", "sha256:newer");

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));
        Assert.Equal(UpdateOutcome.Succeeded, outcome);
        Assert.Null(_runtime.Get(UpdateNaming.PriorContainerName("web")));

        var secondOutcome = await _updater.RunAsync("web", "web:3.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));
        Assert.Equal(UpdateOutcome.Succeeded, secondOutcome);
    }

    // S2.5 / I32: lock message names owner host, pid and age; a passed deadline is diagnostic, not an authorization.
    [Fact]
    public async Task LockHeld_MessageNamesOwnerAndAge_AndNotesPassedDeadlineIsNotAnAuthorization()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        var lockLabels = new Dictionary<string, string>
        {
            [UpdateNaming.LabelRole] = UpdateNaming.RoleLock,
            [UpdateNaming.LabelContainer] = "web",
            [UpdateNaming.LabelUpdateId] = Guid.NewGuid().ToString(),
            ["com.buildagent.owner-host"] = "other-host",
            ["com.buildagent.owner-pid"] = "999",
            ["com.buildagent.created-at"] = _clock.UtcNow.AddMinutes(-5).ToString("O"),
            ["com.buildagent.deadline"] = _clock.UtcNow.AddMinutes(-1).ToString("O"),
        };
        await _runtime.CreateMarkerAsync(UpdateNaming.LockContainerName("web"), target.ImageId, lockLabels);

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.LockHeld, ex.Code);
        Assert.Contains("other-host", ex.Message);
        Assert.Contains("999", ex.Message);
        Assert.Contains("5m", ex.Message);
        Assert.Contains("does not authorise", ex.Message);
        // S5.4: a refusal naming a lock names the command that clears it.
        Assert.Contains("--clear-lock web", ex.Message);
    }

    // S2.2: the lock is checked by name, before the target is ever inspected — it survives the target
    // having moved to a different name (or being gone entirely) since the lock was taken.
    [Fact]
    public async Task LockHeld_SurvivesEvenWhenTargetNoLongerExistsUnderThatName()
    {
        var lockLabels = new Dictionary<string, string>
        {
            ["com.buildagent.owner-host"] = "other-host",
            ["com.buildagent.owner-pid"] = "999",
        };
        await _runtime.CreateMarkerAsync(UpdateNaming.LockContainerName("web"), "sha256:whatever", lockLabels);
        // Deliberately: no container named "web" is seeded at all.

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.LockHeld, ex.Code);
    }

    // S5.1: reports the lock's owning host, process, update id and acquisition time.
    [Fact]
    public async Task ClearLockAsync_RemovesTheLockAndReportsWhoHeldIt()
    {
        var updateId = Guid.NewGuid();
        var createdAt = _clock.UtcNow.AddMinutes(-5);
        var lockLabels = new Dictionary<string, string>
        {
            [UpdateNaming.LabelRole] = UpdateNaming.RoleLock,
            [UpdateNaming.LabelContainer] = "web",
            [UpdateNaming.LabelUpdateId] = updateId.ToString(),
            [UpdateNaming.LabelOwnerHost] = "other-host",
            [UpdateNaming.LabelOwnerPid] = "999",
            [UpdateNaming.LabelCreatedAt] = createdAt.ToString("O"),
            [UpdateNaming.LabelDeadline] = _clock.UtcNow.AddMinutes(30).ToString("O"),
        };
        await _runtime.CreateMarkerAsync(UpdateNaming.LockContainerName("web"), "sha256:whatever", lockLabels);

        var cleared = await _updater.ClearLockAsync("web");

        Assert.NotNull(cleared);
        Assert.Equal(updateId, cleared!.UpdateId);
        Assert.Equal("other-host", cleared.OwnerHost);
        Assert.Equal(999, cleared.OwnerProcessId);
        Assert.Equal(createdAt, cleared.CreatedAt);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
    }

    // S5.2: leaves the target, the prior container, the prior image pin and the update log untouched.
    [Fact]
    public async Task ClearLockAsync_LeavesTargetPriorAndLogUntouched()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        var priorName = UpdateNaming.PriorContainerName("web");
        _runtime.SeedContainer(Target(priorName, "sha256:ancient", "web:0.9"));
        var pinTag = UpdateNaming.PriorImageTag("web");
        await _runtime.TagImageAsync("sha256:ancient", pinTag);
        _log.Append(new UpdateRecord(1, Guid.NewGuid(), "web", DateTimeOffset.UtcNow, "sha256:ancient", "web:0.9",
            new UpdateOptions(TimeSpan.FromSeconds(30), true), null, null, null));

        var lockLabels = new Dictionary<string, string>
        {
            [UpdateNaming.LabelOwnerHost] = "other-host",
            [UpdateNaming.LabelOwnerPid] = "999",
        };
        await _runtime.CreateMarkerAsync(UpdateNaming.LockContainerName("web"), "sha256:whatever", lockLabels);

        await _updater.ClearLockAsync("web");

        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
        Assert.NotNull(_runtime.Get("web"));
        Assert.NotNull(_runtime.Get(priorName));
        Assert.True(_runtime.HasImageTag(pinTag));
        Assert.Single(File.ReadAllLines(_logPath));
    }

    // S5.2: takes no other action when there is nothing to clear.
    [Fact]
    public async Task ClearLockAsync_ReturnsNullWhenNoLockIsHeld()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        var cleared = await _updater.ClearLockAsync("web");

        Assert.Null(cleared);
        Assert.NotNull(_runtime.Get("web"));
    }

    // S5.3: it is the only operator action that removes a lock it does not own — no ownership check applies.
    [Fact]
    public async Task ClearLockAsync_RemovesALockOwnedByAnotherHostWithNoOwnershipCheck()
    {
        var lockLabels = new Dictionary<string, string>
        {
            [UpdateNaming.LabelOwnerHost] = "some-other-host",
            [UpdateNaming.LabelOwnerPid] = "12345",
        };
        await _runtime.CreateMarkerAsync(UpdateNaming.LockContainerName("web"), "sha256:whatever", lockLabels);

        var cleared = await _updater.ClearLockAsync("web");

        Assert.NotNull(cleared);
        Assert.Equal("some-other-host", cleared!.OwnerHost);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
    }

    // Simulates the daemon's atomic name-uniqueness: another updater's CreateMarkerAsync wins the race
    // after this updater's own initial by-name check found nothing.
    private sealed class RacingDockerRuntime : IDockerRuntime
    {
        private readonly FakeDockerRuntime _inner;
        private readonly string _lockName;
        private readonly IReadOnlyDictionary<string, string> _racerLabels;
        private bool _raced;

        public RacingDockerRuntime(FakeDockerRuntime inner, string lockName, IReadOnlyDictionary<string, string> racerLabels)
        {
            _inner = inner;
            _lockName = lockName;
            _racerLabels = racerLabels;
        }

        public Task<ContainerInspection?> InspectAsync(string name) => _inner.InspectAsync(name);
        public Task<string> ResolveImageIdAsync(string imageReference) => _inner.ResolveImageIdAsync(imageReference);

        public async Task<string> CreateMarkerAsync(string name, string imageId, IReadOnlyDictionary<string, string> labels)
        {
            if (name == _lockName && !_raced)
            {
                _raced = true;
                await _inner.CreateMarkerAsync(name, imageId, _racerLabels);
            }

            return await _inner.CreateMarkerAsync(name, imageId, labels);
        }

        public Task<string> CreateReplacementAsync(ContainerCreateSpec spec) => _inner.CreateReplacementAsync(spec);
        public Task StartAsync(string name) => _inner.StartAsync(name);
        public Task StopAsync(string name) => _inner.StopAsync(name);
        public Task RenameAsync(string currentName, string newName) => _inner.RenameAsync(currentName, newName);
        public Task RemoveAsync(string name, bool force) => _inner.RemoveAsync(name, force);
        public Task TagImageAsync(string imageId, string tag) => _inner.TagImageAsync(imageId, tag);
        public Task RemoveImageTagAsync(string tag) => _inner.RemoveImageTagAsync(tag);
    }

    // S2.1: concurrent update attempts on the same container — the daemon's name uniqueness lets only one
    // creator win the lock; the loser is refused, not left holding a half-made lock of its own.
    [Fact]
    public async Task ConcurrentLockAcquisition_LoserIsRefused_NotBothWinners()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        var racerLabels = new Dictionary<string, string>
        {
            ["com.buildagent.owner-host"] = "racer-host",
            ["com.buildagent.owner-pid"] = "1",
        };
        var racing = new RacingDockerRuntime(_runtime, UpdateNaming.LockContainerName("web"), racerLabels);
        var updater = new Updater(racing, _log, _clock, _identity);

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.LockHeld, ex.Code);
        Assert.Contains("racer-host", ex.Message);
    }

    // I41: a residue refusal (prior container by name) is before the target's first change; the lock this
    // attempt just took must not survive it.
    [Fact]
    public async Task ResiduePresent_PriorContainerByName_ReleasesTheLockItJustTook()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.SeedContainer(Target(UpdateNaming.PriorContainerName("web"), "sha256:ancient", "web:0.9"));

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.ResiduePresent, ex.Code);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
    }

    // I41: an open (outcome-less) log record for the same container is also residue, and also releases the lock.
    [Fact]
    public async Task ResiduePresent_OpenLogRecord_ReleasesTheLockItJustTook()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _log.Append(new UpdateRecord(1, Guid.NewGuid(), "web", DateTimeOffset.UtcNow, "sha256:ancient", "web:0.9",
            new UpdateOptions(TimeSpan.FromSeconds(30), true), null, null, null));

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.ResiduePresent, ex.Code);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
        // S2.9: names the open record's log path and the action that clears it, not just that one exists.
        Assert.Contains(_logPath, ex.Message);
        Assert.Contains("clear it", ex.Message);
    }

    // S2.19: a corrupt log refuses every update on the host, checked before anything else is touched.
    [Fact]
    public async Task CorruptLog_RefusesBeforeTouchingAnyContainer()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        File.WriteAllText(_logPath, "not json\n");

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.ResiduePresent, ex.Code);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
    }

    // I41: a pin failure is also before the target's first change; the lock is released and no dangling tag remains.
    [Fact]
    public async Task PinFailed_ReleasesTheLockItJustTook()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        var pinTag = UpdateNaming.PriorImageTag("web");
        _runtime.FailTagImage = tag => tag == pinTag;

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.PinFailed, ex.Code);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
        Assert.False(_runtime.HasImageTag(pinTag));
    }

    // I36/LogUnwritable: when the start entry cannot be durably written, the pin is rolled back and the lock released.
    [Fact]
    public async Task LogUnwritable_RollsBackPinAndReleasesTheLock()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");

        // A path that is itself an existing directory can never be opened as a log file.
        var unwritableDir = Path.Combine(Path.GetTempPath(), $"updates-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(unwritableDir);
        try
        {
            var unwritableLog = new UpdateLogStore(unwritableDir);
            var updater = new Updater(_runtime, unwritableLog, _clock, _identity);
            var pinTag = UpdateNaming.PriorImageTag("web");

            var ex = await Assert.ThrowsAsync<UpdateException>(
                () => updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
            Assert.Equal(UpdateErrorCode.LogUnwritable, ex.Code);
            Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
            Assert.False(_runtime.HasImageTag(pinTag));
        }
        finally
        {
            Directory.Delete(unwritableDir, recursive: true);
        }
    }

    // S4.10: a daemon rejection during creation restores unconditionally — no updated container exists to
    // keep — releasing the lock and writing the outcome record, rather than stranding the target (S2's
    // former out-of-scope boundary; closed by this slice).
    [Fact]
    public async Task ReplacementCreateFailed_RestoresUnconditionally_ReleasesLockAndWritesOutcome()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.FailCreateReplacement = name => name == "web";

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, outcome);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));

        var restored = _runtime.Get("web");
        Assert.NotNull(restored);
        Assert.True(restored!.Running);
        Assert.Equal("sha256:old", restored.ImageId);

        var state = _log.ReadState();
        Assert.False(state.IsCorrupt);
        Assert.Empty(state.OpenRecords);
    }

    // S4.2: a replacement that never reports healthy within the timeout restores the prior container and
    // exits with RestoredAfterUnhealthy; the running container's image id equals the pre-update image id.
    [Fact]
    public async Task HealthTimedOut_RestoresThePriorContainer_WithThePreUpdateImage()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.HealthProbe = name => name == "web" ? (true, "starting") : null;

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(2), true));

        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, outcome);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
        var restored = _runtime.Get("web");
        Assert.NotNull(restored);
        Assert.True(restored!.Running);
        Assert.Equal("sha256:old", restored.ImageId);
    }

    // S4.3: a target declaring no health check at all (a null HealthStatus) is treated as unhealthy and
    // restores, exactly like a timeout.
    [Fact]
    public async Task HealthCheckAbsent_TreatedAsUnhealthy_Restores()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.HealthProbe = name => name == "web" ? (true, null) : null;

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, outcome);
        Assert.Equal("sha256:old", _runtime.Get("web")!.ImageId);
    }

    // S4.4: a replacement that exits before becoming healthy fails with ReplacementExited and takes the
    // same restore path as a timeout.
    [Fact]
    public async Task ReplacementExited_Restores()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.HealthProbe = name => name == "web" ? (false, null) : null;

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, outcome);
        var restored = _runtime.Get("web");
        Assert.NotNull(restored);
        Assert.True(restored!.Running);
        Assert.Equal("sha256:old", restored.ImageId);
    }

    // S4.5/S4.6: with restoreOnFailure = false (--no-restore), an unhealthy replacement is left in place;
    // the prior container and its pin remain untouched, for manual recovery. Restoring is the default
    // behaviour exercised by every other health-failure test above, which all pass RestoreOnFailure: true.
    [Fact]
    public async Task NoRestore_LeavesUnhealthyReplacementInPlace()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.HealthProbe = name => name == "web" ? (true, "starting") : null;

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(2), false));

        Assert.Equal(UpdateOutcome.UnhealthyNotRestored, outcome);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));

        var replacement = _runtime.Get("web");
        Assert.NotNull(replacement);
        Assert.Equal("sha256:new", replacement!.ImageId);

        Assert.NotNull(_runtime.Get(UpdateNaming.PriorContainerName("web")));
        Assert.True(_runtime.HasImageTag(UpdateNaming.PriorImageTag("web")));
    }

    // S4.8/S4.9: removing the retained prior container before restore is attempted produces RestoreFailed
    // rather than reconstructing one from inspect output — restore only ever renames an actually-retained
    // container. The outcome is written and the lock released, per S4.9's general RestoreFailed contract.
    [Fact]
    public async Task RestoreFailed_WhenPriorContainerRemovedBeforeRestoreIsAttempted()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        var priorName = UpdateNaming.PriorContainerName("web");
        _runtime.FailCreateReplacement = name =>
        {
            if (name != "web")
            {
                return false;
            }

            // Simulates an operator removing the retained prior container out from under this update,
            // between the rename that retained it and the restore attempt that would otherwise use it.
            _runtime.RemoveAsync(priorName, force: true).GetAwaiter().GetResult();
            return true;
        };

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.RestoreFailed, outcome);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
        Assert.Null(_runtime.Get(priorName));
        Assert.Null(_runtime.Get("web"));

        var state = _log.ReadState();
        Assert.Empty(state.OpenRecords);
    }

    // S4.9: when restore itself fails to put the prior container back, the message names it and its
    // labels, the outcome is written, the lock is released, and the prior container is left in place so
    // the next update refuses.
    [Fact]
    public async Task RestoreFailed_WhenRenamingThePriorContainerBackFails_NamesItAndItsLabels()
    {
        var labels = new Dictionary<string, string> { ["com.example.owner"] = "team-a" };
        var target = Target("web", "sha256:old", "web:1.0", labels: labels);
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        var priorName = UpdateNaming.PriorContainerName("web");
        _runtime.FailCreateReplacement = name => name == "web";
        _runtime.FailRename = (current, next) => current == priorName && next == "web";

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.RestoreFailed, outcome);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));

        var prior = _runtime.Get(priorName);
        Assert.NotNull(prior);
        Assert.Equal("team-a", prior!.Labels["com.example.owner"]);

        var state = _log.ReadState();
        Assert.Empty(state.OpenRecords);
    }

    // Fires `onFirstDelay` the first time the health-wait loop awaits its poll interval, then behaves like
    // FakeUpdateClock — letting a test tamper with runtime or log state at the exact moment the loop is
    // about to poll again, a point otherwise unreachable from outside a single async call.
    private sealed class ClockWithFirstDelayHook : IUpdateClock
    {
        private readonly FakeUpdateClock _inner = new();
        private readonly Action _onFirstDelay;
        private bool _fired;

        public ClockWithFirstDelayHook(Action onFirstDelay)
        {
            _onFirstDelay = onFirstDelay;
        }

        public DateTimeOffset UtcNow => _inner.UtcNow;

        public Task Delay(TimeSpan delay)
        {
            if (!_fired)
            {
                _fired = true;
                _onFirstDelay();
            }

            return _inner.Delay(delay);
        }
    }

    // S4.11: a failure to write the outcome record does not change the exit status — it is reported on
    // stderr, and the stranded start record is what the next update's residue check (S2.9) detects.
    [Fact]
    public async Task FinalizeLogWriteFailure_DoesNotChangeOutcome_ReportsOnStderrAndLeavesResidue()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.HealthProbe = name => name == "web" ? (true, "starting") : null;

        var clock = new ClockWithFirstDelayHook(() => File.SetAttributes(_logPath, FileAttributes.ReadOnly));
        var updater = new Updater(_runtime, _log, clock, _identity);

        var originalError = Console.Error;
        var capturedError = new StringWriter();
        Console.SetError(capturedError);
        UpdateOutcome outcome;
        try
        {
            outcome = await updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(2), true));
        }
        finally
        {
            Console.SetError(originalError);
            File.SetAttributes(_logPath, FileAttributes.Normal);
        }

        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, outcome);
        Assert.Contains("could not be written", capturedError.ToString());

        var state = _log.ReadState();
        var open = Assert.Single(state.OpenRecords);
        Assert.Equal("web", open.ContainerName);
    }

    // A swap that fails before the target is renamed leaves the original under its own name: restore starts
    // it again rather than removing it as though it were a replacement.
    [Fact]
    public async Task SwapFailsBeforeRename_RestartsTheOriginal_NeverRemovesIt()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.FailRename = (current, next) => current == "web" && next == UpdateNaming.PriorContainerName("web");

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, outcome);
        var original = _runtime.Get("web");
        Assert.NotNull(original);
        Assert.Equal(target.Id, original!.Id);
        Assert.True(original.Running);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
    }

    // With the prior container gone, a restore that cannot succeed leaves the replacement standing rather than
    // removing the only container left.
    [Fact]
    public async Task RestoreFailed_PriorGone_LeavesTheReplacementInPlace()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        var priorName = UpdateNaming.PriorContainerName("web");
        _runtime.HealthProbe = name => name == "web" ? (true, "starting") : null;
        var clock = new ClockWithFirstDelayHook(() => _runtime.RemoveAsync(priorName, force: true).GetAwaiter().GetResult());
        var updater = new Updater(_runtime, _log, clock, _identity);

        var outcome = await updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(2), true));

        Assert.Equal(UpdateOutcome.RestoreFailed, outcome);
        var replacement = _runtime.Get("web");
        Assert.NotNull(replacement);
        Assert.Equal("sha256:new", replacement!.ImageId);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
    }

    // S4.9: a prior container renamed back but refusing to start is returned to its prior name, so the next
    // update still finds it rather than updating over it.
    [Fact]
    public async Task RestoreFailed_WhenThePriorContainerCannotStart_ReturnsItToItsPriorName()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        var priorName = UpdateNaming.PriorContainerName("web");
        _runtime.FailCreateReplacement = name => name == "web";
        _runtime.FailStart = name => name == "web";

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.RestoreFailed, outcome);
        var prior = _runtime.Get(priorName);
        Assert.NotNull(prior);
        Assert.Equal(target.Id, prior!.Id);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
        Assert.Empty(_log.ReadState().OpenRecords);
    }

    // A daemon error while polling health is not a verdict: the wait continues, and a replacement that then
    // reports healthy succeeds, with the lock released and the outcome written.
    [Fact]
    public async Task HealthPollDaemonError_KeepsWaiting_ThenSucceeds()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        var polls = 0;
        _runtime.HealthProbe = name =>
        {
            if (name != "web" || _runtime.Get(UpdateNaming.PriorContainerName("web")) == null)
            {
                return null;
            }

            return ++polls == 1 ? throw new DockerRuntimeException("simulated inspect failure") : (true, "healthy");
        };

        var outcome = await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        Assert.Equal(UpdateOutcome.Succeeded, outcome);
        Assert.Null(_runtime.Get(UpdateNaming.LockContainerName("web")));
        Assert.Empty(_log.ReadState().OpenRecords);
    }
}
