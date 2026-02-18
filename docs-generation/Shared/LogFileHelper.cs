using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Searches for generated*/logs directories to support namespace-specific output directories.
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
            
            // First, try to find any generated*/logs directory that exists
            // This supports both generated/ and generated-<namespace>/ patterns
            var parentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));
            string? logDir = null;
            
            if (Directory.Exists(parentDir))
            {
                var generatedDirs = Directory.GetDirectories(parentDir, "generated*")
                    .Where(d => Directory.Exists(Path.Combine(d, "logs")))
                    .OrderByDescending(d => Directory.GetLastWriteTime(d)) // Use most recently modified
                    .ToList();
                    
                if (generatedDirs.Any())
                {
                    logDir = Path.Combine(generatedDirs.First(), "logs");
                }
            }
            
            // Fallback to traditional search paths if no generated*/logs found
            if (logDir == null)
            {
                var searchPaths = new[]
                {
                    Path.Combine(currentDir, "..", "..", "generated", "logs"),  // From bin/Debug or bin/Release
                    Path.Combine(currentDir, "..", "generated", "logs"),         // From docs-generation subdirectory
                    Path.Combine(currentDir, "generated", "logs"),               // From docs-generation root
                    Path.Combine(currentDir, "..", "..", "..", "generated", "logs") // From deeper nested paths
                };
                
                foreach (var path in searchPaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    var parentDirOfPath = Path.GetDirectoryName(fullPath);
                    // Check if parent directory exists, or if its parent exists (to handle paths like ../generated/logs)
                    if (parentDirOfPath != null && Directory.Exists(parentDirOfPath))
                    {
                        logDir = fullPath;
                        break;
                    }
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
