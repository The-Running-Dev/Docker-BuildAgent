using System;

namespace Parameters;

/// <summary>
/// Marks a `*Params` property as secret-declared (design/20-contract.md, "Build parameters", I20):
/// rejected when present in the project configuration file, redacted in every display (I21).
/// Named distinctly from Nuke.Common's own `SecretAttribute` (used on Base's NUKE `[Parameter]`
/// fields) since both namespaces are in scope together in build declarations.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SecretParameterAttribute : Attribute
{
}
