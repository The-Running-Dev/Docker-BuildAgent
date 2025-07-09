public record GitVersionInfo
{
    public string SemVer { get; init; }
    
    public string? FullSemVer { get; init; }
    
    public string? CommitDate { get; init; }

    public string? Sha { get; init; }
}