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
/// All methods use async operations with in-memory caching for performance.
/// </summary>
public static class DataFileLoader
{
    private static Dictionary<string, BrandMapping>? _brandMappings;
    private static Dictionary<string, string>? _compoundWords;
    private static HashSet<string>? _stopWords;
    private static List<CommonParameterDefinition>? _commonParameters;

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
    /// Results are cached in memory after first load.
    /// </summary>
    /// <returns>Dictionary keyed by McpServerName</returns>
    public static async Task<Dictionary<string, BrandMapping>> LoadBrandMappingsAsync()
    {
        if (_brandMappings != null)
            return _brandMappings;

        try
        {
            var mappingFile = ResolveDataFilePath("brand-to-server-mapping.json");

            if (!File.Exists(mappingFile))
            {
                Console.WriteLine($"Warning: Brand mapping file not found at {mappingFile}, using default naming");
                _brandMappings = new Dictionary<string, BrandMapping>();
                return _brandMappings;
            }

            var json = await File.ReadAllTextAsync(mappingFile);
            var mappings = JsonSerializer.Deserialize<List<BrandMapping>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _brandMappings = mappings?.ToDictionary(m => m.McpServerName ?? "", m => m) 
                ?? new Dictionary<string, BrandMapping>();
            
            Console.WriteLine($"Loaded {_brandMappings.Count} brand mappings from {mappingFile}");
            return _brandMappings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading brand mappings: {ex.Message}");
            _brandMappings = new Dictionary<string, BrandMapping>();
            return _brandMappings;
        }
    }

    /// <summary>
    /// Loads compound words mappings from JSON file.
    /// Results are cached in memory after first load.
    /// </summary>
    /// <returns>Dictionary mapping concatenated words to hyphenated forms</returns>
    public static async Task<Dictionary<string, string>> LoadCompoundWordsAsync()
    {
        if (_compoundWords != null)
            return _compoundWords;

        try
        {
            var compoundWordsFile = ResolveDataFilePath("compound-words.json");
            
            if (!File.Exists(compoundWordsFile))
            {
                Console.WriteLine($"Warning: Compound words file not found at {compoundWordsFile}");
                _compoundWords = new Dictionary<string, string>();
                return _compoundWords;
            }

            var compoundWordsJson = await File.ReadAllTextAsync(compoundWordsFile);
            _compoundWords = JsonSerializer.Deserialize<Dictionary<string, string>>(compoundWordsJson) 
                ?? new Dictionary<string, string>();
            
            Console.WriteLine($"Loaded {_compoundWords.Count} compound word mappings from {compoundWordsFile}");
            return _compoundWords;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading compound words: {ex.Message}");
            _compoundWords = new Dictionary<string, string>();
            return _compoundWords;
        }
    }

    /// <summary>
    /// Loads stop words from JSON file.
    /// Results are cached in memory after first load.
    /// </summary>
    /// <returns>HashSet of words to exclude from filenames</returns>
    public static async Task<HashSet<string>> LoadStopWordsAsync()
    {
        if (_stopWords != null)
            return _stopWords;

        try
        {
            var stopWordsFile = ResolveDataFilePath("stop-words.json");
            
            if (!File.Exists(stopWordsFile))
            {
                Console.WriteLine($"Warning: Stop words file not found at {stopWordsFile}");
                _stopWords = new HashSet<string>();
                return _stopWords;
            }

            var stopWordsJson = await File.ReadAllTextAsync(stopWordsFile);
            var stopWordsList = JsonSerializer.Deserialize<List<string>>(stopWordsJson) ?? new List<string>();
            _stopWords = new HashSet<string>(stopWordsList);
            
            Console.WriteLine($"Loaded {_stopWords.Count} stop words from {stopWordsFile}");
            return _stopWords;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stop words: {ex.Message}");
            _stopWords = new HashSet<string>();
            return _stopWords;
        }
    }

    /// <summary>
    /// Loads common parameters from JSON configuration file.
    /// Results are cached in memory after first load.
    /// </summary>
    /// <returns>List of common parameter definitions</returns>
    public static async Task<List<CommonParameterDefinition>> LoadCommonParametersAsync()
    {
        if (_commonParameters != null)
            return _commonParameters;

        try
        {
            var commonParamsFile = ResolveDataFilePath("common-parameters.json");
            
            if (!File.Exists(commonParamsFile))
            {
                Console.WriteLine($"Warning: common-parameters.json not found at {commonParamsFile}");
                _commonParameters = new List<CommonParameterDefinition>();
                return _commonParameters;
            }

            var json = await File.ReadAllTextAsync(commonParamsFile);
            _commonParameters = JsonSerializer.Deserialize<List<CommonParameterDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CommonParameterDefinition>();

            Console.WriteLine($"Loaded {_commonParameters.Count} common parameters from {commonParamsFile}");
            return _commonParameters;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading common parameters: {ex.Message}");
            _commonParameters = new List<CommonParameterDefinition>();
            return _commonParameters;
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
    /// Clears all cached data. Useful for testing or when data files are updated.
    /// </summary>
    public static void ClearCache()
    {
        _brandMappings = null;
        _compoundWords = null;
        _stopWords = null;
        _commonParameters = null;
    }
}
