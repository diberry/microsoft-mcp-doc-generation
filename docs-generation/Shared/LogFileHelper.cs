// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;

namespace Shared;

/// <summary>
/// Provides thread-safe file logging for verbose debug output.
/// Logs are written to ./generated/logs/ directory to keep console output clean.
/// </summary>
public static class LogFileHelper
{
    private static readonly object _lock = new object();
    private static string _logFilePath;
    private static bool _isInitialized = false;

    /// <summary>
    /// Initializes the log file with a timestamp-based filename.
    /// Should be called once at the start of the application.
    /// </summary>
    /// <param name="outputDir">Base output directory (e.g., ./generated)</param>
    /// <param name="logFilePrefix">Prefix for the log file name (default: "csharp-generator")</param>
    public static void Initialize(string outputDir, string logFilePrefix = "csharp-generator")
    {
        lock (_lock)
        {
            if (_isInitialized)
                return;

            var logsDir = Path.Combine(outputDir, "logs");
            Directory.CreateDirectory(logsDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _logFilePath = Path.Combine(logsDir, $"{logFilePrefix}-{timestamp}.log");

            // Write header
            WriteToFile($"=== Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            WriteToFile($"Log file: {_logFilePath}");
            WriteToFile("");

            _isInitialized = true;
        }
    }

    /// <summary>
    /// Writes a debug message to the log file.
    /// If not initialized, the message is silently dropped.
    /// </summary>
    public static void WriteDebug(string message)
    {
        if (!_isInitialized)
            return;

        WriteToFile($"[DEBUG] {message}");
    }

    /// <summary>
    /// Writes an info message to the log file.
    /// If not initialized, the message is silently dropped.
    /// </summary>
    public static void WriteInfo(string message)
    {
        if (!_isInitialized)
            return;

        WriteToFile($"[INFO] {message}");
    }

    /// <summary>
    /// Writes a warning message to the log file.
    /// If not initialized, the message is silently dropped.
    /// </summary>
    public static void WriteWarning(string message)
    {
        if (!_isInitialized)
            return;

        WriteToFile($"[WARN] {message}");
    }

    /// <summary>
    /// Writes an error message to the log file.
    /// If not initialized, the message is silently dropped.
    /// </summary>
    public static void WriteError(string message)
    {
        if (!_isInitialized)
            return;

        WriteToFile($"[ERROR] {message}");
    }

    /// <summary>
    /// Returns the current log file path, or null if not initialized.
    /// </summary>
    public static string GetLogFilePath()
    {
        return _logFilePath;
    }

    /// <summary>
    /// Returns whether the logger has been initialized.
    /// </summary>
    public static bool IsInitialized()
    {
        return _isInitialized;
    }

    /// <summary>
    /// Thread-safe file write operation.
    /// </summary>
    private static void WriteToFile(string message)
    {
        if (string.IsNullOrEmpty(_logFilePath))
            return;

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, message + Environment.NewLine);
            }
            catch
            {
                // Silently fail to avoid disrupting the main application
            }
        }
    }

    /// <summary>
    /// Resets the logger state (useful for testing).
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _logFilePath = null;
            _isInitialized = false;
        }
    }
}
