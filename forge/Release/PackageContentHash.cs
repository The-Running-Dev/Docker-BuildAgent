#nullable enable

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Release;

/// <summary>
/// A package's identity: the SHA-256 over its entries in ordinal path order, each taken as its path
/// and content, excluding the repository signature file <c>.signature.p7s</c>. A feed that signs a
/// package on upload therefore serves the same identity it was pushed with, while the hash of the
/// file itself would differ.
/// </summary>
public static class PackageContentHash
{
    /// <summary>The entry a feed adds when it repository-signs a package on upload.</summary>
    public const string RepositorySignatureEntry = ".signature.p7s";

    private const string Prefix = "sha256:";

    public static string Compute(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        return Compute(stream);
    }

    /// <summary>
    /// Each entry is hashed as its UTF-8 path and its content, each preceded by its length as a
    /// 64-bit big-endian integer, so no two different entry lists hash the same bytes.
    /// </summary>
    public static string Compute(Stream package)
    {
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(long)];

        var entries = archive.Entries
            .Where(entry => !string.Equals(entry.FullName, RepositorySignatureEntry, StringComparison.Ordinal))
            .OrderBy(entry => entry.FullName, StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var path = Encoding.UTF8.GetBytes(entry.FullName);
            BinaryPrimitives.WriteInt64BigEndian(length, path.Length);
            hash.AppendData(length);
            hash.AppendData(path);

            BinaryPrimitives.WriteInt64BigEndian(length, entry.Length);
            hash.AppendData(length);
            using var content = entry.Open();
            var buffer = new byte[81920];
            int read;
            while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }

        return Prefix + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
