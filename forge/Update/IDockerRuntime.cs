#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Update;

public enum MountKind
{
    Bind,
    Volume,
    Tmpfs,
}

/// <summary>One mount as reported by inspect. <see cref="IsAnonymousVolume"/> is set when a volume mount has no
/// caller-supplied source — Docker generated the volume's name itself (S2.8's first unsupported shape).</summary>
public sealed record MountSpec(
    MountKind Kind,
    string? Source,
    string Destination,
    bool ReadOnly,
    bool IsAnonymousVolume);

public sealed record PortSpec(
    int ContainerPort,
    string Protocol,
    string? HostIp,
    string? HostPort);

/// <summary>The state of a container as inspect reports it, restricted to the fields the Updater needs to
/// refuse safely (I34) or reproduce exactly. Deliberately excludes health-check state — S4's concern, not S2's.</summary>
public sealed record ContainerInspection(
    string Id,
    string Name,
    string ImageId,
    string? ImageReference,
    bool Running,
    bool AutoRemove,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<string> Env,
    IReadOnlyList<string> Command,
    IReadOnlyList<string> Entrypoint,
    IReadOnlyList<MountSpec> Mounts,
    IReadOnlyList<PortSpec> Ports,
    string RestartPolicy,
    IReadOnlyList<string> Networks,
    IReadOnlyList<string> Links);

/// <summary>Everything needed to create a container that reproduces another's configuration except its image
/// (I34). Built from a <see cref="ContainerInspection"/> by the Updater, not by the runtime.</summary>
public sealed record ContainerCreateSpec(
    string Name,
    string ImageId,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<string> Env,
    IReadOnlyList<string> Command,
    IReadOnlyList<string> Entrypoint,
    IReadOnlyList<MountSpec> Mounts,
    IReadOnlyList<PortSpec> Ports,
    string RestartPolicy,
    IReadOnlyList<string> Networks);

/// <summary>Thrown by an <see cref="IDockerRuntime"/> member for a daemon- or registry-level failure. The
/// Updater maps this to the appropriate <see cref="UpdateErrorCode"/> for the step that was in progress.</summary>
public sealed class DockerRuntimeException : Exception
{
    public DockerRuntimeException(string message) : base(message)
    {
    }

    public DockerRuntimeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>The Updater's only way to reach Docker. A production implementation shells to the `docker` CLI; a
/// test implementation simulates the daemon's atomic-name-uniqueness and label semantics in memory.</summary>
public interface IDockerRuntime
{
    /// <summary>Returns the named container's state, or null when no container has that name (S2.21).</summary>
    Task<ContainerInspection?> InspectAsync(string name);

    /// <summary>Resolves <paramref name="imageReference"/> to an image id, pulling it if the daemon does not
    /// already have it locally. Throws <see cref="DockerRuntimeException"/> when it cannot be obtained (S2.4).</summary>
    Task<string> ResolveImageIdAsync(string imageReference);

    /// <summary>Creates a container with the given name, image and labels only — never started. Used for the
    /// lock (I30), which must be creatable from an image already local so acquiring it performs no pull (I33).
    /// Throws <see cref="DockerRuntimeException"/> when the name is already in use.</summary>
    Task<string> CreateMarkerAsync(string name, string imageId, IReadOnlyDictionary<string, string> labels);

    /// <summary>Creates the replacement container, never started. Throws <see cref="DockerRuntimeException"/>
    /// when the daemon rejects the spec — never approximates (I34).</summary>
    Task<string> CreateReplacementAsync(ContainerCreateSpec spec);

    Task StartAsync(string name);

    Task StopAsync(string name);

    /// <summary>Renames a container in place. Its id, image and configuration are untouched.</summary>
    Task RenameAsync(string currentName, string newName);

    Task RemoveAsync(string name, bool force);

    /// <summary>Tags an already-local image id, for the prior-image pin (I38).</summary>
    Task TagImageAsync(string imageId, string tag);

    Task RemoveImageTagAsync(string tag);
}
