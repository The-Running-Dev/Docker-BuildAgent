using System;

using Microsoft.Extensions.Logging;

using Nuke.Common;

namespace Extensions;

/// <summary>
/// Extension methods for logging that provide consistent formatting across the Forge build system.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs an informational message with the [OK] status indicator.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void Ok<T>(this ILogger<T> logger, string message, params object[] args)
    {
        logger.LogInformation($"[OK] {message}", args);
    }

    /// <summary>
    /// Logs an error message with the [ERROR] status indicator.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void ErrorStatus<T>(this ILogger<T> logger, string message, params object[] args)
    {
        logger.LogError($"[ERROR] {message}", args);
    }

    /// <summary>
    /// Logs an error message with the [ERROR] status indicator and exception.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void ErrorStatus<T>(this ILogger<T> logger, Exception exception, string message, params object[] args)
    {
        logger.LogError(exception, $"[ERROR] {message}", args);
    }

    /// <summary>
    /// Logs a warning message with the [WARN] status indicator.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void WarnStatus<T>(this ILogger<T> logger, string message, params object[] args)
    {
        logger.LogWarning($"[WARN] {message}", args);
    }

    /// <summary>
    /// Logs an informational message with the [TAG] status indicator.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void Tag<T>(this ILogger<T> logger, string message, params object[] args)
    {
        logger.LogInformation($"[TAG] {message}", args);
    }

    /// <summary>
    /// Logs an informational message with the [PUSH] status indicator.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void Push<T>(this ILogger<T> logger, string message, params object[] args)
    {
        logger.LogInformation($"[PUSH] {message}", args);
    }

    /// <summary>
    /// Logs an informational message with the [COPY] status indicator.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void Copy<T>(this ILogger<T> logger, string message, params object[] args)
    {
        logger.LogInformation($"[COPY] {message}", args);
    }

    /// <summary>
    /// Logs an informational message with a custom status indicator.
    /// </summary>
    /// <typeparam name="T">The type of the logger.</typeparam>
    /// <param name="logger">The logger instance.</param>
    /// <param name="status">The status indicator (without brackets).</param>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional message formatting arguments.</param>
    public static void WithStatus<T>(this ILogger<T> logger, string status, string message, params object[] args)
    {
        logger.LogInformation($"[{status}] {message}", args);
    }
}