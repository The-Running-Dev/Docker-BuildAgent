using Xunit;

using Entities;
using Utilities;

namespace Common.Tests.Utilities;

public class ChangeLogFormatterTests
{
    [Fact]
    public void Format_ReturnsEmpty_WhenNoCommits()
    {
        var result = ChangeLogFormatter.Format(new List<CommitInfo>(), new ChangeLogConfig());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_IncludesTagHeader_WhenTagProvided()
    {
        var commits = new List<CommitInfo>
        {
            new() { Hash = "abc", Author = "me", Date = "2025-01-01", Message = "feat" }
        };
        var config = new ChangeLogConfig { Tag = "v1.0.0" };

        var result = ChangeLogFormatter.Format(commits, config);

        Assert.Contains("## Since v1.0.0", result);
        Assert.Contains("- feat", result);
    }

    [Fact]
    public void Format_IncludesHashAndAuthor_WhenOptionsEnabled()
    {
        var commits = new List<CommitInfo>
        {
            new() { Hash = "abc", Author = "me", Date = "2025-01-01", Message = "feat" }
        };
        var config = new ChangeLogConfig();
        var options = new ChangeLogFormatOptions { IncludeHash = true, IncludeAuthor = true, DateFormat = "yyyy-MM-dd" };

        var result = ChangeLogFormatter.Format(commits, config, options);

        Assert.Contains("- [abc] feat (me)", result);
        Assert.Contains("### 2025-01-01", result);
    }
}
