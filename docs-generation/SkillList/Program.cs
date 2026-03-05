// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using RelatedSkillsGenerator.Models;
using RelatedSkillsGenerator.Parsers;
using Shared;
using SkillList.Models;
using TemplateEngine;

namespace SkillList;

/// <summary>
/// Generates a single catalog page listing all Azure Agent Skills
/// with name, related products, description, and GitHub link.
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Azure Agent Skills Catalog Generator");
        Console.WriteLine("====================================");
        Console.WriteLine();

        try
        {
            LogFileHelper.Initialize("skill-list-generator");

            // Parse CLI arguments
            string? outputPath = null;
            string? skillsSource = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--output-path" && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "--skills-source" && i + 1 < args.Length)
                {
                    skillsSource = args[i + 1];
                    i++;
                }
            }

            var scriptDir = AppContext.BaseDirectory;
            outputPath ??= Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "..", "..", "..", "generated"));
            skillsSource ??= Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "..", "..", "..", "docs-generation", "skills-source"));

            Console.WriteLine($"Output path:   {outputPath}");
            Console.WriteLine($"Skills source: {skillsSource}");
            Console.WriteLine();

            // Load mapping to resolve related products
            var mappingFilePath = Path.Combine(DataFileLoader.GetDataDirectoryPath(), "skills-to-namespace-mapping.json");
            var mappings = await LoadMappingsAsync(mappingFilePath);

            // Build reverse lookup: skill name → list of product brand names
            var skillToProducts = BuildSkillToProductsLookup(mappings);
            Console.WriteLine($"✓ Loaded product mappings ({skillToProducts.Count} skills mapped)");

            // Read CLI version
            var cliVersion = await CliVersionReader.ReadCliVersionAsync(outputPath);
            Console.WriteLine($"✓ CLI version: {cliVersion}");

            // Parse CATALOG.md
            var catalogPath = Path.Combine(skillsSource, "CATALOG.md");
            var catalogSkills = CatalogParser.Parse(catalogPath);
            Console.WriteLine($"✓ Parsed {catalogSkills.Count} skills from CATALOG.md");
            Console.WriteLine();

            // Build skill entries
            var skillEntries = catalogSkills
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Select(s => new SkillEntry(
                    Name: s.Name,
                    Description: s.Description,
                    SkillUrl: s.SkillUrl,
                    RelatedProducts: skillToProducts.GetValueOrDefault(s.Name, new List<string>()),
                    Category: s.Category
                ))
                .ToList();

            var mappedCount = skillEntries.Count(e => e.RelatedProducts.Count > 0);

            var viewModel = new SkillCatalogViewModel(
                GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-dd"),
                CliVersion: cliVersion,
                TotalSkills: skillEntries.Count,
                TotalWithMcpMapping: mappedCount,
                Skills: skillEntries
            );

            // Load and render template
            var templateContent = LoadEmbeddedTemplate("templates.skill-list-template.hbs");
            if (templateContent == null)
            {
                Console.Error.WriteLine("Error: Could not load embedded template 'skill-list-template.hbs'");
                return 1;
            }

            var templateData = new Dictionary<string, object>
            {
                ["generatedAt"] = viewModel.GeneratedAt,
                ["cliVersion"] = viewModel.CliVersion,
                ["totalSkills"] = viewModel.TotalSkills,
                ["totalWithMcpMapping"] = viewModel.TotalWithMcpMapping,
                ["skills"] = viewModel.Skills
            };

            var rendered = HandlebarsTemplateEngine.ProcessTemplateString(templateContent, templateData);

            // Write output
            var outputDir = Path.Combine(outputPath, "related-skills");
            Directory.CreateDirectory(outputDir);
            var outputFile = Path.Combine(outputDir, "skill-list.md");
            await File.WriteAllTextAsync(outputFile, rendered);

            Console.WriteLine($"✓ Generated skill-list.md ({skillEntries.Count} skills, {mappedCount} with MCP mappings)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// Builds a reverse lookup from skill name to list of related product brand names.
    /// </summary>
    internal static Dictionary<string, List<string>> BuildSkillToProductsLookup(List<SkillMapping> mappings)
    {
        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings)
        {
            // Skip the "other" catch-all — those skills have no product mapping
            if (mapping.McpNamespace.Equals("other", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var skill in mapping.Skills)
            {
                if (!lookup.ContainsKey(skill.Name))
                    lookup[skill.Name] = new List<string>();

                if (!lookup[skill.Name].Contains(mapping.BrandName, StringComparer.OrdinalIgnoreCase))
                    lookup[skill.Name].Add(mapping.BrandName);
            }
        }

        return lookup;
    }

    private static async Task<List<SkillMapping>> LoadMappingsAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return new List<SkillMapping>();

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<List<SkillMapping>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<SkillMapping>();
    }

    private static string? LoadEmbeddedTemplate(string resourceNameSuffix)
    {
        var assembly = typeof(Program).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceNameSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null) return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
