#nullable enable

using System.Collections.Generic;

namespace Surface;

public enum SurfaceItemKind
{
    BuildType,
    BuildParameter,
    ConfigKey,
    ConfigSchemaVersion,
    ToolCommand,
    ToolParameter,
    ModuleCommand,
    ModuleParameter,
    TemplateLocation,
    ImageInvocation,
    ImageMountPoint,
    ImageEnvironmentInput,
}

public sealed record SurfaceItem(
    SurfaceItemKind Kind,
    string Name,
    string Value,
    string? DeprecatedSince,
    string? RemoveIn);

public sealed record SurfaceManifest(
    int ManifestSchemaVersion,
    string ProductVersion,
    IReadOnlyList<SurfaceItem> Items);

public enum SurfaceDifferenceKind
{
    ItemAdded,
    ItemRemoved,
    ValueChanged,
    DeprecationAdded,
    DeprecationRemoved,
    RemovalTargetChanged,
}

public sealed record SurfaceDifference(
    SurfaceDifferenceKind Kind,
    SurfaceItemKind ItemKind,
    string Name,
    string? BaselineValue,
    string? CandidateValue);

public sealed record SurfaceComparison(
    IReadOnlyList<SurfaceDifference> All,
    IReadOnlyList<SurfaceDifference> Blocking);
