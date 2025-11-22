// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using NaturalLanguageGenerator;
using Shared;

namespace CSharpGenerator;

public static class OptionsDiscovery
{

    public static async Task<List<CommonParameter>> DiscoverCommonParametersFromSource()
    {
        // TextCleanup is initialized by Config.Load previously; use it directly
        var commonParams = new List<CommonParameter>();

        // Try to find OptionDefinitions.cs in different possible locations
        string[] possiblePaths = new string[] {
            Path.Combine("..", "..", "core", "Azure.Mcp.Core", "src", "Models", "Option", "OptionDefinitions.cs"),
            Path.Combine("..", "..", "core", "Azure.Mcp.Core", "src", "Option", "OptionDefinitions.cs"),
            Path.Combine("..", "..", "core", "AzureMcp.Core", "src", "Models", "Option", "OptionDefinitions.cs"),
            Path.Combine("..", "..", "core", "AzureMcp.Core", "src", "Option", "OptionDefinitions.cs")
        };

        string? optionDefinitionsPath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                optionDefinitionsPath = path;
                Console.WriteLine($"Found OptionDefinitions.cs at: {optionDefinitionsPath}");
                break;
            }
        }

        // Get mappings from options classes regardless of whether we have definitions
        var mappingsFromClasses = await DiscoverOptionsClassMappings();
        Console.WriteLine($"Found {mappingsFromClasses.Count} property mappings from options classes");

        if (optionDefinitionsPath == null)
        {
            Console.WriteLine("Warning: OptionDefinitions.cs not found in any of the expected locations. Using only options classes.");
            
            // Create basic parameters from the discovered properties
            foreach (var mapping in mappingsFromClasses)
            {
                Console.WriteLine($"Adding parameter from class mapping: {mapping.ParameterName}");
                
                commonParams.Add(new CommonParameter
                {
                    Name = mapping.ParameterName,
                    Type = MapCSharpTypeToJsonType(mapping.PropertyType.Replace("?", "")),
                    IsRequired = false, // Conservative default
                    Description = $"Parameter for {mapping.PropertyName} in {mapping.ClassName}",
                    UsagePercent = 100,
                    IsHidden = false,
                    Source = mapping.ClassName,
                    RequiredText = "Optional",
                    NL_Name = TextCleanup.NormalizeParameter(mapping.ParameterName)
                });
            }
            
            return commonParams.OrderBy(p => p.Name).ToList();
        }

        var optionDefinitionsSource = await File.ReadAllTextAsync(optionDefinitionsPath);

        // Step 1: Extract ALL static classes and their options dynamically
        var allOptionsFromClasses = ExtractAllOptionsFromClasses(optionDefinitionsSource);
        Console.WriteLine($"Debug: Found {allOptionsFromClasses.Count} option definitions from static classes");

        // Step 2: Find all GlobalOptions and RetryPolicyOptions properties dynamically
        var optionsClassMappings = await DiscoverOptionsClassMappings();
        Console.WriteLine($"Debug: Found {optionsClassMappings.Count} option class property mappings");

        // Step 3: Cross-reference to create final parameter list
        foreach (var mapping in optionsClassMappings)
        {
            var matchingOption = allOptionsFromClasses.FirstOrDefault(opt =>
                opt.ParameterName.Equals(mapping.ParameterName, StringComparison.OrdinalIgnoreCase));

            if (matchingOption != null)
            {
                Console.WriteLine($"Debug: Matched {mapping.PropertyName} -> {matchingOption.ParameterName}");
                commonParams.Add(new CommonParameter
                {
                    Name = matchingOption.ParameterName,
                    Type = MapCSharpTypeToJsonType(mapping.PropertyType.Replace("?", "")),
                    IsRequired = matchingOption.IsRequired,
                    Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(matchingOption.Description)),
                    UsagePercent = 100,
                    IsHidden = matchingOption.IsHidden,
                    Source = matchingOption.ClassName,
                    RequiredText = matchingOption.IsRequired ? "Required" : "Optional",
                    NL_Name = TextCleanup.NormalizeParameter(matchingOption.ParameterName ?? "")
                });
            }
        }

        // Step 4: Add any remaining options that might not be mapped to properties
        foreach (var option in allOptionsFromClasses)
        {
            if (!commonParams.Any(p => p.Name.Equals(option.ParameterName, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"Debug: Adding unmapped option: {option.ParameterName}");

                var newParameter = new CommonParameter
                {
                    Name = option.ParameterName ?? "Unknown",
                    Type = MapCSharpTypeToJsonType(option.Type),
                    IsRequired = option.IsRequired,
                    Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(option.Description)),
                    UsagePercent = 100,
                    IsHidden = option.IsHidden,
                    Source = option.ClassName,
                    RequiredText = option.IsRequired ? "Required" : "Optional",
                    NL_Name = TextCleanup.NormalizeParameter(option.ParameterName ?? "")
                };

                if (newParameter.Name == "Unknown")
                {
                    Console.WriteLine($"Warning: Parameter '{option.ParameterName}' has an unknown name.");
                }
                if (newParameter.NL_Name == "TBD")
                {
                    Console.WriteLine($"Warning: Parameter '{option.ParameterName}' could not be mapped to a natural language name.");
                }

                commonParams.Add(newParameter);
            }
        }

        Console.WriteLine($"Debug: Total discovered parameters: {commonParams.Count}");
        return commonParams.OrderBy(p => p.Name).ToList();
    }

    private static List<OptionDefinition> ExtractAllOptionsFromClasses(string sourceCode)
    {
        var options = new List<OptionDefinition>();

        // Step 1: Extract all constants that map to parameter names
        var constPattern = @"public\s+const\s+string\s+(\w+)\s*=\s*""([^""]+)"";";
        var constMatches = Regex.Matches(sourceCode, constPattern);
        var constantMap = new Dictionary<string, string>();

        foreach (Match constMatch in constMatches)
        {
            var constName = constMatch.Groups[1].Value;
            var paramName = constMatch.Groups[2].Value;
            constantMap[constName] = paramName;
            Console.WriteLine($"Debug: Found constant {constName} = {paramName}");
        }

        // Step 2: Find class boundaries for context
        var classPositions = new List<(string name, int position)>();
        var classPattern = @"public\s+static\s+class\s+(\w+)";
        var classMatches = Regex.Matches(sourceCode, classPattern);

        foreach (Match classMatch in classMatches)
        {
            classPositions.Add((classMatch.Groups[1].Value, classMatch.Index));
        }

        // Step 3: Use a simple pattern to find option definitions, then parse them individually
        var simpleOptionPattern = @"public\s+static\s+readonly\s+Option<([^>]+)>\s+(\w+)\s*=\s*new\s*\(";
        var optionMatches = Regex.Matches(sourceCode, simpleOptionPattern);

        foreach (Match optionMatch in optionMatches)
        {
            var type = optionMatch.Groups[1].Value.Trim();
            var propertyName = optionMatch.Groups[2].Value;

            // Find the class this option belongs to
            var className = "Common";
            foreach (var (name, position) in classPositions.OrderByDescending(x => x.position))
            {
                if (position < optionMatch.Index)
                {
                    className = name;
                    break;
                }
            }

            // Extract the full option definition by finding the matching closing parenthesis and brace
            int startPos = optionMatch.Index;
            int currentPos = optionMatch.Index + optionMatch.Length;

            // Find the parameter list inside new(...) 
            int parenCount = 1;
            int constructorStart = currentPos;

            while (currentPos < sourceCode.Length && parenCount > 0)
            {
                if (sourceCode[currentPos] == '(') parenCount++;
                else if (sourceCode[currentPos] == ')') parenCount--;
                currentPos++;
            }

            var constructorContent = sourceCode.Substring(constructorStart, currentPos - constructorStart - 1);

            // Parse the constructor parameters
            var paramName = "";
            var description = "";
            var isRequired = false;
            var isHidden = false;

            // Look for the parameter pattern: $"--{ConstantName}"
            var parameterPattern = @"\$""--\{(\w+)\}""";
            var paramMatch = Regex.Match(constructorContent, parameterPattern);
            if (paramMatch.Success)
            {
                var constReference = paramMatch.Groups[1].Value;
                paramName = constantMap.ContainsKey(constReference) ? constantMap[constReference] : constReference.ToLowerInvariant();
            }

            // Extract description - look for quoted strings that aren't the parameter name
            var descriptionPattern = @"""([^""]{10,})""";
            var descMatches = Regex.Matches(constructorContent, descriptionPattern);
            var descriptions = new List<string>();

            foreach (Match descMatch in descMatches)
            {
                var desc = descMatch.Groups[1].Value;
                if (!desc.StartsWith("--") && desc.Length > 10) // Skip parameter names, keep descriptions
                {
                    descriptions.Add(TextCleanup.ReplaceStaticText(desc));
                }
            }

            description = string.Join(" ", descriptions);

            // Check for properties after the constructor
            // Look ahead for the { ... } block
            while (currentPos < sourceCode.Length && char.IsWhiteSpace(sourceCode[currentPos]))
                currentPos++;

            if (currentPos < sourceCode.Length && sourceCode[currentPos] == '{')
            {
                int braceStart = currentPos + 1;
                int braceCount = 1;
                currentPos++;

                while (currentPos < sourceCode.Length && braceCount > 0)
                {
                    if (sourceCode[currentPos] == '{') braceCount++;
                    else if (sourceCode[currentPos] == '}') braceCount--;
                    currentPos++;
                }

                var propertiesContent = sourceCode.Substring(braceStart, currentPos - braceStart - 1);
                isRequired = propertiesContent.Contains("IsRequired = true");
                isHidden = propertiesContent.Contains("IsHidden = true");
            }

            if (string.IsNullOrEmpty(description))
            {
                description = $"Parameter for {propertyName}";
            }

            Console.WriteLine($"Debug: Found option in {className}: {propertyName} -> {paramName} ({type})");
            Console.WriteLine($"Debug: Description: {description.Substring(0, Math.Min(50, description.Length))}...");

            options.Add(new OptionDefinition
            {
                ClassName = className,
                PropertyName = propertyName,
                ParameterName = paramName,
                Type = type,
                Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(description)),
                IsRequired = isRequired,
                IsHidden = isHidden
            });
        }

        Console.WriteLine($"Debug: Found {constMatches.Count} constants and {options.Count} options");
        return options;
    }

    private static async Task<List<OptionsClassMapping>> DiscoverOptionsClassMappings()
    {
        var mappings = new List<OptionsClassMapping>();

        // Try different possible paths for the Options directory
        string[] possiblePaths = new string[] {
            Path.Combine("..", "..", "core", "Azure.Mcp.Core", "src", "Models", "Option"),
            Path.Combine("..", "..", "core", "Azure.Mcp.Core", "src", "Option"),
            Path.Combine("..", "..", "core", "AzureMcp.Core", "src", "Models", "Option"),
            Path.Combine("..", "..", "core", "AzureMcp.Core", "src", "Option")
        };

        string? optionsDirectoryPath = null;
        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
            {
                optionsDirectoryPath = path;
                Console.WriteLine($"Found Options directory at: {optionsDirectoryPath}");
                break;
            }
        }
        
        if (optionsDirectoryPath == null)
        {
            Console.WriteLine("Warning: Options directory not found in any of the expected locations");
            return mappings;
        }
        
        // Get all .cs files in the Options directory
        var optionsFiles = Directory.GetFiles(optionsDirectoryPath, "*.cs")
            .Where(file => !Path.GetFileName(file).Equals("OptionDefinitions.cs", StringComparison.OrdinalIgnoreCase)) // Skip the definitions file
            .ToList();
        
        Console.WriteLine($"Found {optionsFiles.Count} options class files");
        
        if (optionsFiles.Count == 0)
        {
            Console.WriteLine($"Warning: No options class files found in {optionsDirectoryPath}");
            
            // Fallback to hardcoded GlobalOptions and RetryPolicyOptions paths for backward compatibility
            var globalOptionsPath = Path.Combine(optionsDirectoryPath, "GlobalOptions.cs");
            if (File.Exists(globalOptionsPath))
            {
                Console.WriteLine($"Using fallback: {globalOptionsPath}");
                var globalOptionsSource = await File.ReadAllTextAsync(globalOptionsPath);
                mappings.AddRange(ExtractPropertiesFromOptionsClass(globalOptionsSource, "GlobalOptions"));
            }
            
            var retryPolicyPath = Path.Combine(optionsDirectoryPath, "RetryPolicyOptions.cs");
            if (File.Exists(retryPolicyPath))
            {
                Console.WriteLine($"Using fallback: {retryPolicyPath}");
                var retryPolicySource = await File.ReadAllTextAsync(retryPolicyPath);
                mappings.AddRange(ExtractPropertiesFromOptionsClass(retryPolicySource, "RetryPolicyOptions"));
            }
            
            return mappings;
        }
        
        // Process each file
        foreach (var optionsFile in optionsFiles)
        {
            var className = Path.GetFileNameWithoutExtension(optionsFile);
            Console.WriteLine($"Processing options class: {className}");
            
            try
            {
                var optionsSource = await File.ReadAllTextAsync(optionsFile);
                var extractedMappings = ExtractPropertiesFromOptionsClass(optionsSource, className);
                Console.WriteLine($"Extracted {extractedMappings.Count} properties from {className}");
                mappings.AddRange(extractedMappings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {className}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"Total mappings discovered: {mappings.Count}");
        return mappings;
    }

    private static List<OptionsClassMapping> ExtractPropertiesFromOptionsClass(string sourceCode, string className)
    {
        var mappings = new List<OptionsClassMapping>();

        // Extract properties that might map to option definitions
        var propertyPattern = @"public\s+([^?\s]+\??)\s+(\w+)\s*\{\s*get;\s*set;\s*\}";
        var propertyMatches = Regex.Matches(sourceCode, propertyPattern);

        // Check if the class actually contains the expected class name
        var classNamePattern = $@"(public|internal)\s+(class|record)\s+{className}";
        if (!Regex.IsMatch(sourceCode, classNamePattern))
        {
            Console.WriteLine($"Warning: Class {className} not found in the source file");
            // Try to extract the actual class name from the file
            var actualClassMatch = Regex.Match(sourceCode, @"(public|internal)\s+(class|record)\s+(\w+)");
            if (actualClassMatch.Success)
            {
                className = actualClassMatch.Groups[3].Value;
                Console.WriteLine($"Using actual class name: {className}");
            }
        }

        foreach (Match match in propertyMatches)
        {
            var propertyType = match.Groups[1].Value;
            var propertyName = match.Groups[2].Value;

            // Try to infer parameter name from property name
            var parameterName = InferParameterNameFromProperty(propertyName);

            Console.WriteLine($"Debug: Found property in {className}: {propertyName} ({propertyType}) -> inferred param: {parameterName}");

            mappings.Add(new OptionsClassMapping
            {
                ClassName = className,
                PropertyName = propertyName,
                PropertyType = propertyType,
                ParameterName = parameterName
            });
        }

        return mappings;
    }

    private static string InferParameterNameFromProperty(string propertyName)
    {
        // Convert PascalCase property names to kebab-case parameter names
        // Examples: TenantId -> tenant-id, AuthMethod -> auth-method
        return Regex.Replace(propertyName, @"([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }

    private static string MapCSharpTypeToJsonType(string csharpType)
    {
        return csharpType.ToLowerInvariant() switch
        {
            "string" => "string",
            "int" => "integer",
            "integer" => "integer",
            "double" => "number",
            "float" => "number",
            "decimal" => "number",
            "bool" => "boolean",
            "boolean" => "boolean",
            "timespan" => "number",
            _ => "string" // Default to string for unknown types
        };
    }
}

// Data models for options discovery
public class OptionDefinition
{
    public string ClassName { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public string ParameterName { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsHidden { get; set; }
}

public class OptionsClassMapping
{
    public string ClassName { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public string ParameterName { get; set; } = "";
}
