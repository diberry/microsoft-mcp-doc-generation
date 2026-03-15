// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using ExamplePromptValidator;
using Shared;

namespace ExamplePromptValidatorCli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var generatedDir = string.Empty;
        var toolsDir = string.Empty;
        var examplePromptsDir = string.Empty;
        var filterToolCommand = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--generated", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                generatedDir = args[++i];
                continue;
            }

            if (arg.Equals("--tools-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                toolsDir = args[++i];
                continue;
            }

            if (arg.Equals("--example-prompts-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                examplePromptsDir = args[++i];
                continue;
            }

            if (arg.Equals("--tool-command", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                filterToolCommand = args[++i];
                continue;
            }
        }

        if (string.IsNullOrWhiteSpace(generatedDir))
        {
            generatedDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "generated"));
        }

        if (string.IsNullOrWhiteSpace(toolsDir))
        {
            toolsDir = Path.Combine(generatedDir, "tools");
        }

        if (string.IsNullOrWhiteSpace(examplePromptsDir))
        {
            examplePromptsDir = Path.Combine(generatedDir, "example-prompts");
        }

        Console.WriteLine("Example Prompt Validator");
        Console.WriteLine("========================");
        Console.WriteLine($"Generated directory: {generatedDir}");
        Console.WriteLine($"Example prompts directory: {examplePromptsDir}");
        if (!string.IsNullOrWhiteSpace(filterToolCommand))
        {
            Console.WriteLine($"Filtering to tool: {filterToolCommand}");
        }
        Console.WriteLine();
        Console.Out.Flush();

        if (!Directory.Exists(examplePromptsDir))
        {
            Console.Error.WriteLine($"Error: Example prompts directory not found: {examplePromptsDir}");
            return 1;
        }

        // Load CLI output to get tool commands and map to example prompt files
        var cliOutputFile = Path.Combine(generatedDir, "cli", "cli-output.json");
        if (!File.Exists(cliOutputFile))
        {
            Console.Error.WriteLine($"Error: CLI output file not found: {cliOutputFile}");
            return 1;
        }

        var cliJson = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(cliOutputFile));
        var allTools = cliJson.RootElement.GetProperty("results").EnumerateArray().ToList();
        
        // Filter by tool command if specified
        var tools = string.IsNullOrWhiteSpace(filterToolCommand)
            ? allTools
            : allTools.Where(t => 
                t.TryGetProperty("command", out var cmdProp) && 
                cmdProp.GetString()?.Equals(filterToolCommand, StringComparison.OrdinalIgnoreCase) == true
            ).ToList();
        
        if (!string.IsNullOrWhiteSpace(filterToolCommand) && tools.Count == 0)
        {
            Console.Error.WriteLine($"Error: Tool not found: {filterToolCommand}");
            return 1;
        }

        var validator = new PromptValidator();
        if (!validator.IsInitialized())
        {
            Console.Error.WriteLine("Error: Validator not initialized. Check Azure OpenAI configuration and prompt files.");
            return 1;
        }

        var totalTools = 0;
        var validated = 0;
        var valid = 0;
        var invalid = 0;
        var skipped = 0;
        var invalidTools = new List<string>();

        var validationDir = Path.Combine(generatedDir, "example-prompts-validation");
        Directory.CreateDirectory(validationDir);

        // Load shared data files for deterministic filename generation (matches ExamplePromptGeneratorStandalone)
        var nameContext = await FileNameContext.CreateAsync();

        async Task WriteValidationFileAsync(string baseName, string content)
        {
            var validationPath = Path.Combine(validationDir, $"{baseName}-validation.md");
            await File.WriteAllTextAsync(validationPath, content);
        }

        foreach (var toolElement in tools)
        {
            var command = toolElement.GetProperty("command").GetString();
            if (string.IsNullOrWhiteSpace(command))
                continue;

            totalTools++;
            
            // Debug: Check if we're processing this command multiple times
            var toolId = toolElement.TryGetProperty("id", out var idElem) ? idElem.GetString() : "no-id";
            
            // Use shared filename builder to match ExamplePromptGeneratorStandalone naming
            var baseName = ToolFileNameBuilder.BuildBaseFileName(command, nameContext);
            var examplePromptFileName = ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext);
            var examplePromptFile = Path.Combine(examplePromptsDir, examplePromptFileName);

            if (!File.Exists(examplePromptFile))
            {
                Console.WriteLine($"⚠️  {command} (example prompts file not found)");
                skipped++;
                var missingFileReport = new StringBuilder()
                    .AppendLine($"# Example Prompt Validation: {command}")
                    .AppendLine()
                    .AppendLine($"**Status:** Skipped (example prompts file not found)")
                    .AppendLine($"**Expected File:** {examplePromptFile}")
                    .ToString();
                await WriteValidationFileAsync(baseName, missingFileReport);
                continue;
            }

            // Build tool context for validation
            var toolName = toolElement.GetProperty("name").GetString() ?? "unknown";
            var toolDescription = toolElement.GetProperty("description").GetString() ?? "";
            
            // Handle optional parameters
            var options = new List<System.Text.Json.JsonElement>();
            if (toolElement.TryGetProperty("option", out var optionElement) && optionElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                options = optionElement.EnumerateArray().ToList();
            }
            
            var requiredParams = options.Where(o => o.TryGetProperty("required", out var req) && req.GetBoolean()).ToList();
            
            // Show file being checked
            Console.WriteLine($"\n📄 {command} (ID: {toolId?.Substring(0, 8)})");
            Console.WriteLine($"   File: {examplePromptFile}");
            
            // Handle tools with no parameters
            if (options.Count == 0)
            {
                Console.WriteLine($"   0️⃣  Tool has zero parameters");
                validated++;
                valid++;
                Console.WriteLine($"   ⏭️  SKIPPED - No parameters to validate");
                Console.Out.Flush();
                var zeroParamsReport = new StringBuilder()
                    .AppendLine($"# Example Prompt Validation: {command}")
                    .AppendLine()
                    .AppendLine($"**Status:** Skipped (zero parameters)")
                    .AppendLine($"**Example Prompts File:** {examplePromptFile}")
                    .ToString();
                await WriteValidationFileAsync(baseName, zeroParamsReport);
                continue;
            }
            
            // Handle tools with no required parameters
            if (requiredParams.Count == 0)
            {
                Console.WriteLine($"   0️⃣  Tool has zero required parameters ({options.Count} optional)");
                validated++;
                valid++;
                Console.WriteLine($"   ⏭️  SKIPPED - No required parameters to validate");
                Console.Out.Flush();
                var zeroRequiredReport = new StringBuilder()
                    .AppendLine($"# Example Prompt Validation: {command}")
                    .AppendLine()
                    .AppendLine($"**Status:** Skipped (zero required parameters)")
                    .AppendLine($"**Example Prompts File:** {examplePromptFile}")
                    .AppendLine($"**Optional Parameters:** {options.Count}")
                    .ToString();
                await WriteValidationFileAsync(baseName, zeroRequiredReport);
                continue;
            }

            // Read example prompts file
            var examplePromptsContent = await File.ReadAllTextAsync(examplePromptFile);
            
            // Build parameter list
            var parameterLines = options.Select(o => {
                var name = o.TryGetProperty("name", out var nameElem) ? nameElem.GetString() : "unknown";
                var required = o.TryGetProperty("required", out var reqElem) && reqElem.GetBoolean();
                var desc = o.TryGetProperty("description", out var descElem) ? descElem.GetString() : "No description";
                return $"- `{name}` ({(required ? "Required" : "Optional")}): {desc}";
            }).ToList();
            
            // Build tool file content for validation (simplified format with just what we need)
            var toolContent = $"## Tool: {toolName}\n\n" +
                $"**Command:** {command}\n\n" +
                $"**Description:** {toolDescription}\n\n" +
                $"### Parameters\n\n" +
                string.Join("\n", parameterLines) + "\n\n" +
                $"### Example Prompts\n\n" +
                examplePromptsContent;

            // Validate with LLM
            var result = await validator.ValidateWithLLMAsync(toolContent);

            if (result == null)
            {
                Console.WriteLine($"⚠️  {command} (validation failed)");
                skipped++;
                var failedReport = new StringBuilder()
                    .AppendLine($"# Example Prompt Validation: {command}")
                    .AppendLine()
                    .AppendLine($"**Status:** Failed (LLM validation returned no result)")
                    .AppendLine($"**Example Prompts File:** {examplePromptFile}")
                    .ToString();
                await WriteValidationFileAsync(baseName, failedReport);
                continue;
            }

            validated++;
            
            // Show required parameters
            if (result.RequiredParameters != null && result.RequiredParameters.Count > 0)
            {
                Console.WriteLine($"   Required: {string.Join(", ", result.RequiredParameters)}");
            }
            
            var reportBuilder = new StringBuilder()
                .AppendLine($"# Example Prompt Validation: {command}")
                .AppendLine()
                .AppendLine($"**Example Prompts File:** {examplePromptFile}")
                .AppendLine($"**Required Parameters:** {string.Join(", ", result.RequiredParameters ?? new List<string>())}")
                .AppendLine($"**Total Prompts:** {result.TotalPrompts}")
                .AppendLine($"**Valid Prompts:** {result.ValidPrompts}")
                .AppendLine($"**Invalid Prompts:** {result.InvalidPrompts}")
                .AppendLine();

            if (result.IsValid)
            {
                valid++;
                Console.WriteLine($"   ✅ VALID - All prompts contain required parameters with correct quoting");
                reportBuilder.AppendLine("**Status:** Valid");
                if (!string.IsNullOrEmpty(result.Summary))
                {
                    reportBuilder.AppendLine($"**Summary:** {result.Summary}");
                }
                await WriteValidationFileAsync(baseName, reportBuilder.ToString());
                Console.Out.Flush();
            }
            else
            {
                invalid++;
                invalidTools.Add(command);
                Console.WriteLine($"   ❌ INVALID - {result.InvalidPrompts}/{result.TotalPrompts} prompts have issues");
                reportBuilder.AppendLine("**Status:** Invalid");
                
                // Show summary first
                if (!string.IsNullOrEmpty(result.Summary))
                {
                    Console.WriteLine($"   📝 {result.Summary}");
                    reportBuilder.AppendLine($"**Summary:** {result.Summary}");
                }
                
                // Show details about invalid prompts
                if (result.Validation != null && result.Validation.Any())
                {
                    var invalidCount = 0;
                    foreach (var validation in result.Validation)
                    {
                        if (!validation.IsValid)
                        {
                            invalidCount++;
                            if (invalidCount <= 3) // Show first 3 invalid prompts
                            {
                                var prompt = validation.Prompt?.Length > 60 ? validation.Prompt.Substring(0, 60) + "..." : validation.Prompt;
                                Console.WriteLine($"\n   ❌ Prompt {invalidCount}: {prompt}");
                                
                                if (validation.MissingParameters != null && validation.MissingParameters.Count > 0)
                                {
                                    Console.WriteLine($"      Missing params: {string.Join(", ", validation.MissingParameters)}");
                                    reportBuilder.AppendLine($"- Missing params: {string.Join(", ", validation.MissingParameters)}");
                                }
                                
                                if (validation.FoundParameters != null && validation.FoundParameters.Count > 0)
                                {
                                    Console.WriteLine($"      Found params: {string.Join(", ", validation.FoundParameters)}");
                                    reportBuilder.AppendLine($"- Found params: {string.Join(", ", validation.FoundParameters)}");
                                }
                                
                                if (validation.Issues != null && validation.Issues.Count > 0)
                                {
                                    foreach (var issue in validation.Issues)
                                    {
                                        Console.WriteLine($"      Issue: {issue}");
                                        reportBuilder.AppendLine($"- Issue: {issue}");
                                    }
                                }
                            }
                        }
                    }
                    if (invalidCount > 3)
                    {
                        Console.WriteLine($"\n   ... and {invalidCount - 3} more invalid prompts");
                    }
                }
                
                // Show recommendations
                if (result.Recommendations != null && result.Recommendations.Count > 0)
                {
                    Console.WriteLine($"   💡 Recommendations:");
                    foreach (var rec in result.Recommendations.Take(3))
                    {
                        Console.WriteLine($"      - {rec}");
                    }
                    reportBuilder.AppendLine();
                    reportBuilder.AppendLine("**Recommendations:**");
                    foreach (var rec in result.Recommendations)
                    {
                        reportBuilder.AppendLine($"- {rec}");
                    }
                }
                
                await WriteValidationFileAsync(baseName, reportBuilder.ToString());
                Console.Out.Flush();
            }
        }

        Console.WriteLine();
        Console.WriteLine("Validation Summary");
        Console.WriteLine("------------------");
        Console.WriteLine($"Total tools: {totalTools}");
        Console.WriteLine($"Validated: {validated}");
        Console.WriteLine($"Valid: {valid}");
        Console.WriteLine($"Invalid: {invalid}");
        Console.WriteLine($"Skipped: {skipped}");

        if (invalidTools.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Invalid tools:");
            foreach (var tool in invalidTools)
            {
                Console.WriteLine($"  - {tool}");
            }
        }

        return invalid > 0 || validated == 0 ? 1 : 0;
    }
}
