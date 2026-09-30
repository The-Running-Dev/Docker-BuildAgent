using System;
using System.Collections.Generic;

namespace Utilities;

/// <summary>
/// What a build asks of the project's configuration file before it runs anything (I18). Declared in
/// Common because Config depends on Common; each build type's entry point supplies the Config-backed
/// implementation.
/// </summary>
public interface IBuildConfigurationGate
{
    /// <summary>
    /// Resolves the project's configuration. Reports every error through <paramref name="writeError"/>
    /// (file, key and rule — never a value) and returns an outcome that is not valid when any were found.
    /// </summary>
    /// <param name="projectRoot">The project root the configuration file is discovered in.</param>
    /// <param name="invocationArguments">The command-line arguments of this run.</param>
    /// <param name="mappingFileEnvironment">Parameter name to value, as the mapping file resolves it.</param>
    /// <param name="writeError">Receives one line per configuration error.</param>
    BuildConfigurationOutcome Resolve(
        string projectRoot,
        IReadOnlyList<string> invocationArguments,
        IReadOnlyDictionary<string, string> mappingFileEnvironment,
        Action<string> writeError);
}

/// <summary>
/// The result of <see cref="IBuildConfigurationGate.Resolve"/>.
/// </summary>
/// <param name="IsValid">False when the configuration is invalid and the build must exit with status 2.</param>
/// <param name="ProjectFileValues">Parameter name to value for every parameter whose effective value comes
/// from the project file (and so is not already supplied by a higher tier).</param>
public record BuildConfigurationOutcome(bool IsValid, IReadOnlyDictionary<string, string> ProjectFileValues);
