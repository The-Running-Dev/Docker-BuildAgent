namespace Entities;

/// <summary>
/// Specifies the source of the change log in a version control system.
/// </summary>
/// <remarks>This enumeration is used to determine the scope of changes to include in a change log.</remarks>
public enum ChangeLogSource
{
    All = 0,
    LastTag = 1,
    SpecificTag = 2
}