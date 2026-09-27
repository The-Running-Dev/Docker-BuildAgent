namespace Config;

/// <summary>
/// Precedence tiers a resolved value may come from (design/20-contract.md, "Project configuration
/// and precedence"). Numeric values are the precedence order, lowest wins. S6 only produces values
/// at <see cref="ProjectConfigurationFile"/> and <see cref="DeclaredDefault"/>; the remaining tiers
/// are S7's scope.
/// </summary>
public enum ConfigurationTier
{
    InvocationArgument = 1,
    ModuleConfiguration = 2,
    ProcessEnvironment = 3,
    MappingFileEnvironment = 4,
    ProjectConfigurationFile = 5,
    DeclaredDefault = 6,
}
