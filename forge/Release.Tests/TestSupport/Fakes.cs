#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Surface;

namespace Release.Tests.TestSupport;

/// <summary>
/// An in-memory claim store. Deliberately has no delete/remove operation at all — the interface
/// it implements does not expose one — so "no failure path deletes ... a release" (S3.13) is true
/// by construction for anything built against <see cref="IReleaseClaimStore"/>.
/// </summary>
public sealed class FakeClaimStore : IReleaseClaimStore
{
    private readonly Dictionary<string, ReleaseClaim> _claims = new();
    private readonly Dictionary<string, string> _refs = new();

    public Func<ReleaseVersion, string, string, SurfaceManifest, Exception?>? FailCreateDraft { get; set; }

    public Exception? FailCreateRef { get; set; }

    /// <summary>Runs inside <see cref="CreateClaimRefAsync"/> before the ref is written, so a test can
    /// play a second run that claims the version between this run's check and its claim.</summary>
    public Action<FakeClaimStore>? BeforeCreateRef { get; set; }

    public int DraftsCreated { get; private set; }

    public IReadOnlyDictionary<string, string> Refs => _refs;

    public void SeedRef(ReleaseVersion version, string commitSha) => _refs[version.ToTagString()] = commitSha;

    public Task<string?> FindClaimRefAsync(ReleaseVersion version)
    {
        return Task.FromResult(_refs.TryGetValue(version.ToTagString(), out var sha) ? sha : null);
    }

    public Task<ClaimRef> CreateClaimRefAsync(ReleaseVersion version, string commitSha)
    {
        if (FailCreateRef != null)
        {
            throw FailCreateRef;
        }

        var race = BeforeCreateRef;
        BeforeCreateRef = null;
        race?.Invoke(this);

        if (_refs.TryGetValue(version.ToTagString(), out var existing))
        {
            return Task.FromResult(new ClaimRef(existing, Created: false));
        }

        _refs[version.ToTagString()] = commitSha;
        return Task.FromResult(new ClaimRef(commitSha, Created: true));
    }

    public Exception? FailPublish { get; set; }

    public Action<ReleaseClaim>? OnPublished { get; set; }

    public IReadOnlyDictionary<string, ReleaseClaim> Claims => _claims;

    public void Seed(ReleaseClaim claim) => _claims[claim.Version.ToTagString()] = claim;

    public Task<ReleaseClaim?> FindClaimAsync(ReleaseVersion version)
    {
        return Task.FromResult(_claims.TryGetValue(version.ToTagString(), out var claim) ? claim : null);
    }

    public Task<ReleaseClaim> CreateDraftAsync(ReleaseVersion version, string commitSha, string notes, SurfaceManifest manifest)
    {
        var failure = FailCreateDraft?.Invoke(version, commitSha, notes, manifest);
        if (failure != null)
        {
            throw failure;
        }

        var claim = new ReleaseClaim(version, commitSha, ClaimState.Draft, notes, manifest);
        _claims[version.ToTagString()] = claim;
        DraftsCreated++;
        return Task.FromResult(claim);
    }

    public Task PublishAsync(ReleaseClaim claim)
    {
        if (FailPublish != null)
        {
            throw FailPublish;
        }

        _claims[claim.Version.ToTagString()] = claim with { State = ClaimState.Published };
        OnPublished?.Invoke(claim);
        return Task.CompletedTask;
    }
}

public sealed class FakeImageTagChecker : IImageTagChecker
{
    private readonly HashSet<string> _existingVersions = new();

    public void SeedExisting(ReleaseVersion version) => _existingVersions.Add(version.ToPackageString());

    public Task<bool> ExistsAsync(ReleaseVersion version) => Task.FromResult(_existingVersions.Contains(version.ToPackageString()));
}

public sealed class FakeGitTagChecker : IGitTagChecker
{
    private readonly Dictionary<string, string> _tags = new();

    public void Seed(ReleaseVersion version, string commitSha) => _tags[version.ToTagString()] = commitSha;

    public Task<string?> FindTagCommitAsync(ReleaseVersion version)
    {
        return Task.FromResult(_tags.TryGetValue(version.ToTagString(), out var sha) ? sha : null);
    }
}

public sealed class FakeHighestPublishedVersionSource : IHighestPublishedVersionSource
{
    public ReleaseVersion? Highest { get; set; }

    public Task<ReleaseVersion?> GetHighestPublishedAsync() => Task.FromResult(Highest);
}

public sealed class FakeCiPublishingContext : ICiPublishingContext
{
    public bool IsCiPublishing { get; set; } = true;
}

/// <summary>
/// A sink fake that can be told to fail, and records every version string it was asked to write
/// so tests can assert byte-identical stamping across sinks (S3.7).
/// </summary>
public sealed class FakeSinkPublisher : IReleaseSinkPublisher
{
    public FakeSinkPublisher(ReleaseSink sink)
    {
        Sink = sink;
    }

    public ReleaseSink Sink { get; }

    public bool ShouldFail { get; set; }

    public bool WasCalled { get; private set; }

    public string? RecordedPackageVersion { get; private set; }

    public Task PublishAsync(ReleaseClaim claim)
    {
        WasCalled = true;
        RecordedPackageVersion = claim.Version.ToPackageString();

        if (ShouldFail)
        {
            throw new InvalidOperationException($"{Sink} rejected the write.");
        }

        return Task.CompletedTask;
    }
}

public static class TestManifest
{
    public static SurfaceManifest Empty(string productVersion) =>
        new(1, productVersion, Array.Empty<SurfaceItem>());
}

public static class TestNotes
{
    public const string Valid = """
        # Release notes

        ## 💥 Breaking Changes

        None.

        ## 🗑️ Deprecations

        None.
        """;

    public const string MissingBreakingChanges = """
        # Release notes

        ## 🗑️ Deprecations

        None.
        """;

    public const string MissingDeprecations = """
        # Release notes

        ## 💥 Breaking Changes

        None.
        """;

    public const string MissingBoth = """
        # Release notes

        Nothing structured here.
        """;

    public const string IssueReferencesAreNotHeadings = """
        # Release notes

        #42 Breaking change in the parser
        #43 Deprecation of the old flag
        """;

    public const string EmptySectionsPass = """
        ## Breaking Changes
        ## Deprecations
        """;
}
