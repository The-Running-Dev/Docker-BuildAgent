#nullable enable

using System;

using Xunit;

namespace Common.Tests.Build;

/// <summary>
/// Unit tests for <see cref="BuildFailure"/>: a classified failure inside a target decides the build's
/// exit status, because NUKE's own status cannot carry it.
/// </summary>
[Collection(nameof(BuildFailureStateCollection))]
public class BuildFailureTests : IDisposable
{
    public BuildFailureTests()
    {
        BuildFailure.Reset();
    }

    public void Dispose()
    {
        BuildFailure.Reset();
    }

    [Fact]
    public void Resolve_WithNothingRecorded_ReturnsTheExecuteStatus()
    {
        Assert.Equal(0, BuildFailure.Resolve(0));
        Assert.Equal(-1, BuildFailure.Resolve(-1));
    }

    [Fact]
    public void Resolve_WhenTheBuildFailed_ReturnsTheRecordedStatus()
    {
        _ = new BuildFailureException(BuildFailure.Registry, "push failed");

        Assert.Equal(BuildFailure.Registry, BuildFailure.Resolve(-1));
    }

    [Fact]
    public void Resolve_WhenTheBuildSucceeded_IgnoresARecordedStatus()
    {
        BuildFailure.Record(BuildFailure.DockerDaemon);

        Assert.Equal(0, BuildFailure.Resolve(0));
    }

    [Fact]
    public void Record_KeepsTheFirstFailure()
    {
        BuildFailure.Record(BuildFailure.DockerDaemon);
        BuildFailure.Record(BuildFailure.Registry);

        Assert.Equal(BuildFailure.DockerDaemon, BuildFailure.Status);
    }

    [Fact]
    public void Statuses_MatchTheContract()
    {
        Assert.Equal(5, BuildFailure.DockerDaemon);
        Assert.Equal(6, BuildFailure.Registry);
    }
}

/// <summary>Serialises the test classes that touch the process-wide <see cref="BuildFailure"/> state.</summary>
[CollectionDefinition(nameof(BuildFailureStateCollection), DisableParallelization = true)]
public class BuildFailureStateCollection;
