using System.Text.RegularExpressions;

namespace Config;

/// <summary>
/// The same PascalCase/camelCase-to-kebab-case conversion the PowerShell module already applies
/// (PSModule.requirements.md R-INVOKE-003, `scripts/powershell-module/Docker-BuildAgent.psm1`'s
/// `Convert-ToKebabCase`), ported so a project file's `parameters:` keys are checked against the
/// same one accepted spelling (design/20-contract.md, "Project configuration file").
/// </summary>
internal static class KebabCase
{
    public static string FromPropertyName(string propertyName)
    {
        return Regex.Replace(propertyName, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }
}
