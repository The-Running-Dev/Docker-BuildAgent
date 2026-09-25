#nullable enable

using System;
using Update;
using Xunit;

namespace Update.Tests;

public sealed class UpdateCommandLineTests
{
    [Fact]
    public void Parse_BareContainerName_UsesDefaults()
    {
        var command = Assert.IsType<UpdateCommand.Run>(UpdateCommandLine.Parse(new[] { "web" }));
        Assert.Equal("web", command.ContainerName);
        Assert.Null(command.ImageReference);
        Assert.Equal(UpdateCommandLine.DefaultHealthTimeout, command.HealthTimeout);
        Assert.True(command.RestoreOnFailure);
        Assert.Null(command.NotifyUrl);
    }

    [Fact]
    public void Parse_AllOptions_AreCaptured()
    {
        var command = Assert.IsType<UpdateCommand.Run>(UpdateCommandLine.Parse(new[]
        {
            "web", "--image", "web:2.0", "--health-timeout", "45s", "--no-restore", "--notify", "https://example/hook",
        }));

        Assert.Equal("web", command.ContainerName);
        Assert.Equal("web:2.0", command.ImageReference);
        Assert.Equal(TimeSpan.FromSeconds(45), command.HealthTimeout);
        Assert.False(command.RestoreOnFailure);
        Assert.Equal("https://example/hook", command.NotifyUrl);
    }

    [Theory]
    [InlineData("120", 120)]
    [InlineData("120s", 120)]
    [InlineData("2m", 120)]
    [InlineData("1h", 3600)]
    public void Parse_HealthTimeout_AcceptsBareOrSuffixedDurations(string value, int expectedSeconds)
    {
        var command = Assert.IsType<UpdateCommand.Run>(
            UpdateCommandLine.Parse(new[] { "web", "--health-timeout", value }));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), command.HealthTimeout);
    }

    [Fact]
    public void Parse_InvalidDuration_Throws()
    {
        Assert.Throws<UpdateCommandLineException>(
            () => UpdateCommandLine.Parse(new[] { "web", "--health-timeout", "soon" }));
    }

    // S4.7: a value of zero or less refuses rather than waiting indefinitely.
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void Parse_HealthTimeoutNotPositive_Throws(string value)
    {
        Assert.Throws<UpdateCommandLineException>(
            () => UpdateCommandLine.Parse(new[] { "web", "--health-timeout", value }));
    }

    [Fact]
    public void Parse_ClearLock_TakesJustTheContainerName()
    {
        var command = Assert.IsType<UpdateCommand.ClearLock>(UpdateCommandLine.Parse(new[] { "--clear-lock", "web" }));
        Assert.Equal("web", command.ContainerName);
    }

    [Fact]
    public void Parse_ClearLockWithAnotherOption_Throws()
    {
        Assert.Throws<UpdateCommandLineException>(
            () => UpdateCommandLine.Parse(new[] { "--clear-lock", "web", "--image", "web:2.0" }));
    }

    [Fact]
    public void Parse_UnrecognizedOption_Throws()
    {
        Assert.Throws<UpdateCommandLineException>(() => UpdateCommandLine.Parse(new[] { "web", "--bogus" }));
    }

    [Fact]
    public void Parse_MissingContainerName_Throws()
    {
        Assert.Throws<UpdateCommandLineException>(() => UpdateCommandLine.Parse(new[] { "--image", "web:2.0" }));
    }

    [Fact]
    public void Parse_TwoPositionalArguments_Throws()
    {
        Assert.Throws<UpdateCommandLineException>(() => UpdateCommandLine.Parse(new[] { "web", "db" }));
    }

    [Fact]
    public void Parse_OptionMissingItsValue_Throws()
    {
        Assert.Throws<UpdateCommandLineException>(() => UpdateCommandLine.Parse(new[] { "web", "--image" }));
    }

    [Theory]
    [InlineData(UpdateOutcome.Succeeded, 0)]
    [InlineData(UpdateOutcome.AlreadyCurrent, 0)]
    [InlineData(UpdateOutcome.RestoredAfterUnhealthy, 10)]
    [InlineData(UpdateOutcome.UnhealthyNotRestored, 11)]
    [InlineData(UpdateOutcome.RestoreFailed, 12)]
    [InlineData(UpdateOutcome.Refused, 1)]
    public void ForOutcome_MatchesExitStatusTable(UpdateOutcome outcome, int expected)
    {
        Assert.Equal(expected, UpdateExitCode.ForOutcome(outcome));
    }

    [Theory]
    [InlineData(UpdateErrorCode.TargetNotFound, 20)]
    [InlineData(UpdateErrorCode.TargetAutoRemove, 20)]
    [InlineData(UpdateErrorCode.TargetOrchestratorManaged, 20)]
    [InlineData(UpdateErrorCode.TargetShapeUnsupported, 20)]
    [InlineData(UpdateErrorCode.LockHeld, 21)]
    [InlineData(UpdateErrorCode.ResiduePresent, 22)]
    [InlineData(UpdateErrorCode.PinFailed, 23)]
    [InlineData(UpdateErrorCode.LogUnwritable, 24)]
    [InlineData(UpdateErrorCode.ImageUnavailable, 30)]
    [InlineData(UpdateErrorCode.ReplacementCreateFailed, 1)]
    public void ForError_MatchesExitStatusTable(UpdateErrorCode code, int expected)
    {
        Assert.Equal(expected, UpdateExitCode.ForError(code));
    }
}
