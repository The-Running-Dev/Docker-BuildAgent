#nullable enable

using System;
using System.IO;
using System.Reflection;

using Xunit;
using Microsoft.Extensions.DependencyInjection;

using Entities;
using Parameters;
using Services;
using Notifications;
using DependencyInjection;

namespace Common.Tests.Build;

/// <summary>
/// Unit tests for the <c>Base&lt;TParams, TNotifications&gt;</c> abstract class.
/// <para>
/// Because <c>Base</c> extends <c>NukeBuild</c> and cannot be safely instantiated outside a
/// Nuke build execution, its private static method <c>GetGitRepositorySafely</c> is exercised
/// via reflection.  The DI-registration behaviour exposed by <c>InitializeDependencyInjection</c>
/// is covered through <see cref="ServiceCollectionExtensions"/> which mirrors that method exactly.
/// </para>
/// </summary>
public class BaseTests : IDisposable
{
    private readonly string _tempDir;

    // Closed-generic type used to locate private static methods declared on the base class.
    private static readonly Type ClosedBaseType = typeof(Base<ForgeParams, NoNotifications>);

    private static readonly MethodInfo GetGitRepositorySafelyMethod =
        ClosedBaseType.GetMethod(
            "GetGitRepositorySafely",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
        ?? throw new MissingMethodException("Base<ForgeParams, NoNotifications>", "GetGitRepositorySafely");

    public BaseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BaseTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // ─── GetGitRepositorySafely: directory with no .git folder ───────────────

    [Fact]
    public void GetGitRepositorySafely_WhenNoGitDirectory_DoesNotThrow()
    {
        var ex = Record.Exception(() => InvokeGetGitRepositorySafely(_tempDir));

        Assert.Null(ex);
    }

    [Fact]
    public void GetGitRepositorySafely_WhenNoGitDirectory_ReturnsNull()
    {
        var result = InvokeGetGitRepositorySafely(_tempDir);

        Assert.Null(result);
    }

    // ─── GetGitRepositorySafely: directory does not exist ────────────────────

    [Fact]
    public void GetGitRepositorySafely_WhenDirectoryDoesNotExist_DoesNotThrow()
    {
        var nonExistent = Path.Combine(_tempDir, "does-not-exist");

        var ex = Record.Exception(() => InvokeGetGitRepositorySafely(nonExistent));

        Assert.Null(ex);
    }

    [Fact]
    public void GetGitRepositorySafely_WhenDirectoryDoesNotExist_ReturnsNull()
    {
        var nonExistent = Path.Combine(_tempDir, "does-not-exist");

        var result = InvokeGetGitRepositorySafely(nonExistent);

        Assert.Null(result);
    }

    // ─── GetGitRepositorySafely: .git present but no commits ─────────────────

    [Fact]
    public void GetGitRepositorySafely_WhenGitDirHasNoCommits_DoesNotThrow()
    {
        var repoDir = Path.Combine(_tempDir, "empty-repo");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));

        var ex = Record.Exception(() => InvokeGetGitRepositorySafely(repoDir));

        Assert.Null(ex);
    }

    [Fact]
    public void GetGitRepositorySafely_WhenGitDirHasNoCommits_ReturnsNull()
    {
        var repoDir = Path.Combine(_tempDir, "empty-repo");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));

        var result = InvokeGetGitRepositorySafely(repoDir);

        Assert.Null(result);
    }

    // ─── GetGitRepositorySafely: duplicate key in git config ─────────────────
    // Mirrors the real-world scenario where `gh pr checkout` writes duplicate
    // `github-pr-owner-number` entries inside a [remote "origin"] section.
    // Nuke's config parser throws ArgumentException("An item with the same key
    // has already been added.  Key: github-pr-owner-number") in that case.

    [Fact]
    public void GetGitRepositorySafely_WhenGitConfigHasDuplicateKey_DoesNotThrow()
    {
        var repoDir = CreateRepoWithDuplicateConfigKey();

        var ex = Record.Exception(() => InvokeGetGitRepositorySafely(repoDir));

        Assert.Null(ex);
    }

    [Fact]
    public void GetGitRepositorySafely_WhenGitConfigHasDuplicateKey_ReturnsNull()
    {
        var repoDir = CreateRepoWithDuplicateConfigKey();

        var result = InvokeGetGitRepositorySafely(repoDir);

        Assert.Null(result);
    }

    // ─── AddForgeServices / AddNotificationServices ──────────────────────────
    // These mirror what InitializeDependencyInjection does inside Base<T,T>.

    [Fact]
    public void AddForgeServices_RegistersGitService()
    {
        var provider = new ServiceCollection()
            .AddForgeServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<GitService>());
    }

    [Fact]
    public void AddForgeServices_RegistersGitHubService()
    {
        var provider = new ServiceCollection()
            .AddForgeServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<GitHubService>());
    }

    [Fact]
    public void AddForgeServices_RegistersIGitServiceInterface()
    {
        var provider = new ServiceCollection()
            .AddForgeServices()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<IGitService>());
    }

    [Fact]
    public void AddForgeServices_IGitServiceAndGitService_AreSameInstance()
    {
        var provider = new ServiceCollection()
            .AddForgeServices()
            .BuildServiceProvider();

        var concrete = provider.GetService<GitService>();
        var iface = provider.GetService<IGitService>();

        Assert.Same(concrete, iface);
    }

    [Fact]
    public void AddNotificationServices_RegistersConcreteType()
    {
        var provider = new ServiceCollection()
            .AddForgeServices()
            .AddNotificationServices<NoNotifications>()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<NoNotifications>());
    }

    [Fact]
    public void AddNotificationServices_RegistersINotificationsAsConcreteType()
    {
        var provider = new ServiceCollection()
            .AddForgeServices()
            .AddNotificationServices<NoNotifications>()
            .BuildServiceProvider();

        var service = provider.GetService<INotifications>();

        Assert.NotNull(service);
        Assert.IsType<NoNotifications>(service);
    }

    [Fact]
    public void AddNotificationServices_BothRegistrationsBehaveAsNoNotifications()
    {
        var provider = new ServiceCollection()
            .AddForgeServices()
            .AddNotificationServices<NoNotifications>()
            .BuildServiceProvider();

        var concrete = provider.GetService<NoNotifications>();
        var iface = provider.GetService<INotifications>();

        // Both registrations are valid instances of NoNotifications.
        Assert.NotNull(concrete);
        Assert.IsType<NoNotifications>(iface);
    }

    [Fact]
    public void CreateForgeServiceProvider_ReturnsNonNullProvider()
    {
        var provider = ServiceCollectionExtensions.CreateForgeServiceProvider<NoNotifications>();

        Assert.NotNull(provider);
    }

    [Fact]
    public void CreateForgeServiceProvider_CanResolveGitService()
    {
        var provider = ServiceCollectionExtensions.CreateForgeServiceProvider<NoNotifications>();

        Assert.NotNull(provider.GetService<GitService>());
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Invokes the private static <c>GetGitRepositorySafely</c> method and unwraps any
    /// <see cref="TargetInvocationException"/> so test assertions see the real exception.
    /// </summary>
    private static object? InvokeGetGitRepositorySafely(string directory)
    {
        try
        {
            return GetGitRepositorySafelyMethod.Invoke(null, new object[] { directory });
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(tie.InnerException)
                .Throw();

            return null; // unreachable
        }
    }

    /// <summary>
    /// Creates a synthetic git repository whose config contains a duplicate key within the
    /// same section — the exact pattern written by <c>gh pr checkout</c> that triggers the
    /// "An item with the same key has already been added. Key: github-pr-owner-number"
    /// exception in Nuke's config parser.
    /// </summary>
    private string CreateRepoWithDuplicateConfigKey()
    {
        var repoDir = Path.Combine(_tempDir, "dupe-key-repo");
        var gitDir = Path.Combine(repoDir, ".git");
        Directory.CreateDirectory(gitDir);

        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main\n");
        File.WriteAllText(Path.Combine(gitDir, "config"),
            "[core]\n" +
            "\trepositoryformatversion = 0\n" +
            "\tfilemode = false\n" +
            "[remote \"origin\"]\n" +
            "\turl = https://github.com/test/repo.git\n" +
            "\tfetch = +refs/heads/*:refs/remotes/origin/*\n" +
            "\tgithub-pr-owner-number = 17\n" +   // duplicate key — same as real scenario
            "\tgithub-pr-owner-number = 18\n" +
            "[branch \"main\"]\n" +
            "\tremote = origin\n" +
            "\tmerge = refs/heads/main\n");

        return repoDir;
    }
}
