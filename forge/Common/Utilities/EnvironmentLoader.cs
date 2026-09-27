using DotNetEnv;

namespace Utilities;

/// <summary>
/// Loads a `.env` file into the process environment without overwriting a variable the process
/// already has set (I22 — a mapping-file-derived environment must not overwrite process
/// environment; design/30-slices.md § S7).
/// </summary>
public static class EnvironmentLoader
{
    public static void LoadWithoutClobbering(string path)
    {
        Env.Load(path, new LoadOptions(clobberExistingVars: false));
    }
}
