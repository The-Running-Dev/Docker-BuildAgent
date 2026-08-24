using Xunit;

using Extensions;

namespace Common.Tests.Extensions;
public class IoExtensionsTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _targetDir;

    public IoExtensionsTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "build-agent-io-tests", Guid.NewGuid().ToString("N"));
        _sourceDir = Path.Combine(root, "source");
        _targetDir = Path.Combine(root, "target");
        Directory.CreateDirectory(_sourceDir);
    }

    public void Dispose()
    {
        var root = Directory.GetParent(_sourceDir)?.Parent?.FullName;

        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CopyDirectory_CopiesFilesAndDirectories()
    {
        var subDir = Path.Combine(_sourceDir, "sub");
        
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_sourceDir, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subDir, "child.txt"), "child");

        _sourceDir.CopyDirectory(_targetDir);

        Assert.True(File.Exists(Path.Combine(_targetDir, "root.txt")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "sub", "child.txt")));
    }

    [Fact]
    public void StripDirectory_RemovesPrefix()
    {
        var sourcePath = Path.Combine(_sourceDir, "file.txt");
        var result = sourcePath.StripDirectory(_sourceDir);

        Assert.Equal($"file.txt", result);
    }
}
