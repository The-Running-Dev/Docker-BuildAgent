using DocsCheck.Tests.TestSupport;
using Surface;
using Xunit;

namespace DocsCheck.Tests;

/// <summary>S12: every document about a public surface is a canonical contract, a reference naming its source, or removed.</summary>
public sealed class DocumentOwnershipTests
{
    private const string ListPath = "design/docs-classification.txt";

    private static readonly string[] ProtectedSurfaces =
        { "image", "build command", "global tool", "project configuration", "Docker-template discovery", "PowerShell module" };

    private sealed record Row(string Document, string Class, string Detail);

    private static string Root => RepoRootLocator.Find();

    private static List<Row> ReadList()
    {
        var path = Path.Combine(Root, "design", "docs-classification.txt");
        Assert.True(File.Exists(path), $"{ListPath} must exist.");

        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t'))
            .Select(p =>
            {
                Assert.True(p.Length >= 4 && p[3].Trim().Length > 0, $"Every classification carries a note: '{string.Join("|", p)}'.");
                return new Row(p[0].Trim(), p[1].Trim(), p[2].Trim());
            })
            .ToList();
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static IEnumerable<string> DocumentsOnDisk()
    {
        var root = Root;
        var found = new List<string>();
        if (File.Exists(Path.Combine(root, "README.md")))
        {
            found.Add("README.md");
        }

        foreach (var tree in new[] { "documentation/docs", "documentation/src/pages" })
        {
            var directory = Path.Combine(root, tree.Replace('/', Path.DirectorySeparatorChar));
            found.AddRange(Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase))
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')));
        }

        found.AddRange(Directory.EnumerateFiles(Path.Combine(root, "scripts", "powershell-module"), "*.ps*1")
            .Where(f => !f.EndsWith(".Tests.ps1", StringComparison.OrdinalIgnoreCase)
                && (f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".psm1", StringComparison.OrdinalIgnoreCase)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')));

        return found;
    }

    [Fact]
    public void S12_1_EveryDocumentIsClassifiedOnce_AndTheListMatchesTheTree()
    {
        var rows = ReadList();

        Assert.Equal(rows.Count, rows.Select(r => r.Document).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, r => Assert.Contains(r.Class, new[] { "canonical", "reference", "removed", "non-overlapping" }));

        foreach (var document in DocumentsOnDisk())
        {
            Assert.True(rows.Any(r => r.Document == document), $"'{document}' is not classified in {ListPath}.");
        }

        foreach (var row in rows.Where(r => r.Class != "removed"))
        {
            Assert.True(File.Exists(Path.Combine(Root, row.Document)), $"'{row.Document}' is classified {row.Class} but does not exist.");
        }
    }

    [Fact]
    public void S12_2_EachProtectedSurfaceHasExactlyOneCanonicalContract_AndTheModulesIsTheRequirementsDocument()
    {
        var canonical = ReadList().Where(r => r.Class == "canonical").ToList();

        foreach (var surface in ProtectedSurfaces)
        {
            var owners = canonical.Where(r => r.Detail.Split(',').Select(s => s.Trim()).Contains(surface, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.True(owners.Count == 1, $"The {surface} surface needs exactly one canonical contract; the list gives {owners.Count}.");

            var declaration = Read(owners[0].Document);
            Assert.Matches($@"(?im)^[\s>*\-]*(?:\*\*)?Canonical for(?:\*\*)?\s*:.*\b{System.Text.RegularExpressions.Regex.Escape(surface)}\b", declaration);
        }

        Assert.Equal("PSModule.requirements.md", canonical.Single(r => r.Detail.Contains("PowerShell module")).Document);
    }

    [Fact]
    public void S12_3_EveryReferenceNamesItsCanonicalSource_AndTheCheckReportsNoCanonicalSourceMissing()
    {
        var canonical = ReadList().Where(r => r.Class == "canonical").Select(r => r.Document).ToHashSet();

        foreach (var reference in ReadList().Where(r => r.Class == "reference"))
        {
            Assert.Contains(reference.Detail, canonical);
            Assert.Contains("Canonical contract", Read(reference.Document));
            Assert.Contains(Path.GetFileName(reference.Detail), Read(reference.Document));
        }

        var report = DocsChecker.Check(Root, SurfaceDeriver.Derive(Root, "0.0.0"));
        Assert.DoesNotContain(report.Findings, f => f.Error.Code == DocsCheckErrorCode.CanonicalSourceMissing);
        Assert.DoesNotContain(report.Recorded, r => r.Finding.Error.Code == DocsCheckErrorCode.CanonicalSourceMissing);
    }

    [Fact]
    public void S12_4_NoSurfaceHasTwoCanonicalDocuments()
    {
        var report = DocsChecker.Check(Root, SurfaceDeriver.Derive(Root, "0.0.0"));

        Assert.DoesNotContain(report.Findings, f => f.Error.Code == DocsCheckErrorCode.CanonicalSourceConflict);
        Assert.DoesNotContain(report.Recorded, r => r.Finding.Error.Code == DocsCheckErrorCode.CanonicalSourceConflict);
    }

    [Fact]
    public void S12_5_RemovedDocumentsAreGoneFromTheTree_AndNothingLinksToThem()
    {
        var removed = ReadList().Where(r => r.Class == "removed").Select(r => r.Document).ToList();
        Assert.NotEmpty(removed);

        foreach (var document in removed)
        {
            Assert.False(File.Exists(Path.Combine(Root, document)), $"'{document}' is classified removed but still exists.");

            // The published site is generated from documentation/docs, so a removed page must not be linked from a kept page.
            foreach (var kept in DocumentsOnDisk().Where(d => d.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.DoesNotContain(Path.GetFileName(document), Read(kept));
            }
        }
    }

    [Fact]
    public void S12_6_NoDocumentDisagreesWithTheTree_AndEveryRecordedNameExistsInTheTree()
    {
        var report = DocsChecker.Check(Root, SurfaceDeriver.Derive(Root, "0.0.0"));
        Assert.Empty(report.Findings);
        Assert.Empty(report.StaleRecords);

        // What stays recorded is real: the tree has the name, the surface manifest does not derive it (a manifest gap, not stale prose).
        var wrapper = Read("scripts/nuke/build.ps1") + Read("forge/Forge/Forge.cs");
        foreach (var recorded in report.Recorded)
        {
            Assert.Equal(DocsCheckErrorCode.UnknownName, recorded.Finding.Error.Code);
            var name = recorded.Finding.Error.Name.Replace("-", string.Empty);
            Assert.True(wrapper.Replace("-", string.Empty).Contains(name, StringComparison.OrdinalIgnoreCase),
                $"'{recorded.Finding.Error.Name}' is recorded in {recorded.Finding.Error.Document} but the tree does not have it.");
        }
    }
}
