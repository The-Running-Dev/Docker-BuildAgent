#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Surface.SurfaceDerivation;

/// <summary>
/// Derives one ModuleCommand item per exported PowerShell module member (each exported function
/// and the exported `BuildAgentConfig` variable, since SurfaceItemKind has no dedicated "module
/// variable" kind) and one ModuleParameter item per parameter of each exported function.
/// Exported names come from the .psd1 manifest (FunctionsToExport/VariablesToExport - simpler
/// and structurally equivalent to the .psm1's own Export-ModuleMember call); each function's
/// parameter list is parsed from the .psm1 source text rather than by loading the module in a
/// live PowerShell process, for the same determinism reason as BuildTypeDeriver (S1.1).
/// </summary>
internal static class ModuleSurfaceDeriver
{
    public static List<SurfaceItem> Derive(string rootDirectory)
    {
        var moduleDir = Path.Combine(rootDirectory, "scripts", "powershell-module");
        var psd1Path = Path.Combine(moduleDir, "Docker-BuildAgent.psd1");
        var psm1Path = Path.Combine(moduleDir, "Docker-BuildAgent.psm1");

        if (!File.Exists(psd1Path) || !File.Exists(psm1Path))
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"PowerShell module files not found under '{moduleDir}'.");
        }

        var psd1 = File.ReadAllText(psd1Path);
        var psm1 = File.ReadAllText(psm1Path);

        var functions = ExtractQuotedList(psd1, "FunctionsToExport");
        var variables = ExtractQuotedList(psd1, "VariablesToExport");

        var items = new List<SurfaceItem>();

        foreach (var variable in variables)
        {
            items.Add(new SurfaceItem(SurfaceItemKind.ModuleCommand, variable, "exported", null, null));
        }

        foreach (var function in functions)
        {
            items.Add(new SurfaceItem(SurfaceItemKind.ModuleCommand, function, "exported", null, null));

            foreach (var parameter in ExtractFunctionParameters(psm1, function))
            {
                var value = $"{parameter.Type}|{parameter.Default ?? string.Empty}";
                items.Add(new SurfaceItem(SurfaceItemKind.ModuleParameter, $"{function}.{parameter.Name}", value, null, null));
            }
        }

        return items;
    }

    private static List<string> ExtractQuotedList(string source, string arrayName)
    {
        var match = Regex.Match(source, $@"{arrayName}\s*=\s*@\(([\s\S]*?)\)");
        if (!match.Success)
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"'{arrayName}' not found in the module manifest.");
        }

        return Regex.Matches(match.Groups[1].Value, @"'([^']*)'")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    private static List<PowerShellParamBlockParser.ParsedParameter> ExtractFunctionParameters(string source, string functionName)
    {
        var functionMatch = Regex.Match(source, $@"\bfunction\s+{Regex.Escape(functionName)}\b");
        if (!functionMatch.Success)
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"Function '{functionName}' not found in the module source.");
        }

        var openBraceIndex = source.IndexOf('{', functionMatch.Index);
        if (openBraceIndex < 0)
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"Function '{functionName}' has no body.");
        }

        var depth = 0;
        var closeBraceIndex = -1;
        for (var i = openBraceIndex; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    closeBraceIndex = i;
                    break;
                }
            }
        }

        if (closeBraceIndex < 0)
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"Function '{functionName}' has an unbalanced body.");
        }

        var body = source.Substring(openBraceIndex, closeBraceIndex - openBraceIndex + 1);

        if (!Regex.IsMatch(body, @"\bparam\s*\("))
        {
            return new List<PowerShellParamBlockParser.ParsedParameter>();
        }

        var paramBlockBody = PowerShellParamBlockParser.ExtractParamBlock(body);
        return PowerShellParamBlockParser.SplitTopLevelParameters(paramBlockBody)
            .Select(PowerShellParamBlockParser.ParseParameter)
            .ToList();
    }
}
