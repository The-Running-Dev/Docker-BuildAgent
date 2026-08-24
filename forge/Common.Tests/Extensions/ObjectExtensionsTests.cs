using Xunit;

using Extensions;

namespace Common.Tests.Extensions;
public class ObjectExtensionsTests
{
    private class Source
    {
        public string Name { get; set; } = "value";
        public int Count { get; set; } = 2;
        public string RegistryToken { get; set; } = "secret-token";
        public string WebhookUrl { get; set; } = "https://hooks.example.com/abc";
        public string Value { get; set; } = "mysecret";
        public string[] StripForDisplay { get; set; } = ["secret"];
    }

    private class Target
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    [Fact]
    public void CopyTo_CopiesMatchingProperties()
    {
        var source = new Source { Name = "copied", Count = 5 };

        var result = source.CopyTo<Target>();

        Assert.Equal("copied", result.Name);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void ToDisplayString_ExcludesSensitivePropertiesAndStripsWebhookAndPatterns()
    {
        var source = new Source();

        var display = source.ToDisplayString();

        Assert.DoesNotContain("RegistryToken", display);
        Assert.Contains("https://hooks.example.com...", display);
        Assert.Contains("[CONFIG] Value: my", display);
    }
}
