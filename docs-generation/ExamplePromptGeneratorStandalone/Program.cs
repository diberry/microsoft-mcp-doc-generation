using System.Text;
using System.Text.Json;
using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using ExamplePromptGeneratorStandalone.Utilities;
using TemplateEngine;
using Shared;

namespace ExamplePromptGeneratorStandalone;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔═════════════════════════════════════════════╗");
        Console.WriteLine("║  Standalone Example Prompt Generator        ║");
        Console.WriteLine("╚═════════════════════════════════════════════╝\n");

        // Parse arguments
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: ExamplePromptGeneratorStandalone <cliOutputFile> <outputDir> [version] [--e2e-prompts <path>]");
            Console.WriteLine("  cliOutputFile    - Path to cli-output.json");
            Console.WriteLine("  outputDir        - Output directory for generated files");
            Console.WriteLine("  version          - (Optional) CLI version string. If not provided, reads from cli-version.json");
            Console.WriteLine("  --e2e-prompts    - (Optional) Path to parsed.json from E2eTestPromptParser");
            Console.WriteLine("\nNote: Templates and prompts are embedded in the package.");
            return 1;
        }

        var cliOutputFile = args[0];
        var outputDir = args[1];
        LogFileHelper.Initialize("example-prompts");
        string? e2ePromptsPath = null;

        // Scan for named flags first (they can appear anywhere after positional args)
        string? versionArg = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--e2e-prompts" && i + 1 < args.Length)
            {
                e2ePromptsPath = args[++i];
            }
            else if (versionArg == null && !args[i].StartsWith("--"))
            {
                versionArg = args[i];
            }
        }

        // Get version from arguments or read from cli-version.json
        string version = versionArg ?? await CliVersionReader.ReadCliVersionAsync(outputDir);
        
        // Use embedded templates/prompts from package folder
        var packageRootDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
        var templatesDir = Path.Combine(packageRootDir, "templates");
        var promptsDir = Path.Combine(packageRootDir, "prompts");

        // Validate paths
        if (!File.Exists(cliOutputFile))
        {
            Console.WriteLine($"❌ CLI output file not found: {cliOutputFile}");
            return 1;
        }

        if (!Directory.Exists(templatesDir))
        {
            Console.WriteLine($"❌ Templates directory not found: {templatesDir}");
            return 1;
        }

        if (!Directory.Exists(promptsDir))
        {
            Console.WriteLine($"❌ Prompts directory not found: {promptsDir}");
            return 1;
        }

        // Create output directory
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(Path.Combine(outputDir, "example-prompts"));
        Directory.CreateDirectory(Path.Combine(outputDir, "example-prompts-prompts"));
        Directory.CreateDirectory(Path.Combine(outputDir, "example-prompts-raw-output"));

        // Load e2e test prompts if provided
        Dictionary<string, List<string>>? e2eLookup = null;
        if (!string.IsNullOrEmpty(e2ePromptsPath))
        {
            if (File.Exists(e2ePromptsPath))
            {
                try
                {
                    var e2eJson = await File.ReadAllTextAsync(e2ePromptsPath);
                    var e2eData = JsonSerializer.Deserialize<E2eTestPromptsData>(e2eJson);
                    if (e2eData != null)
                    {
                        e2eLookup = E2eTestPromptsLookup.BuildLookup(e2eData);
                        Console.WriteLine($"✅ Loaded e2e test prompts: {e2eLookup.Count} tools, {e2eLookup.Values.Sum(v => v.Count)} prompts");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Failed to load e2e prompts (falling back to default): {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"⚠️  e2e prompts file not found (falling back to default): {e2ePromptsPath}");
            }
        }

        Console.WriteLine($"📂 CLI output:  {Path.GetFullPath(cliOutputFile)}");
        Console.WriteLine($"📂 Output dir:  {Path.GetFullPath(outputDir)}");
        Console.WriteLine($"📂 Templates:   {Path.GetFullPath(templatesDir)}");
        Console.WriteLine($"📂 Prompts:     {Path.GetFullPath(promptsDir)}");
        Console.WriteLine($"📂 E2e prompts: {(e2ePromptsPath != null ? Path.GetFullPath(e2ePromptsPath) : "(none)")}");
        Console.WriteLine($"📌 Version:     {version}\n");

        // Load CLI output
        Console.WriteLine("Loading CLI tools...");
        CliOutput? cliOutput;
        try
        {
            var json = await File.ReadAllTextAsync(cliOutputFile);
            cliOutput = JsonSerializer.Deserialize<CliOutput>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (cliOutput?.Results == null || cliOutput.Results.Count == 0)
            {
                Console.WriteLine("❌ No tools found in CLI output");
                return 1;
            }

            // Count tools with commands (tools without commands are skipped)
            var toolsWithCommands = cliOutput.Results.Count(t => !string.IsNullOrEmpty(t.Command));
            Console.WriteLine($"✅ Loaded {cliOutput.Results.Count} tools ({toolsWithCommands} with commands)\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to load CLI output: {ex.Message}");
            return 1;
        }

        // Initialize generator (uses embedded prompts)
        var generator = new ExamplePromptGenerator();
        if (!generator.IsInitialized)
        {
            Console.WriteLine("❌ Generator not initialized (Azure OpenAI credentials missing?)");
            return 1;
        }

        // Calculate estimated time (2-4 seconds per tool with Azure OpenAI)
        var toolCount = cliOutput.Results.Count(t => !string.IsNullOrEmpty(t.Command));
        var estimatedMinSeconds = toolCount * 2;
        var estimatedMaxSeconds = toolCount * 4;
        var estimatedMinMinutes = (int)Math.Ceiling(estimatedMinSeconds / 60.0);
        var estimatedMaxMinutes = (int)Math.Ceiling(estimatedMaxSeconds / 60.0);

        Console.WriteLine("Generating user prompts and calling Azure OpenAI for each tool...");
        Console.WriteLine($"⏱️  Estimated time: {estimatedMinMinutes}-{estimatedMaxMinutes} minutes for {toolCount} tools");
        Console.WriteLine($"    (You can safely cancel with Ctrl+C after a few successes to verify setup)\n");

        // Load shared data files for deterministic filename generation
        var nameContext = await FileNameContext.CreateAsync();

        var successCount = 0;
        var failureCount = 0;
        var e2eMatchCount = 0;
        var e2eMissCount = 0;
        var e2eLogEntries = new List<string>();

        // Create e2e error log directory if e2e prompts are loaded
        var e2eErrorLogDir = Path.Combine(outputDir, "example-prompts-e2e-errors");
        if (e2eLookup != null)
        {
            Directory.CreateDirectory(e2eErrorLogDir);
        }

        foreach (var tool in cliOutput.Results)
        {
            if (string.IsNullOrEmpty(tool.Command))
                continue;

            // Look up e2e reference prompts for this tool
            List<string>? referencePrompts = null;
            e2eLookup?.TryGetValue(tool.Command!, out referencePrompts);

            // Track e2e coverage
            if (e2eLookup != null)
            {
                if (referencePrompts != null && referencePrompts.Count > 0)
                {
                    e2eMatchCount++;
                    e2eLogEntries.Add($"MATCH  | {tool.Command,-55} | {referencePrompts.Count} reference prompts");
                }
                else
                {
                    e2eMissCount++;
                    e2eLogEntries.Add($"MISS   | {tool.Command,-55} | no e2e entry — using default (5 prompts)");
                    Console.WriteLine($"  ⚠️  No e2e reference for: {tool.Command} (using default)");

                    // Write individual error file per missing tool
                    var toolSlug = tool.Command!.Replace(' ', '-');
                    var errorFilePath = Path.Combine(e2eErrorLogDir, $"{toolSlug}-error.log");
                    var errorContent = $"Tool command: {tool.Command}\n"
                        + $"Tool name:    {tool.Name}\n"
                        + $"Timestamp:    {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n"
                        + $"\n"
                        + $"This tool has no entry in the e2e test prompts (parsed.json).\n"
                        + $"The example prompt generator fell back to default behavior (5 AI-generated prompts).\n"
                        + $"\n"
                        + $"To fix: Add this tool to the upstream e2eTestPrompts.md in the microsoft/mcp repo,\n"
                        + $"then re-run E2eTestPromptParser to regenerate parsed.json.\n";
                    await File.WriteAllTextAsync(errorFilePath, errorContent);
                }
            }

            var result = await generator.GenerateAsync(tool, referencePrompts);
            if (!result.HasValue)
            {
                failureCount++;
                Console.WriteLine($"  ❌ {tool.Command}");
                continue;
            }

            var (userPrompt, promptsResponse, rawResponse) = result.Value;

            // Use shared deterministic filename builder
            var inputPromptFileName = ToolFileNameBuilder.BuildInputPromptFileName(
                tool.Command, nameContext);
            var inputPromptPath = Path.Combine(outputDir, "example-prompts-prompts", inputPromptFileName);
            var inputContent = FrontmatterUtility.GenerateInputPromptFrontmatter(
                tool.Command, version, inputPromptFileName, userPrompt);
            await File.WriteAllTextAsync(inputPromptPath, inputContent);

            // Save raw AI response (extract just the JSON for clarity)
            var rawOutputFileName = ToolFileNameBuilder.BuildRawOutputFileName(
                tool.Command, nameContext);
            var rawOutputPath = Path.Combine(outputDir, "example-prompts-raw-output", rawOutputFileName);
            var jsonOnlyContent = ExtractJsonFromResponse(rawResponse);
            await File.WriteAllTextAsync(rawOutputPath, jsonOnlyContent);

            if (promptsResponse == null || promptsResponse.Prompts.Count == 0)
            {
                failureCount++;
                Console.WriteLine($"  ❌ {tool.Command} (JSON parse failed - raw output saved)");
                continue;
            }

            successCount++;

            // Generate example prompts markdown
            var examplePromptFileName = ToolFileNameBuilder.BuildExamplePromptsFileName(
                tool.Command, nameContext);
            var examplePromptPath = Path.Combine(outputDir, "example-prompts", examplePromptFileName);

            var templatePath = Path.Combine(templatesDir, "example-prompts-template.hbs");
            var frontmatter = FrontmatterUtility.GenerateExamplePromptsFrontmatter(version);
            var promptsList = promptsResponse.Prompts.Select(p => new { prompt = p }).ToList();

            // Get required parameters for the template comment
            var requiredParams = tool.Option?.Where(o => o.Required).ToList() ?? new List<Models.Option>();
            var requiredParamNames = string.Join(", ", requiredParams.Select(p => $"'{p.Name}'"));

            string exampleContent;
            if (File.Exists(templatePath))
            {
                var templateContext = new Dictionary<string, object>
                {
                    ["version"] = version ?? "unknown",
                    ["generatedAt"] = DateTime.UtcNow,
                    ["command"] = tool.Command,
                    ["examplePrompts"] = promptsList,
                    ["requiredParamCount"] = requiredParams.Count,
                    ["requiredParamNames"] = requiredParamNames
                };

                var templateOutput = await HandlebarsTemplateEngine.ProcessTemplateAsync(templatePath, templateContext);
                exampleContent = templateOutput;
            }
            else
            {
                // Fallback: manual markdown generation
                var sb = new System.Text.StringBuilder(frontmatter);
                sb.AppendLine($"<!-- @mcpcli {tool.Command} -->");
                sb.AppendLine($"<!-- Required parameters: {requiredParams.Count} - {requiredParamNames} -->\n");
                sb.AppendLine("Example prompts include:\n");
                foreach (var prompt in promptsResponse.Prompts)
                {
                    sb.AppendLine($"- \"{prompt}\"");
                }
                exampleContent = sb.ToString();
            }

            await File.WriteAllTextAsync(examplePromptPath, exampleContent);
            Console.WriteLine($"  ✅ {tool.Command,-50} → {examplePromptFileName}");
        }

        Console.WriteLine($"\n📊 Summary:");
        Console.WriteLine($"  ✅ Generated: {successCount}");
        Console.WriteLine($"  ❌ Failed:    {failureCount}");
        if (e2eLookup != null)
        {
            Console.WriteLine($"  📋 E2e match: {e2eMatchCount}");
            Console.WriteLine($"  ⚠️  E2e miss: {e2eMissCount}");
        }
        Console.WriteLine($"  📁 Output:    {Path.GetFullPath(outputDir)}\n");

        // Write e2e coverage log
        if (e2eLookup != null && e2eLogEntries.Count > 0)
        {
            var logDir = Path.Combine(outputDir, "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "example-prompt-e2e-coverage.log");

            var logBuilder = new StringBuilder();

            // Append a separator if the log file already exists (multi-namespace runs)
            if (File.Exists(logPath))
            {
                logBuilder.AppendLine();
                logBuilder.AppendLine($"# ═══════════════════════════════════════════════════════════════════════");
                logBuilder.AppendLine();
            }

            logBuilder.AppendLine($"# Example Prompt E2e Coverage Report");
            logBuilder.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            logBuilder.AppendLine($"# E2e source: {(e2ePromptsPath != null ? Path.GetFullPath(e2ePromptsPath) : "(none)")}");
            logBuilder.AppendLine($"#");
            logBuilder.AppendLine($"# MATCH: {e2eMatchCount} tools had e2e reference prompts");
            logBuilder.AppendLine($"# MISS:  {e2eMissCount} tools had NO e2e reference prompts (used default 5)");
            logBuilder.AppendLine($"# TOTAL: {e2eMatchCount + e2eMissCount} tools processed");
            logBuilder.AppendLine($"#");
            logBuilder.AppendLine($"# STATUS | {"TOOL COMMAND",-55} | DETAILS");
            logBuilder.AppendLine($"# {new string('-', 80)}");
            foreach (var entry in e2eLogEntries)
            {
                logBuilder.AppendLine(entry);
            }

            await File.AppendAllTextAsync(logPath, logBuilder.ToString());
            Console.WriteLine($"  📋 E2e log:   {Path.GetFullPath(logPath)}");
        }

        return failureCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Extracts JSON from LLM response that may contain reasoning/preamble.
    /// Prioritizes finding the last JSON object, as LLM often puts final answer at the end.
    /// </summary>
    internal static string ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return string.Empty;

        var jsonText = response.Trim();

        // Strategy 1: Look for ```json code block
        if (jsonText.Contains("```json"))
        {
            var start = jsonText.IndexOf("```json") + 7;
            var end = jsonText.IndexOf("```", start);
            if (end > start)
                return jsonText.Substring(start, end - start).Trim();
        }

        // Strategy 2: Find the LAST code block (most likely to be the final JSON)
        if (jsonText.Contains("```"))
        {
            var lastBlockStart = jsonText.LastIndexOf("```");
            // Look backwards for the previous ``` to find the start of this block
            var prevBlockEnd = jsonText.LastIndexOf("```", lastBlockStart - 1);
            if (prevBlockEnd >= 0)
            {
                var blockContent = jsonText.Substring(prevBlockEnd + 3, lastBlockStart - prevBlockEnd - 3).Trim();
                // Verify it's JSON (starts with '{')
                if (blockContent.StartsWith("{"))
                    return blockContent;
            }
        }

        // Strategy 3: Find the last occurrence of { followed by the last }
        // This handles cases where JSON appears without code blocks
        var lastBrace = jsonText.LastIndexOf('}');
        if (lastBrace >= 0)
        {
            // Search backwards from the last } to find the matching {
            var braceCount = 1;
            for (int i = lastBrace - 1; i >= 0; i--)
            {
                if (jsonText[i] == '}') braceCount++;
                else if (jsonText[i] == '{') braceCount--;
                
                if (braceCount == 0)
                {
                    return jsonText.Substring(i, lastBrace - i + 1).Trim();
                }
            }
        }

        return response; // Return as-is if no JSON found
    }
}
