#nullable enable

using System;
using System.IO;
using System.Linq;
using Update;
using Xunit;

namespace Update.Tests;

public sealed class UpdateLogStoreTests : IDisposable
{
    private readonly string _path;
    private readonly UpdateLogStore _store;

    public UpdateLogStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"updates-{Guid.NewGuid():N}.jsonl");
        _store = new UpdateLogStore(_path);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static UpdateRecord StartRecord(Guid updateId, string containerName) => new(
        RecordSchemaVersion: 1,
        UpdateId: updateId,
        ContainerName: containerName,
        StartedAt: DateTimeOffset.UtcNow,
        PriorImageId: "sha256:old",
        TargetImageReference: "web:new",
        Options: new UpdateOptions(TimeSpan.FromSeconds(30), true),
        CompletedAt: null,
        Outcome: null,
        FailureCode: null);

    [Fact]
    public void ReadState_OnMissingFile_IsOkWithNoOpenRecords()
    {
        var state = _store.ReadState();
        Assert.False(state.IsCorrupt);
        Assert.Empty(state.OpenRecords);
    }

    [Fact]
    public void Append_WithoutOutcome_IsAnOpenRecord()
    {
        var id = Guid.NewGuid();
        _store.Append(StartRecord(id, "web"));

        var state = _store.ReadState();
        Assert.False(state.IsCorrupt);
        var open = Assert.Single(state.OpenRecords);
        Assert.Equal(id, open.UpdateId);
        Assert.Equal("web", open.ContainerName);
        Assert.False(open.HasOutcome);
    }

    [Fact]
    public void Append_LaterLineForSameUpdateId_WinsOverEarlier()
    {
        var id = Guid.NewGuid();
        _store.Append(StartRecord(id, "web"));
        var completed = StartRecord(id, "web") with { CompletedAt = DateTimeOffset.UtcNow, Outcome = UpdateOutcome.Succeeded };
        _store.Append(completed);

        var state = _store.ReadState();
        Assert.False(state.IsCorrupt);
        Assert.Empty(state.OpenRecords);
    }

    [Fact]
    public void ReadState_IgnoresBlankLines()
    {
        _store.Append(StartRecord(Guid.NewGuid(), "web"));
        File.AppendAllText(_path, "\n");

        var state = _store.ReadState();
        Assert.False(state.IsCorrupt);
    }

    [Fact]
    public void ReadState_OnUnparseableLine_IsCorruptAtThatLineNumber()
    {
        _store.Append(StartRecord(Guid.NewGuid(), "web"));
        File.AppendAllText(_path, "not json\n");

        var state = _store.ReadState();
        Assert.True(state.IsCorrupt);
        Assert.Equal(2, state.CorruptLineNumber);
    }

    [Fact]
    public void ReadState_OnLineMissingUpdateId_IsCorrupt()
    {
        File.WriteAllText(_path, "{\"containerName\":\"web\"}\n");

        var state = _store.ReadState();
        Assert.True(state.IsCorrupt);
        Assert.Equal(1, state.CorruptLineNumber);
    }

    [Fact]
    public void ReadState_OnRecordWithUnknownSchemaVersion_StillReadsTheThreeFields()
    {
        File.WriteAllText(_path,
            "{\"recordSchemaVersion\":99,\"updateId\":\"" + Guid.NewGuid() + "\",\"containerName\":\"web\"}\n");

        var state = _store.ReadState();
        Assert.False(state.IsCorrupt);
        var open = Assert.Single(state.OpenRecords);
        Assert.Equal("web", open.ContainerName);
    }

    [Fact]
    public void Path_ReflectsOverride()
    {
        Assert.Equal(_path, _store.Path);
    }

    [Fact]
    public void ResolveDefaultPath_EndsWithUpdatesJsonl()
    {
        Assert.EndsWith("updates.jsonl", UpdateLogStore.ResolveDefaultPath());
    }
}
