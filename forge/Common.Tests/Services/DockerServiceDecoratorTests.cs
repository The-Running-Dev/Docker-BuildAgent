using Moq;
using Xunit;
using System.Reflection;
using Microsoft.Extensions.Logging;

using Services;

namespace Common.Tests.Services;

public class DockerServiceDecoratorTests
{
    [Fact]
    public void Constructor_Throws_WhenServiceProviderNull()
    {
        var logger = new Mock<ILogger<DockerServiceDecorator>>();

        Assert.Throws<ArgumentNullException>(() => new DockerServiceDecorator(null!, logger.Object));
    }

    [Fact]
    public void Constructor_Throws_WhenLoggerNull()
    {
        var provider = new Mock<IServiceProvider>();

        Assert.Throws<ArgumentNullException>(() => new DockerServiceDecorator(provider.Object, null!));
    }

    [Fact]
    public void DetectDryRunMode_DefaultsFalse_WhenNoIndicators()
    {
        var provider = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<DockerServiceDecorator>>();
        var decorator = new DockerServiceDecorator(provider.Object, logger.Object);

        var method = typeof(DockerServiceDecorator).GetMethod("DetectDryRunMode", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = (bool)method!.Invoke(decorator, null)!;

        Assert.False(result);
    }
}
