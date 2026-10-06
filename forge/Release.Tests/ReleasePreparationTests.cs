#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Release.Tests.TestSupport;

using Surface;

using Xunit;

namespace Release.Tests;

public sealed class ReleasePreparationTests : IDisposable
{
    private const string Repository = "owner/repo";
    private const string Commit = "abc123";

    private readonly string _work = Path.Combine(Path.GetTempPath(), "release-preparation-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    // --- Version resolution -------------------------------------------------------------------

    [Theory]
    [InlineData("v2.1.0", null, null, "2.1.0")]
    [InlineData("v2.1.0-rc1", null, null, "2.1.0-rc1")]
    [InlineData(null, "2.1.0", null, "2.1.0")]
    [InlineData(null, "2.1.0", " ", "2.1.0")]
    [InlineData(null, "2.1.0", "rc1", "2.1.0-rc1")]
    public void Resolve_TakesTheTag_OrTheCoreAndLabel(string? tag, string? core, string? label, string expected)
    {
        Assert.Equal(expected, ReleaseVersionResolver.Resolve(tag, core, label).ToPackageString());
    }

    [Theory]
    [InlineData("v2.1.0", "2.1.0", null)]
    [InlineData(null, null, null)]
    [InlineData("2.1.0", null, null)]
    [InlineData("v2.1.0", null, "rc1")]
    [InlineData(null, "2.1.0-rc1", null)]
    [InlineData(null, "2.1", null)]
    public void Resolve_RefusesAMalformedRequest(string? tag, string? core, string? label)
    {
        Assert.Throws<ArgumentException>(() => ReleaseVersionResolver.Resolve(tag, core, label));
    }

    [Theory]
    [InlineData("v2.1.0-rc.1", null, null)]
    [InlineData(null, "2.1.0", "rc-1")]
    [InlineData(null, "2.1.0", "rc_1")]
    public void Resolve_RefusesALabelEverySinkCannotHold(string? tag, string? core, string? label)
    {
        var ex = Assert.Throws<ReleaseException>(() => ReleaseVersionResolver.Resolve(tag, core, label));
        Assert.Equal(ReleaseErrorCode.PreReleaseLabelUnsupported, ex.Code);
    }

    // --- Surface gate decision ----------------------------------------------------------------

    [Theory]
    [InlineData("2.0.0")]
    [InlineData("2.0.0-rc1")]
    public void SurfaceCheck_AcceptsAMissingBaseline_ForTheFirstGatedReleaseOnly(string version)
    {
        var candidate = Manifest(version);
        var missing = new SurfaceGateResult(false, candidate, null, null, SurfaceErrorCode.BaselineMissing, "no baseline");

        Assert.Same(candidate, ReleaseSurfaceCheck.Accept(missing, ReleaseVersion.Parse(version)));
    }

    [Theory]
    [InlineData("2.0.1")]
    [InlineData("2.1.0")]
    [InlineData("3.0.0")]
    public void SurfaceCheck_RefusesAMissingBaseline_AfterTheFirstGatedRelease(string version)
    {
        var missing = new SurfaceGateResult(false, Manifest(version), null, null, SurfaceErrorCode.BaselineMissing, "no baseline");

        var ex = Assert.Throws<ReleaseException>(() => ReleaseSurfaceCheck.Accept(missing, ReleaseVersion.Parse(version)));
        Assert.Equal(ReleaseErrorCode.SurfaceGateFailed, ex.Code);
        Assert.Contains("BaselineMissing", ex.Message);
    }

    [Fact]
    public void SurfaceCheck_RefusesABlockingDifference_EvenForTheFirstGatedRelease()
    {
        var blocked = new SurfaceGateResult(false, Manifest("2.0.0"), Manifest("1.0.0"), null, SurfaceErrorCode.BlockingDifference, "1 blocking");

        var ex = Assert.Throws<ReleaseException>(() => ReleaseSurfaceCheck.Accept(blocked, ReleaseVersion.Parse("2.0.0")));
        Assert.Contains("BlockingDifference", ex.Message);
    }

    [Fact]
    public void SurfaceCheck_ReturnsTheCandidate_WhenTheGatePasses()
    {
        var candidate = Manifest("2.1.0");

        Assert.Same(candidate, ReleaseSurfaceCheck.Accept(
            new SurfaceGateResult(true, candidate, Manifest("2.0.0"), Comparison(), null, "ok"), ReleaseVersion.Parse("2.1.0")));
    }

    // --- Notes composition --------------------------------------------------------------------

    [Fact]
    public void Compose_ListsTheCommits_AndSaysNone_WhenNothingWasDetected()
    {
        var notes = ReleaseNotesComposer.Compose(null, new[] { "feat: one", "fix: two" }, Comparison());

        Assert.Equal(
            "## Changes\n\n- feat: one\n- fix: two\n\n## Breaking Changes\n\nNone.\n\n## Deprecations\n\nNone.\n",
            notes);
        ReleaseNotesValidator.Validate(notes);
    }

    [Fact]
    public void Compose_PutsEachDetectedDifferenceInItsSection()
    {
        var notes = ReleaseNotesComposer.Compose(null, Array.Empty<string>(), Comparison(
            Difference(SurfaceDifferenceKind.ItemAdded, "added"),
            Difference(SurfaceDifferenceKind.ItemRemoved, "removed"),
            Difference(SurfaceDifferenceKind.ValueChanged, "changed", "a", "b"),
            Difference(SurfaceDifferenceKind.DeprecationRemoved, "undeprecated", "2.0.0", null),
            Difference(SurfaceDifferenceKind.RemovalTargetChanged, "moved", "3.0.0", "4.0.0"),
            Difference(SurfaceDifferenceKind.DeprecationAdded, "deprecated", null, "2.1.0")));

        var breaking = Section(notes, "## Breaking Changes");
        Assert.Contains("- BuildParameter `removed`: removed", breaking);
        Assert.Contains("- BuildParameter `changed`: changed from `a` to `b`", breaking);
        Assert.Contains("- BuildParameter `undeprecated`: no longer deprecated", breaking);
        Assert.Contains("- BuildParameter `moved`: removal moved from 3.0.0 to 4.0.0", breaking);
        Assert.DoesNotContain("`added`", notes);
        Assert.DoesNotContain("`deprecated`", breaking);
        Assert.Equal(
            "Detected in the public surface:\n\n- BuildParameter `deprecated`: deprecated since 2.1.0",
            Section(notes, "## Deprecations"));
        Assert.Contains("## Changes\n\nNone.\n", notes);
    }

    [Fact]
    public void Compose_KeepsAuthoredSections_AndReplacesAnAuthoredNone()
    {
        const string authored = "## Added\n\n- A thing.\n\n## Breaking Changes\n\nNone\n\n- Written by hand.\n\n## Deprecations\n\nNone.\n\nFooter line.\n";

        var notes = ReleaseNotesComposer.Compose(authored, new[] { "ignored: authored notes win" }, Comparison(
            Difference(SurfaceDifferenceKind.ValueChanged, "changed", "a", "b"),
            Difference(SurfaceDifferenceKind.DeprecationAdded, "deprecated", null, "2.1.0")));

        Assert.StartsWith("## Added\n\n- A thing.\n", notes);
        Assert.DoesNotContain("ignored", notes);
        Assert.Equal(
            "- Written by hand.\n\nDetected in the public surface:\n\n- BuildParameter `changed`: changed from `a` to `b`",
            Section(notes, "## Breaking Changes"));
        Assert.Equal(
            "Footer line.\n\nDetected in the public surface:\n\n- BuildParameter `deprecated`: deprecated since 2.1.0",
            Section(notes, "## Deprecations"));
        Assert.DoesNotContain("None.", notes);
    }

    [Fact]
    public void Compose_SaysThereIsNoBaseline_RatherThanNone_WhenThereIsNoComparison()
    {
        var notes = ReleaseNotesComposer.Compose(null, new[] { "feat: one" }, null);

        Assert.Equal(ReleaseNotesComposer.NoBaseline, Section(notes, "## Breaking Changes"));
        Assert.Equal("None.", Section(notes, "## Deprecations"));
    }

    [Fact]
    public void Compose_DoesNotAddAHeadingTheAuthoredNotesLeftOut()
    {
        var notes = ReleaseNotesComposer.Compose("## Added\n\n- A thing.\n\n## Deprecations\n", Array.Empty<string>(), Comparison());

        var ex = Assert.Throws<ReleaseException>(() => ReleaseNotesValidator.Validate(notes));
        Assert.Equal(ReleaseErrorCode.NotesSectionMissing, ex.Code);
    }

    [Fact]
    public void ReadAuthored_DropsThePageFurniture_AndMakesLinksAbsolute()
    {
        var directory = Path.Combine(_work, "documentation", "docs", "release-notes");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "2.1.0.md"), string.Join("\r\n",
            "---",
            "id: release-notes-2-1-0",
            "title: \"2.1.0 Release Notes\"",
            "---",
            "",
            "# 2.1.0 Release Notes",
            "",
            ":::note Not yet released",
            "Remove this notice when it is released.",
            ":::",
            "",
            "## Breaking Changes",
            "",
            "- See the [migration guide](../migration.md) and [samples](https://github.com/x/y).",
            "",
            "## Deprecations",
            "",
            "None.",
            "",
            "# Appendix"));

        var notes = ReleaseNotesComposer.ReadAuthored(_work, ReleaseVersion.Parse("2.1.0-rc1"), Repository, Commit);

        Assert.Equal(
            "## Breaking Changes\n\n- See the [migration guide](https://github.com/owner/repo/blob/abc123/documentation/docs/migration.md)"
            + " and [samples](https://github.com/x/y).\n\n## Deprecations\n\nNone.\n\n# Appendix\n",
            notes);
    }

    [Fact]
    public void ReadAuthored_IsNull_WhenTheVersionHasNoAuthoredNotes()
    {
        Assert.Null(ReleaseNotesComposer.ReadAuthored(_work, ReleaseVersion.Parse("2.1.0"), Repository, Commit));
    }

    [Fact]
    public void ReadAuthored_TheRepositorysOwnNotesPassTheGate()
    {
        var notes = ReleaseNotesComposer.ReadAuthored(RepoRoot(), ReleaseVersion.Parse("2.0.0"), Repository, Commit);

        Assert.NotNull(notes);
        Assert.DoesNotContain("Not yet released", notes);
        Assert.DoesNotContain("](../", notes);
        ReleaseNotesValidator.Validate(ReleaseNotesComposer.Compose(notes, Array.Empty<string>(), null));
    }

    // --- Tool packing -------------------------------------------------------------------------

    [Fact]
    public async Task PackTool_StampsTheVersion_AndReturnsThePackage()
    {
        var runner = new FakeCommandRunner().On(new[] { "pack" }, args =>
        {
            File.WriteAllText(Path.Combine(args[^1], "BuildAgent.Tool.2.1.0-rc1.nupkg"), "package");
            return FakeCommandRunner.Ok();
        });

        var package = await GlobalToolPackager.PackAsync("forge/Tool/Tool.csproj", ReleaseVersion.Parse("2.1.0-rc1"), _work, runner);

        Assert.Equal(Path.Combine(_work, "BuildAgent.Tool.2.1.0-rc1.nupkg"), package);
        var (fileName, arguments) = runner.Calls.Single();
        Assert.Equal("dotnet", fileName);
        Assert.Equal("forge/Tool/Tool.csproj", arguments[1]);
        Assert.Contains("-p:ReleaseVersion=2.1.0-rc1", arguments);
    }

    [Fact]
    public async Task PackTool_Fails_WhenThePackageIsNotWritten()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GlobalToolPackager.PackAsync("forge/Tool/Tool.csproj", ReleaseVersion.Parse("2.1.0"), _work, new FakeCommandRunner()));
        Assert.Contains("BuildAgent.Tool.2.1.0.nupkg", ex.Message);
    }

    private static SurfaceManifest Manifest(string version) => new(1, version, Array.Empty<SurfaceItem>());

    private static SurfaceComparison Comparison(params SurfaceDifference[] all) => new(all, Array.Empty<SurfaceDifference>());

    private static SurfaceDifference Difference(SurfaceDifferenceKind kind, string name, string? baseline = null, string? candidate = null) =>
        new(kind, SurfaceItemKind.BuildParameter, name, baseline, candidate);

    private static string Section(string notes, string heading)
    {
        var start = notes.IndexOf(heading + "\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{heading}' is missing from:\n{notes}");
        var body = notes[(start + heading.Length + 1)..];
        var next = body.IndexOf("\n## ", StringComparison.Ordinal);
        return (next < 0 ? body : body[..next]).Trim();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
