using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

using Extensions;

namespace Common.Tests.Extensions;

public class LoggerExtensionsTests
{
    [Fact]
    public void Ok_LogsWithOkPrefix()
    {
        var logger = new Mock<ILogger<LoggerExtensionsTests>>();

        logger.Object.Ok("Hello");

        VerifyLog(logger, "[OK] Hello", LogLevel.Information);
    }

    [Fact]
    public void WithStatus_LogsWithCustomPrefix()
    {
        var logger = new Mock<ILogger<LoggerExtensionsTests>>();

        logger.Object.WithStatus("TEST", "Hello");

        VerifyLog(logger, "[TEST] Hello", LogLevel.Information);
    }

    [Fact]
    public void ErrorStatus_LogsError()
    {
        var logger = new Mock<ILogger<LoggerExtensionsTests>>();
        var ex = new InvalidOperationException("boom");

        logger.Object.ErrorStatus(ex, "Failed");

        VerifyLog(logger, "Failed", LogLevel.Error);
    }

    private static void VerifyLog(Mock<ILogger<LoggerExtensionsTests>> logger, string expectedMessage, LogLevel level)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
