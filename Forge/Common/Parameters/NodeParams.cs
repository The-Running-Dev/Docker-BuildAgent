namespace Parameters;

/// <summary>
/// Represents the parameters used for configuring a node, including the directory path for storing artifacts.
/// </summary>
public class NodeParams : ForgeParams
{
    /// <summary>
    /// Gets or sets the directory path where artifacts are stored.
    /// </summary>
    public string ArtifactsDir { get; set; } = "artifacts";
}