#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Surface;

/// <summary>Reads the manifest asset attached to the highest published release, as the S1.11 baseline.</summary>
public interface IBaselineManifestSource
{
    /// <summary>Returns the baseline manifest JSON, or null when no baseline release has a manifest asset.</summary>
    Task<string?> GetLatestManifestJsonAsync();
}

public sealed record SurfaceGateResult(
    bool Success,
    SurfaceManifest Candidate,
    SurfaceManifest? Baseline,
    SurfaceComparison? Comparison,
    SurfaceErrorCode? ErrorCode,
    string Message);

/// <summary>
/// Derives the candidate manifest, fetches the baseline, compares them, and decides whether the
/// release may proceed. Never writes a claim or publishes to any sink (S3's scope) — S1 stops at
/// pass/fail plus the candidate manifest (S1.12: any blocking difference exits non-zero before
/// any claim/sink write).
/// </summary>
public static class SurfaceGate
{
    public static async Task<SurfaceGateResult> EvaluateAsync(string rootDirectory, string productVersion, IBaselineManifestSource baselineSource)
    {
        SurfaceManifest candidate;
        try
        {
            candidate = SurfaceDeriver.Derive(rootDirectory, productVersion);
        }
        catch (SurfaceException ex)
        {
            return Failed(null, null, null, ex.Code, ex.Message);
        }

        string? baselineJson;
        try
        {
            baselineJson = await baselineSource.GetLatestManifestJsonAsync();
        }
        catch (SurfaceException ex)
        {
            return Failed(candidate, null, null, ex.Code, ex.Message);
        }

        if (baselineJson == null)
        {
            return Failed(candidate, null, null, SurfaceErrorCode.BaselineMissing,
                "The baseline release has no surface-manifest.json asset.");
        }

        SurfaceManifest baseline;
        try
        {
            baseline = SurfaceManifestSerializer.Deserialize(baselineJson);
        }
        catch (SurfaceException ex)
        {
            return Failed(candidate, null, null, ex.Code, ex.Message);
        }

        SurfaceComparison comparison;
        try
        {
            comparison = SurfaceComparer.Compare(baseline, candidate);
        }
        catch (SurfaceException ex)
        {
            return Failed(candidate, baseline, null, ex.Code, ex.Message);
        }

        if (comparison.Blocking.Count > 0)
        {
            return Failed(candidate, baseline, comparison, SurfaceErrorCode.BlockingDifference, DescribeBlocking(comparison));
        }

        return new SurfaceGateResult(true, candidate, baseline, comparison, null, "No blocking differences.");
    }

    /// <summary>Evaluates the gate and throws SurfaceException when it does not pass, for direct NUKE target wiring.</summary>
    public static async Task<SurfaceGateResult> RunOrThrowAsync(string rootDirectory, string productVersion, IBaselineManifestSource baselineSource)
    {
        var result = await EvaluateAsync(rootDirectory, productVersion, baselineSource);
        if (!result.Success)
        {
            throw new SurfaceException(result.ErrorCode ?? SurfaceErrorCode.BlockingDifference, result.Message);
        }

        return result;
    }

    private static SurfaceGateResult Failed(SurfaceManifest? candidate, SurfaceManifest? baseline, SurfaceComparison? comparison, SurfaceErrorCode code, string message)
    {
        return new SurfaceGateResult(false, candidate!, baseline, comparison, code, message);
    }

    private static string DescribeBlocking(SurfaceComparison comparison)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{comparison.Blocking.Count} blocking surface difference(s):");

        foreach (var d in comparison.Blocking)
        {
            builder.AppendLine($"- [{d.Kind}] {d.ItemKind} '{d.Name}': baseline='{d.BaselineValue}', candidate='{d.CandidateValue}' (not in the compatible set {{ItemAdded, DeprecationAdded}} and no major version increase)");
        }

        return builder.ToString();
    }
}
