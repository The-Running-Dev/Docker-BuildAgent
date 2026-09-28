using DocsCheck.Tests.TestSupport;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace DocsCheck.Tests;

/// <summary>S10.1: the check runs on every pull request, and a violation fails the job.</summary>
public sealed class PullRequestWorkflowTests
{
    private static YamlMappingNode LoadCi()
    {
        var path = Path.Combine(RepoRootLocator.Find(), ".github", "workflows", "ci.yml");
        using var reader = new StreamReader(path);
        var stream = new YamlStream();
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private static YamlNode Child(YamlMappingNode node, string key) => node.Children[new YamlScalarNode(key)];

    [Fact]
    public void S10_1_CiRunsOnPullRequests()
    {
        var on = (YamlMappingNode)Child(LoadCi(), "on");

        Assert.True(on.Children.ContainsKey(new YamlScalarNode("pull_request")));
    }

    [Fact]
    public void S10_1_CiHasAnUnconditionalJobRunningTheDocsCheck()
    {
        var jobs = (YamlMappingNode)Child(LoadCi(), "jobs");
        var docsJobs = jobs.Children.Where(j => RunsDocsCheck((YamlMappingNode)j.Value)).ToList();

        var job = Assert.Single(docsJobs);
        var mapping = (YamlMappingNode)job.Value;
        Assert.False(mapping.Children.ContainsKey(new YamlScalarNode("if")), "the docs check job must not be conditional");
        Assert.False(mapping.Children.ContainsKey(new YamlScalarNode("continue-on-error")), "a violation must fail the job");

        foreach (var step in ((YamlSequenceNode)Child(mapping, "steps")).Children.Cast<YamlMappingNode>())
        {
            Assert.False(step.Children.ContainsKey(new YamlScalarNode("continue-on-error")));
        }
    }

    private static bool RunsDocsCheck(YamlMappingNode job) =>
        job.Children.TryGetValue(new YamlScalarNode("steps"), out var steps) &&
        ((YamlSequenceNode)steps).Children.Cast<YamlMappingNode>().Any(s =>
            s.Children.TryGetValue(new YamlScalarNode("run"), out var run) &&
            ((YamlScalarNode)run).Value!.Contains("forge/DocsCheck", StringComparison.Ordinal));
}
