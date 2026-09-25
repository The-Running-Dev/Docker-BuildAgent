#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using Nuke.Common.Tooling;

namespace Release;

/// <summary>
/// <see cref="IImageTagChecker"/> backed by <c>docker manifest inspect</c> against the registry —
/// no local pull required, so a fresh CI runner still sees a tag published by a previous run.
/// Fails closed: only a registry answer of "no such manifest" counts as absent; any other
/// failure (auth, network, missing docker) throws rather than reporting the version free (I5).
/// </summary>
public sealed class DockerRegistryImageTagChecker : IImageTagChecker
{
    private static readonly string[] NotFoundMarkers = { "no such manifest", "manifest unknown" };

    private readonly string _registryUrl;
    private readonly string _imageTag;

    public DockerRegistryImageTagChecker(string registryUrl, string imageTag)
    {
        _registryUrl = registryUrl;
        _imageTag = imageTag;
    }

    public Task<bool> ExistsAsync(ReleaseVersion version)
    {
        var registryPrefix = string.IsNullOrEmpty(_registryUrl) ? string.Empty : $"{_registryUrl}/";
        var reference = $"{registryPrefix}{_imageTag}:{version.ToPackageString()}";

        var process = ProcessTasks.StartProcess("docker", $"manifest inspect \"{reference}\"", logOutput: false, logInvocation: false);
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return Task.FromResult(true);
        }

        var output = string.Join("\n", process.Output.Select(o => o.Text));
        if (NotFoundMarkers.Any(marker => output.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(false);
        }

        throw new InvalidOperationException(
            $"Could not determine whether image tag '{reference}' exists (docker exit {process.ExitCode}): {output}");
    }
}
