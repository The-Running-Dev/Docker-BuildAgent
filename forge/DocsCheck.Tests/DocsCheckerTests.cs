using DocsCheck.Tests.TestSupport;
using Surface;
using Xunit;

namespace DocsCheck.Tests;

public sealed class DocsCheckerTests
{
    private static DocsCheckReport Run(TempRepo repo, SurfaceManifest? manifest = null) =>
        DocsChecker.Check(repo.Root, manifest ?? TempRepo.StandardManifest());

    private static DocsCheckFinding Single(DocsCheckReport report, DocsCheckErrorCode code) =>
        Assert.Single(report.Findings, f => f.Error.Code == code);

    // ---- S10.2: UnknownName (I45)

    [Fact]
    public void S10_2_UnknownBuildType_FailsNamingDocumentLineAndName()
    {
        using var repo = new TempRepo().Write("README.md", "# Title\n\n```bash\nbuild bogus\n```\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.UnknownName);

        Assert.Equal("README.md", finding.Error.Document);
        Assert.Equal("bogus", finding.Error.Name);
        Assert.Equal(4, finding.Line);
        Assert.Contains("README.md:4", finding.ToString());
        Assert.Contains("bogus", finding.ToString());
    }

    [Fact]
    public void S10_2_KnownBuildTypeAndParameter_Pass()
    {
        using var repo = new TempRepo().Write("README.md",
            "```bash\ndocker run -v ./:/workspace ghcr.io/x/build-agent:latest build docker --image-tag 1.0 \\\n  --artifacts-dir out\n```\n");

        Assert.DoesNotContain(Run(repo).Findings, f => f.Error.Code == DocsCheckErrorCode.UnknownName);
    }

    [Fact]
    public void S10_2_UnknownBuildParameter_Fails()
    {
        using var repo = new TempRepo().Write("README.md", "```bash\nbuild node --no-such-flag x\n```\n");

        Assert.Equal("no-such-flag", Single(Run(repo), DocsCheckErrorCode.UnknownName).Error.Name);
    }

    [Fact]
    public void S10_2_HostToolFlagsAreNotProductSurface()
    {
        using var repo = new TempRepo().Write("README.md", "```bash\ndocker run --rm -v ./:/workspace img build node --artifacts-dir out\n```\n");

        Assert.DoesNotContain(Run(repo).Findings, f => f.Error.Code == DocsCheckErrorCode.UnknownName);
    }

    [Fact]
    public void S10_2_NonBuildUsesOfTheWordBuild_AreNotClaims()
    {
        using var repo = new TempRepo().Write("README.md", "```bash\ndotnet build forge.sln\nnpm run build\ndocker build .\n```\n\nThe `build process` runs.\n");

        Assert.Empty(Run(repo).Findings);
    }

    [Fact]
    public void S10_2_UnknownModuleCommand_Fails()
    {
        using var repo = new TempRepo().Write("README.md", "Call `Invoke-BuildBogus` to go.\n");

        Assert.Equal("Invoke-BuildBogus", Single(Run(repo), DocsCheckErrorCode.UnknownName).Error.Name);
    }

    [Fact]
    public void S10_2_UnknownModuleParameter_Fails_KnownPasses()
    {
        using var repo = new TempRepo().Write("README.md",
            "```powershell\nInvoke-Build -type docker -args @{ imageTag = 'x' }\nInvoke-Build -nonsense 1\n```\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.UnknownName);

        Assert.Equal("Invoke-Build.nonsense", finding.Error.Name);
        Assert.Equal(3, finding.Line);
    }

    [Fact]
    public void S10_2_UnknownArgsHashtableKey_Fails()
    {
        using var repo = new TempRepo().Write("README.md", "```powershell\nInvoke-Build -type docker -args @{ imageTag = 'x'; bogusKey = 1 }\n```\n");

        Assert.Equal("bogusKey", Single(Run(repo), DocsCheckErrorCode.UnknownName).Error.Name);
    }

    [Fact]
    public void S10_2_MissingRepositoryPath_Fails_ExistingPasses()
    {
        using var repo = new TempRepo()
            .Write("forge/Real/Real.cs", "")
            .Write("README.md", "See `forge/Real/Real.cs` and `forge/Gone/Gone.cs` and `.build/` and `./dist` and `docs/x`.\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.UnknownName);

        Assert.Equal("forge/Gone/Gone.cs", finding.Error.Name);
    }

    [Fact]
    public void S10_2_BrokenRelativeLink_Fails_WorkingLinkPasses()
    {
        using var repo = new TempRepo()
            .Write("documentation/docs/a.md", "[ok](./b.md) [bad](./missing.md) [web](https://example.com/x.md) [anchor](#top) [route](/docs/parameters)\n")
            .Write("documentation/docs/b.md", "");

        var finding = Single(Run(repo), DocsCheckErrorCode.UnknownName);

        Assert.Equal("documentation/docs/a.md", finding.Error.Document);
        Assert.Equal("./missing.md", finding.Error.Name);
    }

    [Fact]
    public void S10_2_DiscoveryLocation_UnknownFails_KnownPasses()
    {
        using var repo = new TempRepo().Write("README.md", "Uses `ExplicitTemplatesDirectory` then `MadeUpTemplatesDirectory`.\n");

        Assert.Equal("MadeUpTemplatesDirectory", Single(Run(repo), DocsCheckErrorCode.UnknownName).Error.Name);
    }

    // ---- S10.3: CanonicalSourceMissing (I46)

    [Fact]
    public void S10_3_DocumentCoveringProtectedSurfaceWithoutCanonicalSource_Fails()
    {
        using var repo = new TempRepo().Write("README.md", "Run `Invoke-Build` to build.\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.CanonicalSourceMissing);

        Assert.Equal("README.md", finding.Error.Document);
        Assert.Equal("PowerShell module", finding.Error.Name);
    }

    [Fact]
    public void S10_3_NamedCanonicalSource_Passes()
    {
        using var repo = new TempRepo()
            .Write("PSModule.requirements.md", "Canonical for: PowerShell module\n")
            .Write("README.md", "Canonical contract: [PSModule.requirements.md](PSModule.requirements.md)\n\nRun `Invoke-Build` to build.\n");

        Assert.Empty(Run(repo).Findings);
    }

    [Fact]
    public void S10_3_PointerQualifiedToAnotherSurface_DoesNotCoverThisOne()
    {
        using var repo = new TempRepo()
            .Write("PSModule.requirements.md", "Canonical for: PowerShell module\n")
            .Write("README.md", "Canonical contract (build command): PSModule.requirements.md\n\nRun `Invoke-Build` to build.\n");

        Assert.Equal("PowerShell module", Single(Run(repo), DocsCheckErrorCode.CanonicalSourceMissing).Error.Name);
    }

    [Fact]
    public void S10_3_DocumentNamingNothingProtected_NeedsNoPointer()
    {
        using var repo = new TempRepo().Write("README.md", "Just prose about builds.\n");

        Assert.Empty(Run(repo).Findings);
    }

    [Fact]
    public void S10_3_PowerShellHelpCoveringTheModule_NeedsPointer()
    {
        using var repo = new TempRepo().Write("scripts/powershell-module/M.psm1",
            "<#\n.SYNOPSIS\nRuns Invoke-Build things.\n#>\nfunction X {}\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.CanonicalSourceMissing);

        Assert.Equal("scripts/powershell-module/M.psm1", finding.Error.Document);
    }

    // ---- S10.4: CanonicalSourceConflict

    [Fact]
    public void S10_4_TwoDocumentsCanonicalForOneSurface_Fail()
    {
        using var repo = new TempRepo()
            .Write("PSModule.requirements.md", "Canonical for: PowerShell module\n")
            .Write("documentation/docs/a.md", "Canonical for: PowerShell module, build command\n")
            .Write("documentation/docs/b.md", "Canonical for: build command\n");

        var conflicts = Run(repo).Findings.Where(f => f.Error.Code == DocsCheckErrorCode.CanonicalSourceConflict).ToList();

        Assert.Equal(4, conflicts.Count);
        Assert.Contains(conflicts, f => f.Error.Name == "PowerShell module");
        Assert.Contains(conflicts, f => f.Error.Name == "build command");
    }

    [Fact]
    public void S10_4_OneCanonicalDocumentPerSurface_Passes()
    {
        using var repo = new TempRepo()
            .Write("documentation/docs/a.md", "Canonical for: PowerShell module\n")
            .Write("documentation/docs/b.md", "Canonical for: build command\n");

        Assert.Empty(Run(repo).Findings);
    }

    // ---- S10.5: scope

    [Fact]
    public void S10_5_CoversSiteSourcesReadmeAndPowerShellHelp()
    {
        using var repo = new TempRepo()
            .Write("README.md", "`Invoke-BuildA`\n")
            .Write("documentation/docs/deep/page.md", "`Invoke-BuildB`\n")
            .Write("documentation/src/pages/index.md", "`Invoke-BuildC`\n")
            .Write("scripts/powershell-module/M.psm1", "<#\n.EXAMPLE\nInvoke-BuildD\n#>\n")
            .Write("scripts/powershell-module/M.Tests.ps1", "Invoke-BuildIgnored\n");

        var names = Run(repo).Findings.Where(f => f.Error.Code == DocsCheckErrorCode.UnknownName).Select(f => f.Error.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "Invoke-BuildA", "Invoke-BuildB", "Invoke-BuildC", "Invoke-BuildD" }, names);
    }

    // ---- S10.6: deprecated but present

    [Fact]
    public void S10_6_DeprecatedButPresentSurface_DoesNotFail()
    {
        using var repo = new TempRepo()
            .Write("PSModule.requirements.md", "Canonical for: PowerShell module\n")
            .Write("README.md", "Canonical contract: PSModule.requirements.md\n\n`Invoke-BuildOld`\n");
        var manifest = TempRepo.Manifest(TempRepo.Item(SurfaceItemKind.ModuleCommand, "Invoke-BuildOld", deprecatedSince: "2.0.0"));

        Assert.Empty(Run(repo, manifest).Findings);
    }

    // ---- S10.7: what is not covered

    [Fact]
    public void S10_7_OutputStatesUncoveredClaimClassesAndMakesNoBehaviouralClaim()
    {
        using var repo = new TempRepo().Write("README.md", "Nothing.\n");

        var report = Run(repo);
        var text = report.Render();

        Assert.NotEmpty(report.NotCovered);
        Assert.Contains(report.NotCovered, c => c.Contains("behavioural", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Not covered", text);
        foreach (var claim in report.NotCovered)
        {
            Assert.Contains(claim, text);
        }
    }

    [Fact]
    public void S10_7_RenderListsEachFindingWithDocumentLineAndName()
    {
        using var repo = new TempRepo().Write("README.md", "\n`Invoke-BuildZzz`\n");

        var text = Run(repo).Render();

        Assert.Contains("README.md:2", text);
        Assert.Contains("UnknownName", text);
        Assert.Contains("Invoke-BuildZzz", text);
    }

    // ---- S10.8: findings are reported, and only recorded (with a reason) findings are set aside

    [Fact]
    public void S10_8_RecordedFindingWithReason_IsListedNotFailing()
    {
        using var repo = new TempRepo()
            .Write("PSModule.requirements.md", "Canonical for: PowerShell module\n")
            .Write("README.md", "Canonical contract: PSModule.requirements.md\n\n`Invoke-BuildBogus`\n")
            .Write("design/docs-check-recorded.txt", "# recorded\nUnknownName\tREADME.md\tInvoke-BuildBogus\tS11 rewrites this page\n");

        var report = Run(repo);

        Assert.True(report.Success);
        var recorded = Assert.Single(report.Recorded);
        Assert.Equal("Invoke-BuildBogus", recorded.Finding.Error.Name);
        Assert.Equal("S11 rewrites this page", recorded.Reason);
        Assert.Contains("S11 rewrites this page", report.Render());
    }

    [Fact]
    public void S10_8_RecordedEntryWithoutReason_DoesNotSuppress()
    {
        using var repo = new TempRepo()
            .Write("README.md", "`Invoke-BuildBogus`\n")
            .Write("design/docs-check-recorded.txt", "UnknownName\tREADME.md\tInvoke-BuildBogus\t\n");

        Assert.False(Run(repo).Success);
    }

    [Fact]
    public void S10_8_RecordedEntryForAnotherName_DoesNotSuppress()
    {
        using var repo = new TempRepo()
            .Write("README.md", "`Invoke-BuildBogus`\n")
            .Write("design/docs-check-recorded.txt", "UnknownName\tREADME.md\tInvoke-BuildOther\treason\n");

        Assert.False(Run(repo).Success);
    }

    [Fact]
    public void S10_8_RecordedEntryThatNoLongerMatches_IsReportedAsStale()
    {
        using var repo = new TempRepo()
            .Write("README.md", "Fine.\n")
            .Write("design/docs-check-recorded.txt", "UnknownName\tREADME.md\tInvoke-BuildGone\treason\n");

        var report = Run(repo);

        Assert.True(report.Success);
        Assert.Contains("stale", report.Render(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void S10_2_LeadingDotIsKeptInReportedPathNames()
    {
        using var repo = new TempRepo().Write("README.md", "See `.github/workflows/gone.yml`.\n").Write(".github/x.txt", "");

        Assert.Equal(".github/workflows/gone.yml", Single(Run(repo), DocsCheckErrorCode.UnknownName).Error.Name);
    }

    [Fact]
    public void S10_2_CommentsAndProseInsideBlocksAreNotInvocations()
    {
        using var repo = new TempRepo().Write("README.md",
            "```bash\n# build bogus\n// build bogus\nfeat: add new build type\nNo build type specified\nng build --configuration production\n```\n");

        Assert.Empty(Run(repo).Findings);
    }

    [Fact]
    public void S10_2_NukePassThroughFlagsAndPlaceholderPathsAreNotClaims()
    {
        using var repo = new TempRepo().Write("README.md",
            "```bash\nbuild node --target Foo\n```\n\nSee `forge/[BuildType]/x.csproj`.\n");

        Assert.DoesNotContain(Run(repo).Findings, f => f.Error.Code == DocsCheckErrorCode.UnknownName);
    }

    // ---- S10.9: docs-template

    [Fact]
    public void S10_9_DoesNotReadDocsTemplate()
    {
        using var repo = new TempRepo()
            .Write("docs-template/README.md", "`Invoke-BuildBogus` build bogus\n")
            .Write("docs-template/docs/x.md", "`Invoke-BuildBogus`\n")
            .Write("README.md", "See `docs-template/docs/x.md` and `docs-template/missing.md`.\n")
            .Write("documentation/docs/a.md", "");

        var report = Run(repo);

        Assert.Empty(report.Findings);
        Assert.DoesNotContain(report.Documents, d => d.StartsWith("docs-template", StringComparison.Ordinal));
    }
}
