using DocsCheck.Tests.TestSupport;
using Xunit;

namespace DocsCheck.Tests;

/// <summary>The check also fails a document whose own text is broken: an unclosed code fence or corrupted characters.</summary>
public sealed class DocumentStructureTests
{
    private static DocsCheckReport Run(TempRepo repo) => DocsChecker.Check(repo.Root, TempRepo.StandardManifest());

    private static DocsCheckFinding Single(DocsCheckReport report, DocsCheckErrorCode code) =>
        Assert.Single(report.Findings, f => f.Error.Code == code);

    private static string Chars(params int[] codes) => string.Concat(codes.Select(c => (char)c));

    // ---- Unclosed code fence

    [Fact]
    public void UnclosedCodeFence_Fails_NamingTheLineItOpenedOn()
    {
        using var repo = new TempRepo().Write("README.md", "# Title\n\n```yaml\nname: x\n\nMore prose that now renders as code.\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.UnclosedCodeFence);

        Assert.Equal("README.md", finding.Error.Document);
        Assert.Equal(3, finding.Line);
        Assert.Contains("README.md:3", finding.ToString());
    }

    [Fact]
    public void UnclosedCodeFence_SecondFenceLeftOpen_ReportsTheSecond()
    {
        using var repo = new TempRepo().Write("README.md", "```bash\nls\n```\n\ntext\n\n```bash\nls\n");

        Assert.Equal(7, Single(Run(repo), DocsCheckErrorCode.UnclosedCodeFence).Line);
    }

    [Fact]
    public void ClosedCodeFences_Pass()
    {
        using var repo = new TempRepo().Write("README.md", "```bash\nls\n```\n\n~~~yaml\nname: x\n~~~\n");

        Assert.Empty(Run(repo).Findings);
    }

    [Fact]
    public void UnclosedTildeFence_IsNotClosedByABacktickFence()
    {
        using var repo = new TempRepo().Write("README.md", "~~~md\n```bash\nls\n```\n");

        Assert.Equal(1, Single(Run(repo), DocsCheckErrorCode.UnclosedCodeFence).Line);
    }

    [Fact]
    public void BacktickFence_ContainingTildeLines_IsClosedByBackticks()
    {
        using var repo = new TempRepo().Write("README.md", "```md\n~~~\nnested\n~~~\n```\n");

        Assert.Empty(Run(repo).Findings);
    }

    // ---- Corrupted characters

    public static TheoryData<string> Corrupted => new()
    {
        { "replacement " + Chars(0xFFFD) + " character" },
        { "dash " + Chars(0xE2, 0x20AC, 0x201D) + " dash" },
        { "caf" + Chars(0xC3, 0xA9) },
        { "stray " + Chars(0xC2, 0xA0) + " space" },
        { "emoji " + Chars(0xF0, 0x178, 0x2018) + " emoji" },
    };

    [Theory]
    [MemberData(nameof(Corrupted))]
    public void CorruptedText_Fails_NamingTheLine(string text)
    {
        using var repo = new TempRepo().Write("documentation/docs/page.md", "# Title\n\n" + text + "\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.CorruptedText);

        Assert.Equal("documentation/docs/page.md", finding.Error.Document);
        Assert.Equal(3, finding.Line);
    }

    [Fact]
    public void CorruptedText_InsideACodeFence_Fails()
    {
        using var repo = new TempRepo().Write("README.md", "```text\ncaf" + Chars(0xC3, 0xA9) + "\n```\n");

        Assert.Equal(2, Single(Run(repo), DocsCheckErrorCode.CorruptedText).Line);
    }

    [Fact]
    public void CorruptedText_InPowerShellHelp_Fails()
    {
        using var repo = new TempRepo().Write(
            "scripts/powershell-module/Thing.psm1",
            "<#\n.SYNOPSIS\n  A dash " + Chars(0xE2, 0x20AC, 0x201D) + " here.\n#>\nfunction Get-Thing { }\n");

        var finding = Single(Run(repo), DocsCheckErrorCode.CorruptedText);

        Assert.Equal("scripts/powershell-module/Thing.psm1", finding.Error.Document);
        Assert.Equal(3, finding.Line);
    }

    [Fact]
    public void WellFormedNonAsciiText_Passes()
    {
        var text = "caf" + Chars(0xE9) + " " + Chars(0x2014) + " " + Chars(0x2713) + " " + Chars(0xD83D, 0xDE80) + " "
            + Chars(0xC4) + Chars(0xF6) + " " + Chars(0xA9) + " 2026 " + Chars(0xB7) + " " + Chars(0x2192);
        using var repo = new TempRepo().Write("README.md", "# Title\n\n" + text + "\n");

        Assert.Empty(Run(repo).Findings);
    }

    // ---- Both are recordable like any other finding

    [Fact]
    public void RecordedStructuralFindingWithReason_IsListedNotFailing()
    {
        using var repo = new TempRepo()
            .Write("README.md", "```bash\nls\n")
            .Write("design/docs-check-recorded.txt", "UnclosedCodeFence\tREADME.md\t```\tFixed by the page rewrite\n");

        var report = Run(repo);

        Assert.Empty(report.Findings);
        Assert.Equal(DocsCheckErrorCode.UnclosedCodeFence, Assert.Single(report.Recorded).Finding.Error.Code);
    }
}
