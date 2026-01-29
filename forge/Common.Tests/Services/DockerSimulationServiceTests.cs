using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

using Services;

namespace Common.Tests.Services;

public class DockerSimulationServiceTests
{
    private readonly DockerSimulationService _service;

    public DockerSimulationServiceTests()
    {
        var logger = new Mock<ILogger<DockerSimulationService>>();
        var nodeService = new Mock<INodeService>();
        _service = new DockerSimulationService(logger.Object, nodeService.Object);
    }

    [Fact]
    public void Login_Throws_WhenParametersNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Login(null!));
    }

    [Fact]
    public void Build_Throws_WhenParametersNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Build(null!));
    }

    [Fact]
    public void Push_Throws_WhenParametersNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Push(null!));
    }

    [Fact]
    public void Tag_Throws_WhenParametersNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Tag(null!));
    }
}
