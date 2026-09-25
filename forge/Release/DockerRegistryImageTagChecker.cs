#nullable enable

using System;
using System.Threading.Tasks;

using Nuke.Common.Tooling;

namespace Release;

/// <summary>
/// <see cref="IImageTagChecker"/> backed by <c>docker manifest inspect</c> against the registry —
/// no local pull required, so a fresh CI runner still sees a tag published by a previous run.
/// </summary>
public sealed class DockerRegistryImageTagChecker : IImageTagChecker
{
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

        try
        {
            ProcessTasks
                .StartProcess("docker", $"manifest inspect \"{reference}\"", logOutput: false, logInvocation: false)
                .AssertWaitForExit()
                .AssertZeroExitCode();
            return Task.FromResult(true);
        }
        catch (Exception)
        {
            return Task.FromResult(false);
        }
    }
}
