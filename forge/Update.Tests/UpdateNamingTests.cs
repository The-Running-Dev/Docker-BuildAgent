#nullable enable

using System.Text.RegularExpressions;
using Update;
using Xunit;

namespace Update.Tests;

public sealed class UpdateNamingTests
{
    [Fact]
    public void Hash_IsThirtyTwoLowercaseHexCharacters()
    {
        var hash = UpdateNaming.Hash("web");
        Assert.Matches(new Regex("^[0-9a-f]{32}$"), hash);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        Assert.Equal(UpdateNaming.Hash("web"), UpdateNaming.Hash("web"));
    }

    [Fact]
    public void Hash_DiffersByContainerName()
    {
        Assert.NotEqual(UpdateNaming.Hash("web"), UpdateNaming.Hash("db"));
    }

    [Fact]
    public void LockContainerName_HasExpectedShape()
    {
        var name = UpdateNaming.LockContainerName("web");
        Assert.Equal($"buildagent-lock-{UpdateNaming.Hash("web")}", name);
    }

    [Fact]
    public void PriorContainerName_HasExpectedShape()
    {
        var name = UpdateNaming.PriorContainerName("web");
        Assert.Equal($"buildagent-prior-{UpdateNaming.Hash("web")}", name);
    }

    [Fact]
    public void PriorImageTag_HasExpectedShape()
    {
        var tag = UpdateNaming.PriorImageTag("web");
        Assert.Equal($"buildagent-prior:{UpdateNaming.Hash("web")}", tag);
    }
}
