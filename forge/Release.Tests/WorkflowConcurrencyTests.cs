using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;
using YamlDotNet.RepresentationModel;

namespace Release.Tests;

/// <summary>
/// S3.11: A single CI concurrency group spans every publishing workflow and queues rather than
/// cancels, so at most one run publishes at a time (I13). Parses the actual workflow YAML files
/// rather than re-asserting a hard-coded expectation, so an edit to any one file that breaks the
/// shared group is caught here.
/// </summary>
public sealed class WorkflowConcurrencyTests
{
    private static readonly string[] PublishingWorkflowFileNames =
    {
        "build.yml",
        "release.yml",
        "release-tag.yml",
    };

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

    private static (string FileName, string? Group, bool? CancelInProgress) ReadConcurrency(string filePath)
    {
        using var reader = new StreamReader(filePath);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;

        if (!root.Children.TryGetValue(new YamlScalarNode("concurrency"), out var concurrencyNode))
        {
            return (Path.GetFileName(filePath), null, null);
        }

        var concurrencyMapping = (YamlMappingNode)concurrencyNode;

        string? group = concurrencyMapping.Children.TryGetValue(new YamlScalarNode("group"), out var groupNode)
            ? ((YamlScalarNode)groupNode).Value
            : null;

        bool? cancelInProgress = null;
        if (concurrencyMapping.Children.TryGetValue(new YamlScalarNode("cancel-in-progress"), out var cancelNode))
        {
            cancelInProgress = bool.Parse(((YamlScalarNode)cancelNode).Value!);
        }

        return (Path.GetFileName(filePath), group, cancelInProgress);
    }

    [Fact]
    public void EveryPublishingWorkflow_DeclaresAConcurrencyGroup()
    {
        var root = RepoRoot();

        var results = PublishingWorkflowFileNames
            .Select(name => ReadConcurrency(Path.Combine(root, ".github", "workflows", name)))
            .ToList();

        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Group), $"{r.FileName} has no concurrency.group."));
    }

    [Fact]
    public void EveryPublishingWorkflow_SharesTheSameConcurrencyGroupName()
    {
        var root = RepoRoot();

        var groups = PublishingWorkflowFileNames
            .Select(name => ReadConcurrency(Path.Combine(root, ".github", "workflows", name)).Group)
            .ToList();

        Assert.Single(groups.Distinct());
    }

    [Fact]
    public void EveryPublishingWorkflow_QueuesRatherThanCancels()
    {
        var root = RepoRoot();

        var results = PublishingWorkflowFileNames
            .Select(name => ReadConcurrency(Path.Combine(root, ".github", "workflows", name)))
            .ToList();

        Assert.All(results, r => Assert.False(r.CancelInProgress, $"{r.FileName} must set cancel-in-progress: false."));
    }
}
