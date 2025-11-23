// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HandlebarsDotNet;
using System.Text.Json;

namespace CSharpGenerator;

/// <summary>
/// Manages Handlebars template compilation and custom helper registration.
/// Responsible for all template-related operations and rendering logic.
/// </summary>
public static class HandlebarsTemplateEngine
{
    /// <summary>
    /// Creates a configured Handlebars instance with all custom helpers registered.
    /// </summary>
    public static IHandlebars CreateEngine()
    {
        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);
        return handlebars;
    }

    /// <summary>
    /// Processes a template file with data and returns the rendered result.
    /// </summary>
    public static async Task<string> ProcessTemplateAsync(string templateFile, Dictionary<string, object> data)
    {
        var handlebars = CreateEngine();
        
        var templateContent = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(templateContent);
        
        return template(data);
    }

    /// <summary>
    /// Processes a template string with data and returns the rendered result.
    /// </summary>
    public static string ProcessTemplateString(string templateContent, Dictionary<string, object> data)
    {
        var handlebars = CreateEngine();
        var template = handlebars.Compile(templateContent);
        return template(data);
    }

    /// <summary>
    /// Registers all custom Handlebars helpers for documentation generation.
    /// </summary>
    private static void RegisterHelpers(IHandlebars handlebars)
    {
        // Format date helper
        handlebars.RegisterHelper("formatDate", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (arguments[0] is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (DateTime.TryParse(arguments[0].ToString(), out var parsedDate))
                return parsedDate.ToString("yyyy-MM-dd HH:mm:ss UTC");

            return arguments[0].ToString();
        });

        // Format date short helper (for metadata headers)
        handlebars.RegisterHelper("formatDateShort", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return DateTime.UtcNow.ToString("MM/dd/yyyy");

            if (arguments[0] is DateTime dateTime)
                return dateTime.ToString("MM/dd/yyyy");

            if (DateTime.TryParse(arguments[0].ToString(), out var parsedDate))
                return parsedDate.ToString("MM/dd/yyyy");

            return arguments[0].ToString();
        });

        // Kebab case helper
        handlebars.RegisterHelper("kebabCase", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var str = arguments[0].ToString();
            return str?.ToLowerInvariant()
                .Replace(' ', '-')
                .Replace('_', '-')
                .RegularExpressionReplace("[^a-z0-9-]", "") ?? string.Empty;
        });

        // Get area count helper
        handlebars.RegisterHelper("getAreaCount", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return 0;

            if (arguments[0] is JsonElement element && element.ValueKind == JsonValueKind.Object)
                return element.EnumerateObject().Count();

            if (arguments[0] is Dictionary<string, object> dict)
                return dict.Count;

            if (arguments[0] is Dictionary<string, AreaData> areaDict)
                return areaDict.Count;

            return 0;
        });

        // Math helpers
        handlebars.RegisterHelper("add", (context, arguments) =>
        {
            if (arguments.Length < 2) return 0;
            
            if (double.TryParse(arguments[0]?.ToString(), out var a) && 
                double.TryParse(arguments[1]?.ToString(), out var b))
                return a + b;
            
            return 0;
        });

        handlebars.RegisterHelper("divide", (context, arguments) =>
        {
            if (arguments.Length < 2) return 0;
            
            if (double.TryParse(arguments[0]?.ToString(), out var a) && 
                double.TryParse(arguments[1]?.ToString(), out var b) && b != 0)
                return a / b;
            
            return 0;
        });

        handlebars.RegisterHelper("round", (context, arguments) =>
        {
            if (arguments.Length < 1) return 0;
            
            if (!double.TryParse(arguments[0]?.ToString(), out var num))
                return 0;

            var precision = 1;
            if (arguments.Length > 1 && int.TryParse(arguments[1]?.ToString(), out var p))
                precision = p;

            return Math.Round(num, precision);
        });

        // Required helper for boolean display
        handlebars.RegisterHelper("requiredIcon", (context, arguments) =>
        {
            if (arguments.Length == 0) return "❌";
            
            var value = arguments[0];
            if (value is bool boolValue)
                return boolValue ? "✅" : "❌";
            
            if (bool.TryParse(value?.ToString(), out var parsedBool))
                return parsedBool ? "✅" : "❌";
            
            return "❌";
        });

        // Parse sub-tool family (e.g., "blob" from "azmcp storage blob batch set-tier")
        handlebars.RegisterHelper("subToolFamily", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var command = arguments[0].ToString() ?? string.Empty;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 4) // Need at least "azmcp area subtool operation" for sub-tool structure
                return string.Empty;
                
            // For "azmcp storage blob batch set-tier" -> return "Blob"
            // For "azmcp storage account list" -> return "Account"
            string subTool = parts[2];
            
            // Split by dashes if present and format each part
            if (subTool.Contains('-'))
            {
                var subToolParts = subTool.Split('-')
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(part => char.ToUpper(part[0]) + part.Substring(1).ToLower());
                
                return string.Join(" ", subToolParts);
            }
            
            // Simple capitalization for non-hyphenated terms
            return char.ToUpper(subTool[0]) + subTool.Substring(1).ToLower();
        });

        // Check if command has sub-tool structure
        handlebars.RegisterHelper("hasSubTool", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return false;

            var command = arguments[0].ToString() ?? string.Empty;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Has sub-tool if there are 4+ parts: "azmcp area subtool operation"
            return parts.Length >= 4;
        });

        // Get operation name for commands without sub-tools
        handlebars.RegisterHelper("operationName", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var command = arguments[0].ToString() ?? string.Empty;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 3) // Need at least "azmcp area operation"
                return string.Empty;
                
            // For "azmcp kusto query" -> return "Query"
            string operation = parts[2];
            
            // Handle hyphenated terms
            if (operation.Contains('-'))
            {
                var operationParts = operation.Split('-')
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(part => char.ToUpper(part[0]) + part.Substring(1).ToLower());
                return string.Join("-", operationParts);
            }
            
            // Simple capitalization for non-hyphenated terms
            return char.ToUpper(operation[0]) + operation.Substring(1).ToLower();
        });

        // Parse sub-operation (everything after the sub-tool family)
        handlebars.RegisterHelper("subOperation", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var command = arguments[0].ToString() ?? string.Empty;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 3) // Need at least "azmcp area operation"
                return string.Empty;
            
            string operation;
            
            if (parts.Length >= 4)
            {
                // For "azmcp storage blob batch set-tier" -> return "batch set-tier"
                // Skip "azmcp", area name, and sub-tool family
                var remainingParts = parts.Skip(3).ToArray();
                operation = string.Join(" ", remainingParts);
            }
            else
            {
                // For "azmcp kusto query" -> return "query"
                // Skip "azmcp" and area name, use the operation directly
                operation = parts[2];
            }
            
            // Handle multiple words with proper casing
            if (operation.Contains(' '))
            {
                var operationParts = operation.Split(' ')
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(part => {
                        // Handle hyphenated terms
                        if (part.Contains('-'))
                        {
                            var hyphenParts = part.Split('-')
                                .Where(hp => !string.IsNullOrEmpty(hp))
                                .Select(hp => char.ToUpper(hp[0]) + hp.Substring(1).ToLower());
                            return string.Join("-", hyphenParts);
                        }
                        // Regular term
                        return char.ToUpper(part[0]) + part.Substring(1).ToLower();
                    });
                
                return string.Join(" ", operationParts);
            }
            
            // Handle single hyphenated word
            if (operation.Contains('-'))
            {
                var operationParts = operation.Split('-')
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(part => char.ToUpper(part[0]) + part.Substring(1).ToLower());
                return string.Join("-", operationParts);
            }
            
            // Handle single non-hyphenated word
            if (!string.IsNullOrEmpty(operation))
            {
                return char.ToUpper(operation[0]) + operation.Substring(1).ToLower();
            }
            
            return string.Empty;
        });

        // Concatenate strings
        handlebars.RegisterHelper("concat", (context, arguments) =>
        {
            return string.Join("", arguments.Select(arg => arg?.ToString() ?? string.Empty));
        });
        
        // Format parameter name to natural language
        handlebars.RegisterHelper("formatNaturalLanguage", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return "Unknown";
                
            var paramName = arguments[0].ToString() ?? "";
            
            // Remove CLI-style prefix if present
            if (paramName.StartsWith("--"))
                paramName = paramName.Substring(2);
                
            // Split by hyphens
            var wordsList = paramName.Split('-').Where(w => !string.IsNullOrEmpty(w)).ToList();
            
            // Process words: Capitalize only the first word, handle acronyms
            for (int i = 0; i < wordsList.Count; i++)
            {
                string word = wordsList[i];
                
                // Handle common acronyms (preserve for all positions)
                if (word.Equals("id", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "ID";
                else if (word.Equals("ids", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "IDs";
                else if (word.Equals("uri", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "URI";
                else if (word.Equals("url", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "URL";
                else if (word.Equals("urls", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "URLs";
                else if (word.Equals("ai", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "AI";
                else if (word.Equals("api", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "API";
                else if (word.Equals("apis", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "APIs";
                else if (word.Equals("cpu", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "CPU";
                else if (word.Equals("gpu", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "GPU";
                else if (word.Equals("ip", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "IP";
                else if (word.Equals("sql", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "SQL";
                else if (word.Equals("vm", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "VM";
                else if (word.Equals("vms", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "VMs";
                else if (word.Equals("dns", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "DNS";
                else if (word.Equals("sku", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "SKU";
                else if (word.Equals("skus", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "SKUs";
                else if (word.Equals("tls", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "TLS";
                else if (word.Equals("ssl", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "SSL";
                else if (word.Equals("http", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "HTTP";
                else if (word.Equals("https", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "HTTPS";
                else if (word.Equals("json", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "JSON";
                else if (word.Equals("xml", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "XML";
                else if (word.Equals("yaml", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "YAML";
                else if (word.Equals("oauth", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "OAuth";
                else if (word.Equals("cdn", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "CDN";
                else if (word.Equals("rg", StringComparison.OrdinalIgnoreCase)) wordsList[i] = "Resource group";
                // For the first word, capitalize it
                else if (i == 0)
                {
                    wordsList[i] = char.ToUpper(word[0]) + word.Substring(1).ToLower();
                }
                // For all other words, keep them lowercase
                else
                {
                    wordsList[i] = word.ToLower();
                }
            }
            
            return string.Join(" ", wordsList);
        });
        
        // Equality comparison helper
        handlebars.RegisterHelper("eq", (context, arguments) =>
        {
            if (arguments.Length < 2)
                return false;
                
            var left = arguments[0]?.ToString();
            var right = arguments[1]?.ToString();
            
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        });
        
        // String replacement helper
        handlebars.RegisterHelper("replace", (context, arguments) =>
        {
            if (arguments.Length < 3) return arguments.Length > 0 ? arguments[0]?.ToString() : string.Empty;
            
            var str = arguments[0]?.ToString() ?? string.Empty;
            var oldValue = arguments[1]?.ToString() ?? string.Empty;
            var newValue = arguments[2]?.ToString() ?? string.Empty;
            
            return str.Replace(oldValue, newValue);
        });

        // Group by property helper
        handlebars.RegisterHelper("groupBy", (context, arguments) =>
        {
            if (arguments.Length < 2) return new Dictionary<string, object>();
            
            var collection = arguments[0];
            var propertyName = arguments[1]?.ToString();
            
            if (propertyName == null) return new Dictionary<string, object>();
            
            var grouped = new Dictionary<string, List<object>>();
            
            if (collection is IEnumerable<CommonParameter> commonParams)
            {
                foreach (var item in commonParams)
                {
                    var keyValue = typeof(CommonParameter).GetProperty(propertyName)?.GetValue(item)?.ToString() ?? "Unknown";
                    
                    if (!grouped.ContainsKey(keyValue))
                        grouped[keyValue] = new List<object>();
                    
                    grouped[keyValue].Add(item);
                }
            }
            else if (collection is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    
                    var keyValue = "Unknown";
                    var itemType = item.GetType();
                    var property = itemType.GetProperty(propertyName);
                    
                    if (property != null)
                    {
                        keyValue = property.GetValue(item)?.ToString() ?? "Unknown";
                    }
                    
                    if (!grouped.ContainsKey(keyValue))
                        grouped[keyValue] = new List<object>();
                    
                    grouped[keyValue].Add(item);
                }
            }
            
            return grouped;
        });

        // Generate simple, clean tool names for H2 headers
        handlebars.RegisterHelper("getSimpleToolName", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return "Unknown";

            var command = arguments[0].ToString() ?? string.Empty;
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 2) // Need at least "area operation" 
                return "Unknown";
            
            // Skip area name (first part), get remaining parts
            var remainingParts = parts.Skip(1).ToArray();
            
            // Format each part with proper capitalization
            var formattedParts = remainingParts.Select(part =>
            {
                // Handle hyphenated terms
                if (part.Contains('-'))
                {
                    var hyphenParts = part.Split('-')
                        .Where(hp => !string.IsNullOrEmpty(hp))
                        .Select(hp => char.ToUpper(hp[0]) + hp.Substring(1).ToLower());
                    return string.Join("-", hyphenParts);
                }
                
                // Regular term - capitalize first letter
                return char.ToUpper(part[0]) + part.Substring(1).ToLower();
            });
            
            return string.Join(" ", formattedParts);
        });
    }
}
