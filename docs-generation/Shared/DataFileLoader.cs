// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Shared;

/// <summary>
/// Provides centralized loading of data files used across multiple projects.
/// All methods use async operations with thread-safe in-memory caching for performance.
/// </summary>
public static class DataFileLoader
{
    private static readonly Lazy<Task<Dictionary<string, BrandMapping>>> _brandMappings = 
        new Lazy<Task<Dictionary<string, BrandMapping>>>(LoadBrandMappingsInternalAsync);
    private static readonly Lazy<Task<Dictionary<string, string>>> _compoundWords = 
        new Lazy<Task<Dictionary<string, string>>>(LoadCompoundWordsInternalAsync);
    private static readonly Lazy<Task<HashSet<string>>> _stopWords = 
        new Lazy<Task<HashSet<string>>>(LoadStopWordsInternalAsync);
    private static readonly Lazy<Task<List<CommonParameterDefinition>>> _commonParameters = 
        new Lazy<Task<List<CommonParameterDefinition>>>(LoadCommonParametersInternalAsync);

    /// <summary>
    /// Gets the path to the data directory relative to the executing assembly.
    /// This resolves to docs-generation/data/ regardless of which project calls it.
    /// </summary>
    public static string GetDataDirectoryPath()
    {
        // From bin/Debug/net9.0 we need to go up 4 levels to docs-generation, then into data
        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data");
    }

    /// <summary>
    /// Resolves a data file path with fallback strategy.
    /// First tries AppContext.BaseDirectory relative path, then current directory.
    /// </summary>
    private static string ResolveDataFilePath(string filename)
    {
        var primaryPath = Path.Combine(GetDataDirectoryPath(), filename);
        if (File.Exists(primaryPath))
        {
            return primaryPath;
        }

        // Fallback for legacy invocation patterns
        var fallbackPath = Path.Combine("..", "data", filename);
        if (File.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        return primaryPath; // Return primary path even if it doesn't exist for consistent error messages
    }

    /// <summary>
    /// Loads brand-to-server-name mappings from JSON file.
    /// Results are cached in memory after first load (thread-safe).
    /// </summary>
    /// <returns>Dictionary keyed by McpServerName</returns>
    public static Task<Dictionary<string, BrandMapping>> LoadBrandMappingsAsync()
    {
        return _brandMappings.Value;
    }

    /// <summary>
    /// Internal method to load brand mappings (called once by Lazy).
    /// </summary>
    private static async Task<Dictionary<string, BrandMapping>> LoadBrandMappingsInternalAsync()
    {
        try
        {
            var mappingFile = ResolveDataFilePath("brand-to-server-mapping.json");

            if (!File.Exists(mappingFile))
            {
                Console.WriteLine($"Warning: Brand mapping file not found at {mappingFile}, using default naming");
                return new Dictionary<string, BrandMapping>();
            }

            var json = await File.ReadAllTextAsync(mappingFile);
            var mappings = JsonSerializer.Deserialize<List<BrandMapping>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var result = mappings?.ToDictionary(m => m.McpServerName ?? "", m => m) 
                ?? new Dictionary<string, BrandMapping>();
            
            Console.WriteLine($"Loaded {result.Count} brand mappings from {mappingFile}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading brand mappings: {ex.Message}");
            return new Dictionary<string, BrandMapping>();
        }
    }

    /// <summary>
    /// Loads compound words mappings from JSON file.
    /// Results are cached in memory after first load (thread-safe).
    /// </summary>
    /// <returns>Dictionary mapping concatenated words to hyphenated forms</returns>
    public static Task<Dictionary<string, string>> LoadCompoundWordsAsync()
    {
        return _compoundWords.Value;
    }

    /// <summary>
    /// Internal method to load compound words (called once by Lazy).
    /// </summary>
    private static async Task<Dictionary<string, string>> LoadCompoundWordsInternalAsync()
    {
        try
        {
            var compoundWordsFile = ResolveDataFilePath("compound-words.json");
            
            if (!File.Exists(compoundWordsFile))
            {
                Console.WriteLine($"Warning: Compound words file not found at {compoundWordsFile}");
                return new Dictionary<string, string>();
            }

            var compoundWordsJson = await File.ReadAllTextAsync(compoundWordsFile);
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(compoundWordsJson) 
                ?? new Dictionary<string, string>();
            
            Console.WriteLine($"Loaded {result.Count} compound word mappings from {compoundWordsFile}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading compound words: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Loads stop words from JSON file.
    /// Results are cached in memory after first load (thread-safe).
    /// </summary>
    /// <returns>HashSet of words to exclude from filenames</returns>
    public static Task<HashSet<string>> LoadStopWordsAsync()
    {
        return _stopWords.Value;
    }

    /// <summary>
    /// Internal method to load stop words (called once by Lazy).
    /// </summary>
    private static async Task<HashSet<string>> LoadStopWordsInternalAsync()
    {
        try
        {
            var stopWordsFile = ResolveDataFilePath("stop-words.json");
            
            if (!File.Exists(stopWordsFile))
            {
                Console.WriteLine($"Warning: Stop words file not found at {stopWordsFile}");
                return new HashSet<string>();
            }

            var stopWordsJson = await File.ReadAllTextAsync(stopWordsFile);
            var stopWordsList = JsonSerializer.Deserialize<List<string>>(stopWordsJson) ?? new List<string>();
            var result = new HashSet<string>(stopWordsList);
            
            Console.WriteLine($"Loaded {result.Count} stop words from {stopWordsFile}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stop words: {ex.Message}");
            return new HashSet<string>();
        }
    }

    /// <summary>
    /// Loads common parameters from JSON configuration file.
    /// Results are cached in memory after first load (thread-safe).
    /// </summary>
    /// <returns>List of common parameter definitions</returns>
    public static Task<List<CommonParameterDefinition>> LoadCommonParametersAsync()
    {
        return _commonParameters.Value;
    }

    /// <summary>
    /// Internal method to load common parameters (called once by Lazy).
    /// </summary>
    private static async Task<List<CommonParameterDefinition>> LoadCommonParametersInternalAsync()
    {
        try
        {
            var commonParamsFile = ResolveDataFilePath("common-parameters.json");
            
            if (!File.Exists(commonParamsFile))
            {
                Console.WriteLine($"Warning: common-parameters.json not found at {commonParamsFile}");
                return new List<CommonParameterDefinition>();
            }

            var json = await File.ReadAllTextAsync(commonParamsFile);
            var result = JsonSerializer.Deserialize<List<CommonParameterDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CommonParameterDefinition>();

            Console.WriteLine($"Loaded {result.Count} common parameters from {commonParamsFile}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading common parameters: {ex.Message}");
            return new List<CommonParameterDefinition>();
        }
    }

    /// <summary>
    /// Loads parameter mappings from specified JSON file (nl-parameters or static-text-replacement).
    /// Not cached since multiple files may be loaded.
    /// </summary>
    /// <param name="filePath">Full path to the parameter mapping file</param>
    /// <returns>List of parameter mappings</returns>
    public static async Task<List<MappedParameter>> LoadParameterMappingsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Warning: Parameter mapping file not found at '{filePath}'");
            return new List<MappedParameter>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var mappings = JsonSerializer.Deserialize<List<MappedParameter>>(json) ?? new List<MappedParameter>();
            Console.WriteLine($"Loaded {mappings.Count} parameter mappings from {Path.GetFileName(filePath)}");
            return mappings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading parameter mappings from {filePath}: {ex.Message}");
            return new List<MappedParameter>();
        }
    }

    /// <summary>
    /// Note: Cache clearing is not supported with the thread-safe Lazy pattern.
    /// The cache is cleared automatically when the application domain is unloaded.
    /// For testing scenarios, consider using dependency injection or mocking instead.
    /// </summary>
    [Obsolete("Cache clearing is not supported with thread-safe Lazy initialization. Use dependency injection for testing.", true)]
    public static void ClearCache()
    {
        throw new NotSupportedException("Cache clearing is not supported with thread-safe Lazy initialization.");
    }
}
