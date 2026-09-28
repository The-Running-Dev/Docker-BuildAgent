using System;
using System.IO;

using Xunit;

namespace Release.Tests;

/// <summary>S11.6: the release-notes template carries both required headings, and notes without them cannot publish.</summary>
public sealed class ReleaseNotesTemplateTests
{
    private static string Template()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "forge", "Forge.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, ".github", "RELEASE_TEMPLATE.md"));
    }

    [Fact]
    public void S11_6_TemplatePassesTheReleaseNotesGate()
    {
        ReleaseNotesValidator.Validate(Template());
    }

    [Theory]
    [InlineData("## 💥 Breaking Changes")]
    [InlineData("## 🗑️ Deprecations")]
    public void S11_6_TemplateWithARequiredHeadingRemovedCannotPublish(string heading)
    {
        var notes = Template().Replace(heading, "## Other");

        var ex = Assert.Throws<ReleaseException>(() => ReleaseNotesValidator.Validate(notes));

        Assert.Equal(ReleaseErrorCode.NotesSectionMissing, ex.Code);
    }
}
