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

    [Theory]
    [InlineData("v01.2.3")]
    [InlineData("+1.2.3")]
    [InlineData("1. 2.3")]
    [InlineData("2.0.0+sha.5114f85")]
    [InlineData("2.0.0-beta+sha.5114f85")]
    [InlineData("2.0.0-")]
    [InlineData("2.0.0-beta..1")]
    public void Parse_RejectsNonCanonicalInput(string value)
    {
        Assert.Throws<FormatException>(() => ReleaseVersion.Parse(value));
    }

    [Theory]
    [InlineData("v0.10.0")]
    [InlineData("2.0.0-rc-1.2")]
    public void Parse_RoundTripsCanonicalInput(string value)
    {
        var version = ReleaseVersion.Parse(value);

        Assert.Equal(value.TrimStart('v'), version.ToPackageString());
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
