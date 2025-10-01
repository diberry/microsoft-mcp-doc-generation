using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using Shared;
using NaturalLanguageGenerator; // Added namespace for TextCleanup

public class ConfigFiles
{
    public string? NLParametersPath { get; set; }
    public string? TextReplacerParametersPath { get; set; }
    public List<string> RequiredFiles { get; set; } = new();

}


public class Config
{
    public static string? NLParametersPath { get; set; } // Made static to resolve object reference error
    public static string? TextReplacerParametersPath { get; set; } // Made static to resolve object reference error

    public List<string> RequiredFiles { get; set; } = new();
    public static bool Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }

        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<Config>(configJson);
        if (config == null || config.RequiredFiles == null || config.RequiredFiles.Count == 0)
        {
            throw new InvalidDataException("Invalid or empty config file.");
        }
        Console.WriteLine($"Config loaded from {configPath}");

        // Validate required files and set full paths
        for (int i = 0; i < config.RequiredFiles.Count; i++)
        {
            var file = config.RequiredFiles[i];
            var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, file));
            Console.WriteLine($"Checking file: {fullPath}");
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Required file not found: {fullPath}");
            }

            // Update the list with the resolved full path
            config.RequiredFiles[i] = fullPath;

            if (file.Contains("nl-parameters.json", StringComparison.OrdinalIgnoreCase))
            {
                NLParametersPath = fullPath; // Updated to use static property
            }
            else if (file.Contains("static-text-replacement.json", StringComparison.OrdinalIgnoreCase))
            {
                TextReplacerParametersPath = fullPath; // Updated to use static property
            }
        }

        if (string.IsNullOrEmpty(NLParametersPath) || string.IsNullOrEmpty(TextReplacerParametersPath))
        {
            throw new InvalidDataException("One or more required files are missing in the config.");
        }

        Console.WriteLine($"NLParametersPath: {NLParametersPath}");
        Console.WriteLine($"TextReplacerParametersPath: {TextReplacerParametersPath}");

        Console.WriteLine($"RequiredFiles: {JsonSerializer.Serialize(config.RequiredFiles, new JsonSerializerOptions { WriteIndented = true })}");

        var success = TextCleanup.LoadFiles(config.RequiredFiles);
        if (!success)
        {
            return false;
        }

        return true;
    }
}
