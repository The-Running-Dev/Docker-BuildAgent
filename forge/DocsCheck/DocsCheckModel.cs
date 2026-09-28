#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocsCheck;

public enum DocsCheckErrorCode
{
    UnknownName,
    CanonicalSourceMissing,
    CanonicalSourceConflict,
}

/// <summary>One violation, in the shape design/20-contract.md § Docs check fixes.</summary>
public sealed record DocsCheckError(DocsCheckErrorCode Code, string Document, string Name, string Message);

/// <summary>A violation and the line it was found on, so the PR check can list document, line and name.</summary>
public sealed record DocsCheckFinding(DocsCheckError Error, int Line)
{
    public override string ToString() => $"{Error.Document}:{Line}: {Error.Code} '{Error.Name}' - {Error.Message}";
}

/// <summary>A finding set aside on purpose: it stays visible, with the reason it was recorded.</summary>
public sealed record RecordedFinding(DocsCheckFinding Finding, string Reason);

public sealed record DocsCheckReport(
    IReadOnlyList<string> Documents,
    IReadOnlyList<DocsCheckFinding> Findings,
    IReadOnlyList<string> NotCovered,
    IReadOnlyList<RecordedFinding> Recorded,
    IReadOnlyList<string> StaleRecords)
{
    public bool Success => Findings.Count == 0;

    public string Render()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Docs check: {Documents.Count} document(s) checked, {Findings.Count} finding(s).");

        foreach (var finding in Findings)
        {
            builder.AppendLine(finding.ToString());
        }

        if (Recorded.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Recorded, not failing ({Recorded.Count}):");
            foreach (var record in Recorded)
            {
                builder.AppendLine($"{record.Finding} [recorded: {record.Reason}]");
            }
        }

        foreach (var stale in StaleRecords)
        {
            builder.AppendLine($"Stale record (matches no finding, remove it): {stale}");
        }

        builder.AppendLine();
        builder.AppendLine("Not covered by this check (no claim is made about these):");
        foreach (var claim in NotCovered)
        {
            builder.AppendLine($"- {claim}");
        }

        return builder.ToString();
    }
}
