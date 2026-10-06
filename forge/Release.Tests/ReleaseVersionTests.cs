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

    // SemVer precedence, each pair lower first.
    [Theory]
    [InlineData("1.9.9", "2.0.0")]
    [InlineData("2.9.0", "2.10.0")]
    [InlineData("2.0.9", "2.0.10")]
    [InlineData("2.1.0-rc1", "2.1.0")]
    [InlineData("2.0.0", "2.1.0-rc1")]
    [InlineData("2.1.0-beta2", "2.1.0-rc1")]
    [InlineData("2.1.0-2", "2.1.0-10")]
    [InlineData("2.1.0-99", "2.1.0-rc")]
    [InlineData("2.1.0-rc", "2.1.0-rc.1")]
    [InlineData("2.1.0-rc.2", "2.1.0-rc.10")]
    public void CompareTo_OrdersBySemVerPrecedence(string lower, string higher)
    {
        var low = ReleaseVersion.Parse(lower);
        var high = ReleaseVersion.Parse(higher);

        Assert.True(low.CompareTo(high) < 0, $"{lower} should sort below {higher}");
        Assert.True(high.CompareTo(low) > 0, $"{higher} should sort above {lower}");
        Assert.Equal(0, low.CompareTo(ReleaseVersion.Parse(lower)));
    }

    [Fact]
    public void IsPreRelease_IsTrueOnlyWithALabel()
    {
        Assert.True(ReleaseVersion.Parse("2.1.0-rc1").IsPreRelease);
        Assert.False(ReleaseVersion.Parse("2.1.0").IsPreRelease);
    }
}
