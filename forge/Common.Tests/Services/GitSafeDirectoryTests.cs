using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Services;
using Xunit;

namespace Common.Tests.Services;

[CollectionDefinition("GitSafeDirectory", DisableParallelization = true)]
public class GitSafeDirectoryCollection { }

[Collection("GitSafeDirectory")]
public class GitSafeDirectoryTests
{
    [Theory]
    [InlineData("workspace")]
    [InlineData("workspace with spaces")]
    public void SetSafeDirectory_PreservesTheExactPath(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "GitSafeDirectoryTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        var configPath = Path.Combine(root, "gitconfig");
        var previousConfig = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");

        try
        {
            // Isolate the real git invocation from the developer's global configuration.
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", configPath);
            var path = Path.Combine(root, name);
            var service = new GitService(NullLogger<GitService>.Instance);

            service.SetSafeDirectory(path);

            var start = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "config", "--file", configPath, "--get-all", "safe.directory" })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, error);
            Assert.Equal(path, output.TrimEnd('\r', '\n'));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", previousConfig);
            Directory.Delete(root, recursive: true);
        }
    }
}
