#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Update;

namespace Update.Tests;

/// <summary>
/// An in-memory Docker simulation faithful to the two semantics the Updater depends on: name uniqueness is
/// atomic (whichever caller inserts the name first wins; the loser sees a conflict), and a container's labels
/// are fixed at creation — <see cref="RenameAsync"/> moves an entry without ever touching its labels, exactly
/// as the daemon has no "add a label" operation.
/// </summary>
public sealed class FakeDockerRuntime : IDockerRuntime
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ContainerInspection> _containers = new();
    private readonly Dictionary<string, string> _imageReferences = new();
    private readonly HashSet<string> _localImages = new();

    public bool RegistryReachable { get; set; } = true;
    public Func<string, bool>? FailCreateReplacement { get; set; }
    public Func<string, bool>? FailTagImage { get; set; }

    public void SeedContainer(ContainerInspection inspection)
    {
        lock (_gate)
        {
            _containers[inspection.Name] = inspection;
            _localImages.Add(inspection.ImageId);
        }
    }

    public void SeedImage(string reference, string imageId, bool local = true)
    {
        lock (_gate)
        {
            _imageReferences[reference] = imageId;
            if (local)
            {
                _localImages.Add(imageId);
            }
        }
    }

    public ContainerInspection? Get(string name)
    {
        lock (_gate)
        {
            return _containers.TryGetValue(name, out var c) ? c : null;
        }
    }

    public bool HasImageTag(string tag)
    {
        lock (_gate)
        {
            return _imageReferences.ContainsKey(tag);
        }
    }

    public Task<ContainerInspection?> InspectAsync(string name)
    {
        lock (_gate)
        {
            return Task.FromResult(_containers.TryGetValue(name, out var container) ? container : null);
        }
    }

    public Task<string> ResolveImageIdAsync(string imageReference)
    {
        lock (_gate)
        {
            if (!_imageReferences.TryGetValue(imageReference, out var imageId))
            {
                throw new DockerRuntimeException($"image not found: {imageReference}");
            }

            if (!_localImages.Contains(imageId) && !RegistryReachable)
            {
                throw new DockerRuntimeException("registry unreachable");
            }

            _localImages.Add(imageId);
            return Task.FromResult(imageId);
        }
    }

    public Task<string> CreateMarkerAsync(string name, string imageId, IReadOnlyDictionary<string, string> labels)
    {
        lock (_gate)
        {
            if (_containers.ContainsKey(name))
            {
                throw new DockerRuntimeException($"name already in use: {name}");
            }

            var id = Guid.NewGuid().ToString("N");
            _containers[name] = new ContainerInspection(
                Id: id,
                Name: name,
                ImageId: imageId,
                ImageReference: null,
                Running: false,
                AutoRemove: false,
                Labels: new Dictionary<string, string>(labels),
                Env: Array.Empty<string>(),
                Command: Array.Empty<string>(),
                Entrypoint: Array.Empty<string>(),
                Mounts: Array.Empty<MountSpec>(),
                Ports: Array.Empty<PortSpec>(),
                RestartPolicy: "no",
                Networks: Array.Empty<string>(),
                Links: Array.Empty<string>());
            return Task.FromResult(id);
        }
    }

    public Task<string> CreateReplacementAsync(ContainerCreateSpec spec)
    {
        lock (_gate)
        {
            if (FailCreateReplacement?.Invoke(spec.Name) == true)
            {
                throw new DockerRuntimeException($"simulated create failure for {spec.Name}");
            }

            if (_containers.ContainsKey(spec.Name))
            {
                throw new DockerRuntimeException($"name already in use: {spec.Name}");
            }

            var id = Guid.NewGuid().ToString("N");
            _containers[spec.Name] = new ContainerInspection(
                Id: id,
                Name: spec.Name,
                ImageId: spec.ImageId,
                ImageReference: null,
                Running: false,
                AutoRemove: false,
                Labels: new Dictionary<string, string>(spec.Labels),
                Env: spec.Env,
                Command: spec.Command,
                Entrypoint: spec.Entrypoint,
                Mounts: spec.Mounts,
                Ports: spec.Ports,
                RestartPolicy: spec.RestartPolicy,
                Networks: spec.Networks,
                Links: Array.Empty<string>());
            return Task.FromResult(id);
        }
    }

    public Task StartAsync(string name) => Mutate(name, c => c with { Running = true });

    public Task StopAsync(string name) => Mutate(name, c => c with { Running = false });

    public Task RenameAsync(string currentName, string newName)
    {
        lock (_gate)
        {
            if (!_containers.TryGetValue(currentName, out var container))
            {
                throw new DockerRuntimeException($"no such container: {currentName}");
            }

            if (_containers.ContainsKey(newName))
            {
                throw new DockerRuntimeException($"name already in use: {newName}");
            }

            _containers.Remove(currentName);
            _containers[newName] = container with { Name = newName };
            return Task.CompletedTask;
        }
    }

    public Task RemoveAsync(string name, bool force)
    {
        lock (_gate)
        {
            _containers.Remove(name);
            return Task.CompletedTask;
        }
    }

    public Task TagImageAsync(string imageId, string tag)
    {
        lock (_gate)
        {
            if (FailTagImage?.Invoke(tag) == true)
            {
                throw new DockerRuntimeException($"simulated tag failure for {tag}");
            }

            if (!_localImages.Contains(imageId))
            {
                throw new DockerRuntimeException($"image not local: {imageId}");
            }

            _imageReferences[tag] = imageId;
            return Task.CompletedTask;
        }
    }

    public Task RemoveImageTagAsync(string tag)
    {
        lock (_gate)
        {
            _imageReferences.Remove(tag);
            return Task.CompletedTask;
        }
    }

    private Task Mutate(string name, Func<ContainerInspection, ContainerInspection> update)
    {
        lock (_gate)
        {
            if (!_containers.TryGetValue(name, out var container))
            {
                throw new DockerRuntimeException($"no such container: {name}");
            }

            _containers[name] = update(container);
            return Task.CompletedTask;
        }
    }
}

public sealed class FakeUpdateClock : IUpdateClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-09-21T00:00:00Z");
}
