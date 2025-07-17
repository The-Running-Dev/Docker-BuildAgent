namespace Parameters;

/// <summary>
/// Represents the parameters required for configuring a node, including the directory path for storing artifacts.
/// </summary>
/// <remarks>This class extends <see cref="ForgeParams"/> to include additional settings specific to node
/// configuration.</remarks>
public class NodeParams : ForgeParams
{
    /// <summary>
    /// Gets or sets the directory path where artifacts are stored.
    /// </summary>
    public string ArtifactsDirectory { get; set; }
}