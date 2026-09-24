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

    // Out of scope, S2 — S4 owns health/cleanup, so a success still leaves the prior container and pin,
    // and a second update refuses with ResiduePresent (disclosed boundary interpretation).
    [Fact]
    public async Task Success_LeavesResidueThatRefusesTheNextUpdate()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.SeedImage("web:3.0", "sha256:newer");

        await _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true));

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:3.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));
        Assert.Equal(UpdateErrorCode.ResiduePresent, ex.Code);
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

    // I37: a failure past the target's first change (stop/rename/create/start) does not release the lock and
    // writes no outcome — S2's own out-of-scope boundary for restore/recovery of this path (disclosed).
    [Fact]
    public async Task ReplacementCreateFailed_DoesNotReleaseTheLock_AndWritesNoOutcome()
    {
        var target = Target("web", "sha256:old", "web:1.0");
        SeedTargetAndImages(target, "sha256:new", "web:2.0");
        _runtime.FailCreateReplacement = name => name == "web";

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => _updater.RunAsync("web", "web:2.0", new UpdateOptions(TimeSpan.FromSeconds(30), true)));

        Assert.Equal(UpdateErrorCode.ReplacementCreateFailed, ex.Code);
        Assert.NotNull(_runtime.Get(UpdateNaming.LockContainerName("web")));

        var state = _log.ReadState();
        Assert.False(state.IsCorrupt);
        var open = Assert.Single(state.OpenRecords);
        Assert.Equal("web", open.ContainerName);
        Assert.False(open.HasOutcome);
    }
}
