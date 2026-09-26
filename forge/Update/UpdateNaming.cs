#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace Update;

/// <summary>Names and labels for the lock, prior container and prior image pin (contract § Prior image pin and prior container).</summary>
public static class UpdateNaming
{
    public const string LabelRole = "com.buildagent.role";
    public const string LabelContainer = "com.buildagent.container";
    public const string LabelUpdateId = "com.buildagent.update-id";
    public const string LabelOwnerHost = "com.buildagent.owner-host";
    public const string LabelOwnerPid = "com.buildagent.owner-pid";
    public const string LabelCreatedAt = "com.buildagent.created-at";
    public const string LabelDeadline = "com.buildagent.deadline";

    public const string RoleLock = "lock";
    public const string RolePrior = "prior";

    /// <summary>The first 32 lowercase hex characters of the SHA-256 of <paramref name="containerName"/> in UTF-8.</summary>
    public static string Hash(string containerName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(containerName));
        var builder = new StringBuilder(32);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
            if (builder.Length >= 32)
            {
                break;
            }
        }

        return builder.ToString(0, 32);
    }

    public static string LockContainerName(string targetName) => $"buildagent-lock-{Hash(targetName)}";

    public static string PriorContainerName(string targetName) => $"buildagent-prior-{Hash(targetName)}";

    public static string PriorImageTag(string targetName) => $"buildagent-prior:{Hash(targetName)}";
}
