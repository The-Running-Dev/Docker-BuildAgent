using System.IO;
using System.IO.Compression;
using System.Text;

using Xunit;

namespace Release.Tests;

/// <summary>
/// A package's identity (F4): SHA-256 over its entries in ordinal path order, path and content,
/// without the repository signature a feed adds on upload.
/// </summary>
public sealed class PackageContentHashTests
{
    private static string Hash(params (string Path, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var writer = archive.CreateEntry(path).Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                writer.Write(bytes, 0, bytes.Length);
            }
        }

        stream.Position = 0;
        return PackageContentHash.Compute(stream);
    }

    [Fact]
    public void Compute_IsASha256Identity()
    {
        Assert.Matches("^sha256:[0-9a-f]{64}$", Hash(("lib/tool.dll", "binary")));
    }

    [Fact]
    public void Compute_IgnoresTheRepositorySignature()
    {
        Assert.Equal(
            Hash(("lib/tool.dll", "binary"), ("tool.nuspec", "spec")),
            Hash(("lib/tool.dll", "binary"), ("tool.nuspec", "spec"), (PackageContentHash.RepositorySignatureEntry, "signed by the feed")));
    }

    [Fact]
    public void Compute_IgnoresEntryOrderInTheArchive()
    {
        Assert.Equal(
            Hash(("a.txt", "1"), ("b.txt", "2")),
            Hash(("b.txt", "2"), ("a.txt", "1")));
    }

    [Fact]
    public void Compute_Changes_WhenContentChanges()
    {
        Assert.NotEqual(Hash(("lib/tool.dll", "binary")), Hash(("lib/tool.dll", "binarY")));
    }

    [Fact]
    public void Compute_Changes_WhenAnEntryIsRenamed()
    {
        Assert.NotEqual(Hash(("lib/tool.dll", "binary")), Hash(("lib/tool2.dll", "binary")));
    }

    // Length-prefixing keeps a path/content boundary shift from producing the same bytes.
    [Fact]
    public void Compute_Changes_WhenBytesMoveBetweenPathAndContent()
    {
        Assert.NotEqual(Hash(("ab", "c")), Hash(("a", "bc")));
    }

    [Fact]
    public void Compute_ReadsAPackageFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var file = File.Create(path))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using var writer = archive.CreateEntry("lib/tool.dll").Open();
                var bytes = Encoding.UTF8.GetBytes("binary");
                writer.Write(bytes, 0, bytes.Length);
            }

            Assert.Equal(Hash(("lib/tool.dll", "binary")), PackageContentHash.Compute(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
