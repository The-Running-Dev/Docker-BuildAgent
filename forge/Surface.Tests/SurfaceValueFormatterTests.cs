using Xunit;

namespace Surface.Tests;

public class SurfaceValueFormatterTests
{
    private sealed class Unformattable
    {
    }

    // S1.4: an unstringifiable item value raises DerivationFailed naming the item; no manifest is written.
    // No real *Params property hits this path today (every declared type formats successfully), so this
    // test exercises the formatter directly with a synthetic value the formatter has no convention for.
    [Fact]
    public void FormatParameterValue_WithUnformattableDefault_ThrowsDerivationFailedNamingTheItem()
    {
        var ex = Assert.Throws<SurfaceException>(() =>
            SurfaceValueFormatter.FormatParameterValue(typeof(Unformattable), new Unformattable(), "FakeParams", "Widget"));

        Assert.Equal(SurfaceErrorCode.DerivationFailed, ex.Code);
        Assert.Contains("FakeParams.Widget", ex.Message);
    }

    [Fact]
    public void FormatParameterValue_WithNullDefault_FormatsEmptyDefault()
    {
        var value = SurfaceValueFormatter.FormatParameterValue(typeof(string), null, "FakeParams", "Name");

        Assert.Equal("string|", value);
    }

    [Fact]
    public void FormatParameterValue_WithBoolDefault_FormatsLowercase()
    {
        var value = SurfaceValueFormatter.FormatParameterValue(typeof(bool), true, "FakeParams", "Flag");

        Assert.Equal("bool|true", value);
    }
}
