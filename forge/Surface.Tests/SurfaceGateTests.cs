using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

using Surface.Tests.TestSupport;

namespace Surface.Tests;

public class SurfaceGateTests
{
    private sealed class FakeBaselineSource : IBaselineManifestSource
    {
        private readonly string? _json;
        private readonly SurfaceException? _throws;

        private FakeBaselineSource(string? json, SurfaceException? throws)
        {
            _json = json;
            _throws = throws;
        }

        public static FakeBaselineSource Returning(string? json) => new(json, null);

        public static FakeBaselineSource Throwing(SurfaceException ex) => new(null, ex);

        public Task<string?> GetLatestManifestJsonAsync()
        {
            if (_throws != null)
            {
                throw _throws;
            }

            return Task.FromResult(_json);
        }
    }

    // S1.11: no manifest asset on the baseline release fails closed with BaselineMissing, not an
    // empty-baseline pass.
    [Fact]
    public async Task EvaluateAsync_NoBaselineManifestAsset_FailsClosedWithBaselineMissing()
    {
        using var root = new TempScriptRoot();
        var source = FakeBaselineSource.Returning(null);

        var result = await SurfaceGate.EvaluateAsync(root.RootDirectory, "2.0.0", source);

        Assert.False(result.Success);
        Assert.Equal(SurfaceErrorCode.BaselineMissing, result.ErrorCode);
    }

    // S1.11 (unreadable baseline): a baseline manifest that fails to parse fails closed with
    // BaselineUnreadable rather than being treated as an empty baseline.
    [Fact]
    public async Task EvaluateAsync_UnreadableBaselineManifest_FailsClosedWithBaselineUnreadable()
    {
        using var root = new TempScriptRoot();
        var source = FakeBaselineSource.Returning("not valid json");

        var result = await SurfaceGate.EvaluateAsync(root.RootDirectory, "2.0.0", source);

        Assert.False(result.Success);
        Assert.Equal(SurfaceErrorCode.BaselineUnreadable, result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateAsync_BaselineSourceThrowsSurfaceException_PropagatesItsErrorCode()
    {
        using var root = new TempScriptRoot();
        var source = FakeBaselineSource.Throwing(new SurfaceException(SurfaceErrorCode.BaselineUnreadable, "network fetch failed"));

        var result = await SurfaceGate.EvaluateAsync(root.RootDirectory, "2.0.0", source);

        Assert.False(result.Success);
        Assert.Equal(SurfaceErrorCode.BaselineUnreadable, result.ErrorCode);
    }

    // S1.12: any blocking difference exits the gate non-zero (Success = false) before any claim/sink
    // write, and the message names every blocking difference.
    [Fact]
    public async Task EvaluateAsync_BlockingDifference_FailsAndMessageNamesEachDifference()
    {
        using var root = new TempScriptRoot();
        var candidate = SurfaceDeriver.Derive(root.RootDirectory, "2.0.0");

        var mutatedItems = new List<SurfaceItem>(candidate.Items);
        var removed = mutatedItems[0];
        mutatedItems.RemoveAt(0);
        mutatedItems.Add(removed with { Value = removed.Value + "-renamed-baseline-only" });

        var baseline = new SurfaceManifest(candidate.ManifestSchemaVersion, "2.0.0", mutatedItems);
        var source = FakeBaselineSource.Returning(SurfaceManifestSerializer.Serialize(baseline));

        var result = await SurfaceGate.EvaluateAsync(root.RootDirectory, "2.0.0", source);

        Assert.False(result.Success);
        Assert.Equal(SurfaceErrorCode.BlockingDifference, result.ErrorCode);
        Assert.NotEmpty(result.Comparison!.Blocking);
        foreach (var diff in result.Comparison.Blocking)
        {
            Assert.Contains(diff.Name, result.Message);
        }
    }

    [Fact]
    public async Task EvaluateAsync_NoBlockingDifferences_Succeeds()
    {
        using var root = new TempScriptRoot();
        var candidate = SurfaceDeriver.Derive(root.RootDirectory, "2.0.0");
        var source = FakeBaselineSource.Returning(SurfaceManifestSerializer.Serialize(candidate));

        var result = await SurfaceGate.EvaluateAsync(root.RootDirectory, "2.0.0", source);

        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task RunOrThrowAsync_FailedGate_ThrowsSurfaceExceptionWithMatchingCode()
    {
        using var root = new TempScriptRoot();
        var source = FakeBaselineSource.Returning(null);

        var ex = await Assert.ThrowsAsync<SurfaceException>(() => SurfaceGate.RunOrThrowAsync(root.RootDirectory, "2.0.0", source));

        Assert.Equal(SurfaceErrorCode.BaselineMissing, ex.Code);
    }
}
