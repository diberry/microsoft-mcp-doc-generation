// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using NaturalLanguageGenerator;
using Shared;

public static class ServiceOptionsDiscovery
{
    public static async Task<List<ServiceOption>> DiscoverServiceStartOptionsFromSource()
    {
        var serviceOptions = new List<ServiceOption>();

        // Try to find ServiceOptionDefinitions.cs
        string[] possiblePaths = new string[] {
            Path.Combine("..", "..", "core", "Azure.Mcp.Core", "src", "Areas", "Server", "Options", "ServiceOptionDefinitions.cs"),
            Path.Combine("..", "..", "core", "Azure.Mcp.Core", "src", "Options", "ServiceOptionDefinitions.cs"),
            Path.Combine("..", "..", "core", "AzureMcp.Core", "src", "Areas", "Server", "Options", "ServiceOptionDefinitions.cs"),
            Path.Combine("..", "..", "core", "AzureMcp.Core", "src", "Options", "ServiceOptionDefinitions.cs")
        };

        string? serviceOptionDefinitionsPath = null;
        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                serviceOptionDefinitionsPath = path;
                Console.WriteLine($"Found ServiceOptionDefinitions.cs at: {serviceOptionDefinitionsPath}");
                break;
            }
        }

        if (serviceOptionDefinitionsPath == null)
        {
            Console.WriteLine("Warning: ServiceOptionDefinitions.cs not found in any of the expected locations.");
            return serviceOptions;
        }

        var serviceOptionDefinitionsSource = await File.ReadAllTextAsync(serviceOptionDefinitionsPath);

        // Extract service options from the class
        serviceOptions = ExtractServiceOptions(serviceOptionDefinitionsSource);
        Console.WriteLine($"Debug: Found {serviceOptions.Count} service option definitions");

        return serviceOptions.OrderBy(p => p.Name).ToList();
    }

    private static List<ServiceOption> ExtractServiceOptions(string sourceCode)
    {
        var options = new List<ServiceOption>();

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

        // Step 2: Use a simple pattern to find option definitions, then parse them individually
        var simpleOptionPattern = @"public\s+static\s+readonly\s+Option<([^>]+)>\s+(\w+)\s*=\s*new\s*\(";
        var optionMatches = Regex.Matches(sourceCode, simpleOptionPattern);

        foreach (Match optionMatch in optionMatches)
        {
            var type = optionMatch.Groups[1].Value.Trim();
            var propertyName = optionMatch.Groups[2].Value;

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
                isRequired = propertiesContent.Contains("Required = true");
                isHidden = propertiesContent.Contains("Hidden = true");
                
                // Extract description from properties block
                var propertyDescPattern = @"Description\s*=\s*""([^""]+)""";
                var propertyDescMatch = Regex.Match(propertiesContent, propertyDescPattern);
                if (propertyDescMatch.Success)
                {
                    description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(propertyDescMatch.Groups[1].Value));
                }
            }

            if (string.IsNullOrEmpty(description))
            {
                description = $"Parameter for {propertyName}";
            }

            Console.WriteLine($"Debug: Found service option: {propertyName} -> {paramName} ({type})");
            Console.WriteLine($"Debug: Description: {description.Substring(0, Math.Min(50, description.Length))}...");

            options.Add(new ServiceOption
            {
                Name = paramName,
                PropertyName = propertyName,
                Type = MapCSharpTypeToJsonType(type),
                Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(description)),
                IsRequired = isRequired,
                IsHidden = isHidden,
                NlName = TextCleanup.NormalizeParameter(paramName)
            });
        }

        return options;
    }

    private static string MapCSharpTypeToJsonType(string csharpType)
    {
        return csharpType.ToLowerInvariant() switch
        {
            "string" => "string",
            "string[]" => "array of strings",
            "string?" => "string",
            "int" => "integer",
            "int?" => "integer",
            "integer" => "integer",
            "double" => "number",
            "double?" => "number",
            "float" => "number",
            "float?" => "number",
            "decimal" => "number",
            "decimal?" => "number",
            "bool" => "boolean",
            "bool?" => "boolean",
            "boolean" => "boolean",
            "timespan" => "number",
            _ => "string" // Default to string for unknown types
        };
    }
}

// Data model for service options
public class ServiceOption
{
    public string Name { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsHidden { get; set; }
    public string NlName { get; set; } = "";
}