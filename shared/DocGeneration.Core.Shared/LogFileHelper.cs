using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shared;

/// <summary>
/// Helper for writing debug output to log files in the generated/logs directory.
/// Provides centralized logging for verbose debug information that should not clutter console output.
/// One log file per process, named after the process (e.g., "annotations-generator.log").
/// Each process invocation starts fresh (overwrites previous log).
/// </summary>
public static class LogFileHelper
{
    private static readonly object _lock = new object();
    private static string? _logFilePath;
    private static string? _processName;
    
    /// <summary>
    /// Initializes the logger with a process name.
    /// The process name becomes the log filename (e.g., "annotations-generator" → "annotations-generator.log").
    /// Must be called before any WriteDebug calls for the name to take effect.
    /// Creates a fresh log file (overwrites any previous run's log).
    /// </summary>
    /// <param name="processName">Name of the generator process (e.g., "annotations-generator", "example-prompts")</param>
    public static void Initialize(string processName)
    {
        lock (_lock)
        {
            _processName = processName;
            _logFilePath = null; // Reset so next call creates file with new name
        }
    }
    
    /// <summary>
    /// Gets or creates the log file path for debug output.
    /// Filename: {processName}.log (or debug.log if not initialized).
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
                
            var logDir = FindLogDirectory();
            Directory.CreateDirectory(logDir);
            
            var filename = BuildLogFilename();
            _logFilePath = Path.Combine(logDir, filename);
            
            // Start fresh — overwrite any previous log from a prior run
            File.WriteAllText(_logFilePath, "", Encoding.UTF8);
            
            return _logFilePath;
        }
    }
    
    /// <summary>
    /// Finds the best log directory by searching for generated*/logs directories.
    /// Falls back to a logs/ subdirectory in the current directory.
    /// </summary>
    private static string FindLogDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        // First, try to find any generated*/logs directory that exists
        var parentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));
        
        if (Directory.Exists(parentDir))
        {
            var generatedDirs = Directory.GetDirectories(parentDir, "generated*")
                .Where(d => Directory.Exists(Path.Combine(d, "logs")))
                .OrderByDescending(d => Directory.GetLastWriteTime(d))
                .ToList();
                
            if (generatedDirs.Any())
            {
                return Path.Combine(generatedDirs.First(), "logs");
            }
        }
        
        // Fallback to traditional search paths
        var searchPaths = new[]
        {
            Path.Combine(currentDir, "..", "..", "generated", "logs"),
            Path.Combine(currentDir, "..", "generated", "logs"),
            Path.Combine(currentDir, "generated", "logs"),
            Path.Combine(currentDir, "..", "..", "..", "generated", "logs")
        };
        
        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            var parentDirOfPath = Path.GetDirectoryName(fullPath);
            if (parentDirOfPath != null && Directory.Exists(parentDirOfPath))
            {
                return fullPath;
            }
        }
        
        return Path.Combine(currentDir, "logs");
    }
    
    /// <summary>
    /// Builds the log filename from process name.
    /// Falls back to "debug.log" if Initialize was not called.
    /// </summary>
    internal static string BuildLogFilename()
    {
        return string.IsNullOrWhiteSpace(_processName)
            ? "debug.log"
            : $"{_processName}.log";
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
    
    /// <summary>
    /// Resets all state (process name, log file path).
    /// Intended for testing only.
    /// </summary>
    internal static void Reset()
    {
        lock (_lock)
        {
            _logFilePath = null;
            _processName = null;
        }
    }
}
