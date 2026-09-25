using System;

using Xunit;

namespace Release.Tests;

public sealed class ReleaseVersionTests
{
    [Fact]
    public void Parse_AcceptsTagForm()
    {
        var version = ReleaseVersion.Parse("v2.0.0");

        Assert.Equal(new ReleaseVersion(2, 0, 0, null), version);
    }

    [Fact]
    public void Parse_AcceptsPackageForm()
    {
        var version = ReleaseVersion.Parse("2.0.0");

        Assert.Equal(new ReleaseVersion(2, 0, 0, null), version);
    }

    [Fact]
    public void Parse_AcceptsPreRelease()
    {
        var version = ReleaseVersion.Parse("v2.0.0-beta.1");

        Assert.Equal(new ReleaseVersion(2, 0, 0, "beta.1"), version);
    }

    [Fact]
    public void Parse_RejectsMalformedInput()
    {
        Assert.Throws<FormatException>(() => ReleaseVersion.Parse("not-a-version"));
    }

    [Fact]
    public void ToTagString_PrependsV()
    {
        Assert.Equal("v2.0.0", new ReleaseVersion(2, 0, 0, null).ToTagString());
    }

    [Fact]
    public void ToPackageString_OmitsV()
    {
        Assert.Equal("2.0.0", new ReleaseVersion(2, 0, 0, null).ToPackageString());
    }
}
