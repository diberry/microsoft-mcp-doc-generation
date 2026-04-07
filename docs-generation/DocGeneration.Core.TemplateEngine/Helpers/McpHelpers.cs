// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text.Json;
using HandlebarsDotNet;
using Shared;

namespace TemplateEngine.Helpers;

/// <summary>
/// MCP command structure helpers for Azure MCP documentation generation.
/// </summary>
public static class McpHelpers
{
    /// <summary>
    /// Maps CLI verb tokens to display verbs for H2 headings.
    /// </summary>
    private static readonly Dictionary<string, string> VerbMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["list"] = "List",
        ["show"] = "Get",
        ["get"] = "Get",
        ["describe"] = "Get",
        ["create"] = "Create",
        ["add"] = "Create",
        ["update"] = "Update",
        ["modify"] = "Update",
        ["set"] = "Set",
        ["delete"] = "Delete",
        ["remove"] = "Delete",
        ["query"] = "Query",
        ["search"] = "Search",
        ["retrieve"] = "Retrieve",
        ["check-name-availability"] = "Check Name Availability",
    };

    /// <summary>
    /// Generates a clean, verb-first tool name for H2 headers.
    /// Public for testability — the Handlebars helper delegates to this method.
    /// </summary>
    public static string GenerateSimpleToolName(string command, Dictionary<string, string>? compoundWords = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "Unknown";

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return "Unknown";

        // Skip area name (first part), get remaining segments
        var remaining = parts.Skip(1).ToArray();

        string? verb = null;
        string[] resourceParts;
        bool isList = false;

        // Detect verb: prefer last position ("area resource verb"), fallback to first ("area verb resource")
        if (VerbMap.TryGetValue(remaining[^1], out var mappedVerbLast))
        {
            verb = mappedVerbLast;
            isList = remaining[^1].Equals("list", StringComparison.OrdinalIgnoreCase);
            resourceParts = remaining.Length > 1 ? remaining[..^1] : Array.Empty<string>();
        }
        else if (remaining.Length > 1 && VerbMap.TryGetValue(remaining[0], out var mappedVerbFirst))
        {
            verb = mappedVerbFirst;
            isList = remaining[0].Equals("list", StringComparison.OrdinalIgnoreCase);
            resourceParts = remaining[1..];
        }
        else
        {
            resourceParts = remaining;
        }

        var formattedResource = FormatResourceParts(resourceParts, compoundWords);

        if (isList && !string.IsNullOrEmpty(formattedResource))
        {
            formattedResource = SimplePluralize(formattedResource);
        }

        string result;
        if (verb != null && !string.IsNullOrEmpty(formattedResource))
            result = $"{verb} {formattedResource}";
        else if (verb != null)
            result = verb;
        else
            result = formattedResource;

        // Strip "Area:" prefix pattern (defensive)
        var colonIdx = result.IndexOf(':');
        if (colonIdx > 0 && colonIdx < result.Length - 1)
        {
            result = result[(colonIdx + 1)..].Trim();
        }

        return string.IsNullOrWhiteSpace(result) ? "Unknown" : result;
    }

    private static string FormatResourceParts(string[] parts, Dictionary<string, string>? compoundWords)
    {
        var words = new List<string>();
        foreach (var part in parts)
        {
            var expanded = ApplyCompoundWords(part, compoundWords);
            foreach (var segment in expanded.Split('-', StringSplitOptions.RemoveEmptyEntries))
            {
                words.Add(TitleCaseWord(segment));
            }
        }
        return string.Join(" ", words);
    }

    private static string ApplyCompoundWords(string part, Dictionary<string, string>? compoundWords)
    {
        if (compoundWords != null && compoundWords.TryGetValue(part.ToLowerInvariant(), out var expanded))
            return expanded;
        return part;
    }

    private static string TitleCaseWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        return char.ToUpper(word[0]) + word.Substring(1).ToLower();
    }

    private static string SimplePluralize(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return phrase;

        var words = phrase.Split(' ');
        var lastWord = words[^1];

        // Already looks plural (ends with 's' but not 'ss')
        if (lastWord.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !lastWord.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            return phrase;
        }

        if (lastWord.EndsWith("ss", StringComparison.OrdinalIgnoreCase) ||
            lastWord.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            lastWord.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            lastWord.EndsWith("ch", StringComparison.OrdinalIgnoreCase))
        {
            words[^1] = lastWord + "es";
        }
        else if (lastWord.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                 lastWord.Length > 1 &&
                 !"aeiou".Contains(char.ToLower(lastWord[^2])))
        {
            words[^1] = lastWord[..^1] + "ies";
        }
        else
        {
            words[^1] = lastWord + "s";
        }

        return string.Join(" ", words);
    }
    /// <summary>
    /// Registers all MCP-specific helpers on the given Handlebars instance.
    /// </summary>
    public static void Register(IHandlebars handlebars)
    {
        // Get area count helper (generic — works with any dictionary type)
        handlebars.RegisterHelper("getAreaCount", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return 0;

            if (arguments[0] is JsonElement element && element.ValueKind == JsonValueKind.Object)
                return element.EnumerateObject().Count();

            if (arguments[0] is IDictionary dict)
                return dict.Count;

            return 0;
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
                    .Select(part =>
                    {
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

        // Group by property helper (generic — uses reflection)
        handlebars.RegisterHelper("groupBy", (context, arguments) =>
        {
            if (arguments.Length < 2) return new Dictionary<string, object>();

            var collection = arguments[0];
            var propertyName = arguments[1]?.ToString();

            if (propertyName == null) return new Dictionary<string, object>();

            var grouped = new Dictionary<string, List<object>>();

            if (collection is IEnumerable enumerable)
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

        // Generate simple, clean tool names for H2 headers (verb-first, compound words, no area prefix)
        var lazyCompoundWords = new Lazy<Dictionary<string, string>>(() =>
        {
            try { return DataFileLoader.LoadCompoundWordsAsync().GetAwaiter().GetResult(); }
            catch { return new Dictionary<string, string>(); }
        });

        handlebars.RegisterHelper("getSimpleToolName", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return "Unknown";

            var command = arguments[0].ToString() ?? string.Empty;
            return GenerateSimpleToolName(command, lazyCompoundWords.Value);
        });
    }
}
