// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using RelatedSkillsGenerator.Models;
using RelatedSkillsGenerator.Parsers;
using Shared;
using TemplateEngine;

namespace RelatedSkillsGenerator;

/// <summary>
/// Entry point for the Related Skills Generator.
/// Generates related-skills markdown pages linking MCP namespaces to Agent Skills
/// by parsing CATALOG.md and BUNDLES.md from the Agent-Skills repository.
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Azure MCP Related Skills Generator");
        Console.WriteLine("==================================");
        Console.WriteLine();

        try
        {
            LogFileHelper.Initialize("related-skills-generator");

            // Parse CLI arguments
            string? singleService = null;
            string? outputPath = null;
            string? skillsSource = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--single-service" && i + 1 < args.Length)
                {
                    singleService = args[i + 1];
                    i++;
                }
                else if (args[i] == "--output-path" && i + 1 < args.Length)
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

            // Resolve paths
            var scriptDir = AppContext.BaseDirectory;
            outputPath ??= Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "..", "..", "..", "generated"));
            skillsSource ??= Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "..", "..", "..", "docs-generation", "skills-source"));

            Console.WriteLine($"Output path:    {outputPath}");
            Console.WriteLine($"Skills source:  {skillsSource}");
            if (singleService != null)
            {
                Console.WriteLine($"Single service: {singleService}");
            }
            Console.WriteLine();

            // Load skill-to-namespace mapping
            var mappingFilePath = Path.Combine(DataFileLoader.GetDataDirectoryPath(), "skills-to-namespace-mapping.json");
            var mappings = await LoadSkillMappingsAsync(mappingFilePath);
            if (mappings.Count == 0)
            {
                Console.Error.WriteLine($"Error: No skill mappings found. Expected file at: {mappingFilePath}");
                return 1;
            }
            Console.WriteLine($"✓ Loaded {mappings.Count} skill mappings");

            // Load brand mappings
            var brandMappings = await DataFileLoader.LoadBrandMappingsAsync();
            Console.WriteLine($"✓ Loaded {brandMappings.Count} brand mappings");

            // Read CLI version
            var cliVersion = await CliVersionReader.ReadCliVersionAsync(outputPath);
            Console.WriteLine($"✓ CLI version: {cliVersion}");
            Console.WriteLine();

            // Parse CATALOG.md and BUNDLES.md
            var catalogPath = Path.Combine(skillsSource, "CATALOG.md");
            var bundlesPath = Path.Combine(skillsSource, "BUNDLES.md");

            var catalogSkills = CatalogParser.Parse(catalogPath);
            Console.WriteLine($"✓ Parsed {catalogSkills.Count} skills from CATALOG.md");

            var bundles = BundlesParser.Parse(bundlesPath);
            Console.WriteLine($"✓ Parsed {bundles.Count} bundles from BUNDLES.md");
            Console.WriteLine();

            // Build lookup by skill name
            var catalogLookup = catalogSkills
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Load the template
            var templateContent = LoadEmbeddedTemplate("templates.related-skills-template.hbs");
            if (templateContent == null)
            {
                Console.Error.WriteLine("Error: Could not load embedded template 'related-skills-template.hbs'");
                return 1;
            }

            // Filter mappings if single-service mode
            var targetMappings = singleService != null
                ? mappings.Where(m => m.McpNamespace.Equals(singleService, StringComparison.OrdinalIgnoreCase)).ToList()
                : mappings;

            if (singleService != null && targetMappings.Count == 0)
            {
                Console.Error.WriteLine($"Error: No mapping found for namespace '{singleService}'");
                return 1;
            }

            // Generate related-skills pages
            var outputDir = Path.Combine(outputPath, "related-skills");
            Directory.CreateDirectory(outputDir);

            var generatedCount = 0;
            var skippedCount = 0;

            foreach (var mapping in targetMappings)
            {
                if (mapping.Skills.Count == 0)
                {
                    LogFileHelper.WriteDebug($"Skipping {mapping.McpNamespace}: no skills mapped");
                    skippedCount++;
                    continue;
                }

                // Resolve brand name
                var brandName = mapping.BrandName;
                if (brandMappings.TryGetValue(mapping.McpNamespace, out var brand))
                {
                    brandName = brand.BrandName;
                }

                // Resolve skills from catalog
                var resolvedSkills = new List<ResolvedSkill>();
                var skillNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var skillRef in mapping.Skills)
                {
                    if (catalogLookup.TryGetValue(skillRef.Name, out var catalogSkill))
                    {
                        resolvedSkills.Add(new ResolvedSkill(
                            Name: catalogSkill.Name,
                            Description: catalogSkill.Description,
                            Category: catalogSkill.Category,
                            SkillUrl: catalogSkill.SkillUrl
                        ));
                        skillNameSet.Add(skillRef.Name);
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠ Skill '{skillRef.Name}' not found in CATALOG.md for {mapping.McpNamespace}");
                    }
                }

                // Find bundles containing any of the mapped skills
                var resolvedBundles = bundles
                    .Where(b => b.SkillNames.Any(s => skillNameSet.Contains(s)))
                    .Select(b => new ResolvedBundle(
                        Name: b.Name,
                        Description: b.Description,
                        BundleUrl: BundlesParser.GetBundleUrl(b.AnchorId)
                    ))
                    .ToList();

                if (resolvedSkills.Count == 0)
                {
                    LogFileHelper.WriteDebug($"Skipping {mapping.McpNamespace}: all skills unresolved");
                    skippedCount++;
                    continue;
                }

                var viewModel = new NamespaceSkillsViewModel(
                    McpNamespace: mapping.McpNamespace,
                    BrandName: brandName,
                    GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    CliVersion: cliVersion,
                    Skills: resolvedSkills,
                    Bundles: resolvedBundles
                );

                // Render template
                var templateData = new Dictionary<string, object>
                {
                    ["mcpNamespace"] = viewModel.McpNamespace,
                    ["brandName"] = viewModel.BrandName,
                    ["generatedAt"] = viewModel.GeneratedAt,
                    ["cliVersion"] = viewModel.CliVersion,
                    ["skills"] = viewModel.Skills,
                    ["bundles"] = viewModel.Bundles
                };

                var rendered = HandlebarsTemplateEngine.ProcessTemplateString(templateContent, templateData);

                var outputFile = Path.Combine(outputDir, $"related-skills-{mapping.McpNamespace}.md");
                await File.WriteAllTextAsync(outputFile, rendered);

                var bundleInfo = resolvedBundles.Count > 0 ? $", {resolvedBundles.Count} bundles" : "";
                Console.WriteLine($"  ✓ {mapping.McpNamespace} ({resolvedSkills.Count} skills{bundleInfo})");
                generatedCount++;
            }

            Console.WriteLine();
            Console.WriteLine($"✓ Generated {generatedCount} related-skills files");
            if (skippedCount > 0)
            {
                Console.WriteLine($"  Skipped {skippedCount} namespaces (no skills mapped or resolved)");
            }

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
    /// Loads skill-to-namespace mappings from the JSON configuration file.
    /// </summary>
    internal static async Task<List<SkillMapping>> LoadSkillMappingsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            LogFileHelper.WriteDebug($"Skill mappings file not found: {filePath}");
            return new List<SkillMapping>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var mappings = JsonSerializer.Deserialize<List<SkillMapping>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return mappings ?? new List<SkillMapping>();
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Error loading skill mappings: {ex.Message}");
            return new List<SkillMapping>();
        }
    }

    /// <summary>
    /// Loads an embedded resource template by its resource name suffix.
    /// </summary>
    private static string? LoadEmbeddedTemplate(string resourceNameSuffix)
    {
        var assembly = typeof(Program).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceNameSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            LogFileHelper.WriteDebug($"Embedded resource not found matching suffix: {resourceNameSuffix}");
            LogFileHelper.WriteDebug($"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
