using Xunit;
using Nuke.Common.IO;

using Entities;

namespace Common.Tests.Entities;

public class BuildConfigTests
{
    [Fact]
    public void Paths_AreDerivedFromRootDirectory()
    {
        var root = (AbsolutePath)"C:/repo";
        var config = new BuildConfig(root);

        Assert.EndsWith(".build", config.DirectoryPath);
        Assert.EndsWith(".build.copy", config.FilesToCopyConfigPath);
        Assert.EndsWith(".build.scripts", config.ScriptsConfigPath);
        Assert.EndsWith(".build.env", config.EnvFilePath);
        Assert.EndsWith(".build.env.map", config.EnvMapFilePath);
        Assert.EndsWith(".env", config.AppEnvFilePath);
        Assert.EndsWith(".app.env.map", config.AppEnvMapFilePath);
    }
}
