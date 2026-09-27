namespace Config;

/// <summary>
/// One Config error (design/20-contract.md, "Error semantics" § Config).
/// </summary>
public sealed record ConfigError(ConfigErrorCode Code, string? File, string? Key, string Message);
