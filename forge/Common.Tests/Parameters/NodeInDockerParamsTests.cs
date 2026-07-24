using Xunit;
using Nuke.Common;

using Entities;
using Parameters;
using Assert = Xunit.Assert;

namespace Common.Tests.Parameters;

public class NodeInDockerParamsTests
{
    [Fact]
    public void ToNodeParams_CopiesBaseProperties()
    {
        var source = new NodeInDockerParams
        {
            ArtifactsDir = "out",
            Config = new BuildConfig((Nuke.Common.IO.AbsolutePath)"/repo"),
            RootDirectory = "/repo",
            RepositoryUrl = "https://github.com/owner/repo",
            Version = new VersionInfo { Version = "1.0.0" },
            Notifications = true,
            ForceNotifications = true,
            NotificationsWebHookUrl = "https://hooks.example.com",
            ForcePush = true,
            DryRun = true,
            ChangeLogConfig = new ChangeLogConfig(),
            Verbosity = Verbosity.Verbose
        };

        var result = source.ToNodeParams();

        Assert.Equal("out", result.ArtifactsDir);
        Assert.Same(source.Config, result.Config);
        Assert.Equal(source.RootDirectory, result.RootDirectory);
        Assert.Equal(source.RepositoryUrl, result.RepositoryUrl);
        Assert.Same(source.Version, result.Version);
        Assert.Equal(source.Notifications, result.Notifications);
        Assert.Equal(source.ForceNotifications, result.ForceNotifications);
        Assert.Equal(source.NotificationsWebHookUrl, result.NotificationsWebHookUrl);
        Assert.Equal(source.ForcePush, result.ForcePush);
        Assert.Equal(source.DryRun, result.DryRun);
        Assert.Same(source.ChangeLogConfig, result.ChangeLogConfig);
        Assert.Equal(source.Verbosity, result.Verbosity);
    }
}
