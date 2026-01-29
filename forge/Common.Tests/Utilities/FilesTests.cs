using Xunit;

using Utilities;

namespace Common.Tests.Utilities;

public class FilesTests : IDisposable
{
    private readonly string _rootDir;

    public FilesTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-files-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void Read_ReturnsEmpty_WhenFileMissing()
    {
        var result = Files.Read(Path.Combine(_rootDir, "missing.txt"));

        Assert.Empty(result);
    }

    [Fact]
    public void Read_TrimsAndSkipsEmptyLines()
    {
        var path = Path.Combine(_rootDir, "lines.txt");
        File.WriteAllLines(path, new[] { "  a  ", "", " b" });

        var result = Files.Read(path);

        Assert.Equal(new[] { "a", "b" }, result);
    }

    [Fact]
    public void EscapeValue_Wraps_WhenSpecialCharacters()
    {
        var result = Files.EscapeValue("a b=c#d");

        Assert.Equal("\"a b=c#d\"", result);
    }

    [Fact]
    public void ParseEnvironment_ResolvesConstAndEnv()
    {
        var mapPath = Path.Combine(_rootDir, "map.env");
        Environment.SetEnvironmentVariable("TEST_ENV_VAR", "env-value");
        try
        {
            File.WriteAllLines(mapPath, new[]
            {
                "KeyA=const:alpha",
                "KeyB=env:TEST_ENV_VAR",
                "KeyC=TEST_ENV_VAR"
            });

            var result = Files.ParseEnvironment(mapPath);

            Assert.Equal(3, result.Count);
            Assert.Equal("alpha", result[0].Value);
            Assert.Equal("env-value", result[1].Value);
            Assert.Equal("env-value", result[2].Value);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_ENV_VAR", null);
        }
    }

    [Fact]
    public void GenerateEnvironmentFile_WritesOnlyNonEmptyValues()
    {
        var mapPath = Path.Combine(_rootDir, "map.env");
        var outputPath = Path.Combine(_rootDir, "out", ".env");
        Environment.SetEnvironmentVariable("TEST_ENV_VAR", "env-value");
        try
        {
            File.WriteAllLines(mapPath, new[]
            {
                "KeyA=const:alpha",
                "KeyB=env:MISSING_ENV"
            });

            var success = Files.GenerateEnvironmentFile(mapPath, outputPath, _ => { }, _ => { });

            Assert.False(success);
            var lines = File.ReadAllLines(outputPath);
            Assert.Single(lines);
            Assert.Equal("KeyA=alpha", lines[0]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_ENV_VAR", null);
        }
    }
}
