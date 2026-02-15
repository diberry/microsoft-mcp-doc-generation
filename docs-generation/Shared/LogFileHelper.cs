using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shared;

/// <summary>
/// Helper for writing debug output to log files in the generated/logs directory.
/// Provides centralized logging for verbose debug information that should not clutter console output.
/// </summary>
public static class LogFileHelper
{
    private static readonly object _lock = new object();
    private static string? _logFilePath;
    
    /// <summary>
    /// Gets or creates the log file path for debug output.
    /// Default location: {outputPath}/logs/debug-{timestamp}.log
    /// </summary>
    private static string GetLogFilePath()
    {
        if (_logFilePath != null)
            return _logFilePath;
            
        lock (_lock)
        {
            if (_logFilePath != null)
                return _logFilePath;
                
            // Try to determine output directory from common locations
            var currentDir = Directory.GetCurrentDirectory();
            var searchPaths = new[]
            {
                Path.Combine(currentDir, "..", "..", "generated", "logs"),  // From bin/Debug or bin/Release
                Path.Combine(currentDir, "..", "generated", "logs"),         // From docs-generation subdirectory
                Path.Combine(currentDir, "generated", "logs"),               // From docs-generation root
                Path.Combine(currentDir, "..", "..", "..", "generated", "logs") // From deeper nested paths
            };
            
            string? logDir = null;
            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                var parentDir = Path.GetDirectoryName(fullPath);
                // Check if parent directory exists, or if its parent exists (to handle paths like ../generated/logs)
                if (parentDir != null && Directory.Exists(parentDir))
                {
                    logDir = fullPath;
                    break;
                }
            }
            
            // Fallback to current directory if we can't find generated/logs
            logDir ??= Path.Combine(currentDir, "logs");
            
            // Ensure directory exists
            Directory.CreateDirectory(logDir);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _logFilePath = Path.Combine(logDir, $"debug-{timestamp}.log");
            
            return _logFilePath;
        }
    }
    
    /// <summary>
    /// Writes a debug message to the log file with a timestamp.
    /// Thread-safe for concurrent writes.
    /// </summary>
    public static void WriteDebug(string message)
    {
        try
        {
            var logPath = GetLogFilePath();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\n";
            
            lock (_lock)
            {
                File.AppendAllText(logPath, logEntry, Encoding.UTF8);
            }
        }
        catch
        {
            // Silently fail if we can't write to log - don't break the generation process
        }
    }
    
    /// <summary>
    /// Writes multiple debug messages to the log file.
    /// More efficient than multiple WriteDebug calls.
    /// Note: All messages receive the same timestamp since they're part of a batch write.
    /// </summary>
    public static void WriteDebugLines(IEnumerable<string> messages)
    {
        try
        {
            var logPath = GetLogFilePath();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sb = new StringBuilder();
            
            foreach (var message in messages)
            {
                sb.AppendLine($"[{timestamp}] {message}");
            }
            
            lock (_lock)
            {
                File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Silently fail if we can't write to log - don't break the generation process
        }
    }
    
    /// <summary>
    /// Gets the current log file path (for displaying to user).
    /// Returns null if no log file has been created yet.
    /// </summary>
    public static string? GetCurrentLogFilePath()
    {
        return _logFilePath;
    }
}
