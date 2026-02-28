// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SkillsRelevance.Models;
using SkillsRelevance.Output;
using SkillsRelevance.Services;
using Shared;

namespace SkillsRelevance;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║  Azure MCP Skills Relevance Analyzer         ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.WriteLine();

        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        // Parse arguments
        string? serviceNameArg = null;
        string outputPath = "../../generated/skills-relevance";
        double minScore = 0.1;
        bool allSkills = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output-path":
                    if (i + 1 < args.Length) outputPath = args[++i];
                    break;
                case "--min-score":
                    if (i + 1 < args.Length && double.TryParse(args[++i], out var parsedScore))
                        minScore = parsedScore;
                    break;
                case "--all-skills":
                    allSkills = true;
                    break;
                default:
                    if (!args[i].StartsWith("--", StringComparison.Ordinal) && serviceNameArg == null)
                        serviceNameArg = args[i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(serviceNameArg))
        {
            Console.Error.WriteLine("Error: Azure service name or MCP namespace is required.");
            Console.Error.WriteLine("Example: SkillsRelevance aks");
            PrintUsage();
            return 1;
        }

        LogFileHelper.Initialize("skills-relevance");

        var serviceName = serviceNameArg;
        var outputDir = Path.GetFullPath(outputPath);
        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        Console.WriteLine($"Service/Namespace: {serviceName}");
        Console.WriteLine($"Output directory:  {outputDir}");
        Console.WriteLine($"Minimum relevance: {minScore:F2}");
        Console.WriteLine($"GitHub token:      {(githubToken != null ? "✓ set" : "⚠️  not set (rate limits apply)")}");
        Console.WriteLine();

        try
        {
            var sources = SkillSource.Defaults.ToList();
            var fetcher = new GitHubSkillsFetcher(githubToken);
            var analyzer = new SkillRelevanceAnalyzer(serviceName);
            var allFetchedSkills = new List<SkillInfo>();

            // Fetch skills from all sources
            Console.WriteLine($"Fetching skills from {sources.Count} source(s)...");
            foreach (var source in sources)
            {
                Console.WriteLine($"  📦 {source.DisplayName}...");
                var files = await fetcher.FetchSkillsAsync(source);
                Console.WriteLine($"     Found {files.Count} skill file(s)");

                foreach (var (entry, content) in files)
                {
                    var skill = SkillContentParser.Parse(
                        entry.Name,
                        content,
                        entry.HtmlUrl,
                        entry.DownloadUrl ?? entry.Url,
                        source.DisplayName);
                    allFetchedSkills.Add(skill);
                }
            }

            Console.WriteLine($"\nTotal skills fetched: {allFetchedSkills.Count}");

            // Analyze relevance
            Console.WriteLine($"Analyzing relevance for '{serviceName}'...");
            var effectiveMinScore = allSkills ? 0.0 : minScore;
            var relevantSkills = analyzer.FilterAndSort(allFetchedSkills, effectiveMinScore);

            Console.WriteLine($"Relevant skills:      {relevantSkills.Count} (min score: {effectiveMinScore:F2})");
            Console.WriteLine();

            // Write output
            Console.WriteLine($"Writing output to: {outputDir}");
            await SkillsMarkdownWriter.WriteServiceSummaryAsync(outputDir, serviceName, relevantSkills, sources);
            await SkillsMarkdownWriter.WriteIndexAsync(outputDir, new List<string> { serviceName });

            Console.WriteLine();
            Console.WriteLine("✅ Done!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: SkillsRelevance <service-name-or-namespace> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <service-name-or-namespace>  Azure service name or MCP namespace (e.g., 'aks', 'storage', 'keyvault')");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --output-path <path>   Output directory (default: ../../generated/skills-relevance)");
        Console.WriteLine("  --min-score <0.0-1.0>  Minimum relevance score to include (default: 0.1)");
        Console.WriteLine("  --all-skills           Include all skills regardless of relevance score");
        Console.WriteLine("  --help, -h             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Environment variables:");
        Console.WriteLine("  GITHUB_TOKEN           GitHub personal access token (recommended to avoid rate limits)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  SkillsRelevance aks");
        Console.WriteLine("  SkillsRelevance storage --output-path ./my-output");
        Console.WriteLine("  SkillsRelevance keyvault --min-score 0.2");
        Console.WriteLine("  SkillsRelevance kubernetes --all-skills");
    }
}
