// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Shared;

namespace CSharpGenerator;

internal class Program
{
     private static async Task<int> Main(string[] args)
     {

         // Load and validate config
         var configPath = Path.Combine(AppContext.BaseDirectory, "../../../../config.json");
         Console.WriteLine($"Loading config from: {configPath}");
         var success = Config.Load(configPath);
         if (!success)
         {
             Console.Error.WriteLine("Failed to load configuration.");
             return 1;
         }

         if (args.Length == 0)
         {
             Console.Error.WriteLine("Usage: CSharpGenerator <mode> [arguments...]");
             Console.Error.WriteLine("Modes:");
             Console.Error.WriteLine("  template <template-file> <data-file> <output-file> [additional-context-json]");
             Console.Error.WriteLine("  generate-docs <cli-output-json> <output-dir> [--index] [--common] [--commands] [--annotations] [--no-service-options]");
             return 1;
         }

         var mode = args[0];

         try
         {
             switch (mode)
             {
                 case "template":
                     return await ProcessTemplate(args[1..]);
                 case "generate-docs":
                     return await GenerateDocumentation(args[1..]);
                 default:
                     Console.Error.WriteLine($"Unknown mode: {mode}");
                     return 1;
             }
         }
         catch (Exception ex)
         {
             Console.Error.WriteLine($"Error: {ex.Message}");
             return 1;
         }
     }

     private static async Task<int> ProcessTemplate(string[] args)
     {
         if (args.Length < 3)
         {
             Console.Error.WriteLine("Usage: CSharpGenerator template <template-file> <data-file> <output-file> [additional-context-json]");
             return 1;
         }

         var templateFile = args[0];
         var dataFile = args[1];
         var outputFile = args[2];
         var additionalContext = args.Length > 3 ? args[3] : null;

         // Configure Handlebars template engine

         // Read data
         var dataJson = await File.ReadAllTextAsync(dataFile);
         var data = JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson, new JsonSerializerOptions
         {
             PropertyNameCaseInsensitive = true
         });

         if (data == null)
         {
             Console.Error.WriteLine($"Failed to parse data file: {dataFile}");
             return 1;
         }

         // Add additional context if provided
         if (!string.IsNullOrEmpty(additionalContext))
         {
             var additionalData = JsonSerializer.Deserialize<Dictionary<string, object>>(additionalContext, new JsonSerializerOptions
             {
                 PropertyNameCaseInsensitive = true
             });

             if (additionalData != null)
             {
                 foreach (var kvp in additionalData)
                 {
                     data[kvp.Key] = kvp.Value;
                 }
             }
         }

         // Add current timestamp
         data["generatedAt"] = DateTime.UtcNow;

         // Process template using the template engine
         var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, data);

         // Ensure output directory exists
         var outputDir = Path.GetDirectoryName(outputFile);
         if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
         {
             Directory.CreateDirectory(outputDir);
         }

         // Write output
         await File.WriteAllTextAsync(outputFile, result);

         Console.WriteLine($"Generated: {outputFile}");
         return 0;
     }

     private static async Task<int> GenerateDocumentation(string[] args)
     {
         if (args.Length < 2)
         {
             Console.Error.WriteLine("Usage: CSharpGenerator generate-docs <cli-output-json> <output-dir> [--index] [--common] [--commands] [--annotations] [--example-prompts] [--no-service-options] [--version <version>]");
             return 1;
         }

         var cliOutputFile = args[0];
         var outputDir = args[1];
         var generateIndex = args.Contains("--index");
         var generateCommon = args.Contains("--common");
         var generateCommands = args.Contains("--commands");
         var generateAnnotations = args.Contains("--annotations");
         var generateExamplePrompts = args.Contains("--example-prompts");
         var generateServiceOptions = !args.Contains("--no-service-options");
         
         // Extract version if provided
         string? cliVersion = null;
         var versionIndex = Array.IndexOf(args, "--version");
         if (versionIndex >= 0 && versionIndex + 1 < args.Length)
         {
             cliVersion = args[versionIndex + 1];
         }

         return await DocumentationGenerator.GenerateAsync(
             cliOutputFile,
             outputDir,
             generateIndex,
             generateCommon,
             generateCommands,
             generateServiceOptions,
             generateAnnotations,
             cliVersion,
             generateExamplePrompts);
     }
}

// Data models
public class CliOutput
{
    public List<Tool> Results { get; set; } = new();
}

public class Tool
{
    public string? Name { get; set; }
    public string? Command { get; set; }
    public string? Description { get; set; }
    public string? SourceFile { get; set; }
    public List<Option>? Option { get; set; }
    public string? Area { get; set; }
    public ToolMetadata? Metadata { get; set; }
    public string? AnnotationContent { get; set; } // Content from annotation file
    public string? AnnotationFileName { get; set; } // Filename of the annotation file
}

public class ToolMetadata
{
    [JsonPropertyName("destructive")]
    public MetadataValue? Destructive { get; set; }
    
    [JsonPropertyName("idempotent")]
    public MetadataValue? Idempotent { get; set; }
    
    [JsonPropertyName("openWorld")]
    public MetadataValue? OpenWorld { get; set; }
    
    [JsonPropertyName("readOnly")]
    public MetadataValue? ReadOnly { get; set; }
    
    [JsonPropertyName("secret")]
    public MetadataValue? Secret { get; set; }
    
    [JsonPropertyName("localRequired")]
    public MetadataValue? LocalRequired { get; set; }
}

public class MetadataValue
{
    [JsonPropertyName("value")]
    public bool Value { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class Option
{
    public string? Name { get; set; }
    public string? NL_Name { get; set; }
    public string? Type { get; set; }
    public bool Required { get; set; }
    public string RequiredText { get; set; } = "";
    public string? Description { get; set; }
}

public class AreaData
{
    public string Description { get; set; } = "";
    public int ToolCount { get; set; }
    public List<Tool> Tools { get; set; } = new();
}

public class TransformedData
{
    public string Version { get; set; } = "";
    public List<Tool> Tools { get; set; } = new();
    public Dictionary<string, AreaData> Areas { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public List<CommonParameter> SourceDiscoveredCommonParams { get; set; } = new();
}

public class CommonParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsRequired { get; set; }
    public string Description { get; set; } = "";
    public double UsagePercent { get; set; }
    public bool IsHidden { get; set; }
    public string Source { get; set; } = "";
    public string RequiredText { get; set; } = "";
    public string NL_Name { get; set; } = "";

}


// Extension method for regex replacement
public static class StringExtensions
{
    public static string RegularExpressionReplace(this string input, string pattern, string replacement)
    {
        return System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement);
    }
}
