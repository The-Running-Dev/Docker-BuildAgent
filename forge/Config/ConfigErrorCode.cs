namespace Config;

/// <summary>
/// Config's error codes (design/20-contract.md, "Error semantics" § Config). Every code exits 2
/// and runs no build step (S6.14); none are retryable except <see cref="FileUnreadable"/>.
/// </summary>
public enum ConfigErrorCode
{
    MultipleConfigurationFiles,
    FileUnreadable,
    MalformedDocument,
    SchemaVersionMissing,
    SchemaVersionUnsupported,
    UnknownBuildType,
    UnknownKey,
    SecretKeyRejected,
    ValueTypeMismatch,
    RequiredValueMissing,
}
