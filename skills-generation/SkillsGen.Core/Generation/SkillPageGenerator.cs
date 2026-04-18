using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Generation;

public class SkillPageGenerator : ISkillPageGenerator
{
    private readonly ILogger<SkillPageGenerator> _logger;
    private readonly HandlebarsTemplate<object, object> _compiledTemplate;

    public SkillPageGenerator(string templateContent, ILogger<SkillPageGenerator> logger)
    {
        _logger = logger;
        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);
        _compiledTemplate = handlebars.Compile(templateContent);
    }

    public string Generate(SkillData skillData, TriggerData triggerData, TierAssessment tierAssessment, SkillPrerequisites prerequisites, Func<string, string>? triggerProcessor = null)
    {
        var context = BuildContext(skillData, triggerData, tierAssessment, prerequisites, triggerProcessor);
        var result = _compiledTemplate(context);
        return result;
    }

    private static readonly int MaxExamplePrompts = 10;

    private static object BuildContext(SkillData skillData, TriggerData triggerData, TierAssessment tierAssessment, SkillPrerequisites prerequisites, Func<string, string>? triggerProcessor = null)
    {
        // Build "When to use" from UseFor, falling back to trigger prompts if empty
        var rawUseFor = skillData.UseFor.Count > 0
            ? skillData.UseFor
            : triggerData.ShouldTrigger.Count > 0
                ? triggerData.ShouldTrigger.Take(7).ToList()
                : new List<string> { $"Manage and configure {skillData.DisplayName} resources in Azure" };

        // Naturalize the bullet points — convert keyword fragments into sentences
        var useForList = NaturalizeItems(rawUseFor, skillData.DisplayName);
        // Cap at 10 items max to keep the section focused
        if (useForList.Count > 10)
            useForList = useForList.Take(10).ToList();

        // Build "When NOT to use" — only from explicit DoNotUseFor in SKILL.md
        // Do NOT fall back to shouldNotTrigger: those are test prompts, not customer guidance
        var rawDoNotUseFor = skillData.DoNotUseFor.Count > 0
            ? skillData.DoNotUseFor
            : new List<string>();
        var doNotUseForList = NaturalizeItems(rawDoNotUseFor, skillData.DisplayName);

        // Cap example prompts and optionally post-process them
        // When trigger test files are missing, fall back to useFor/WHEN items as prompts
        List<string> examplePrompts;
        if (triggerData.ShouldTrigger.Count > 0)
        {
            examplePrompts = triggerData.ShouldTrigger.Take(MaxExamplePrompts)
                .Select(t => triggerProcessor != null ? triggerProcessor(t) : t)
                .ToList();
        }
        else
        {
            // Fallback: generate natural-language prompts from UseFor items
            var fallbackSources = skillData.UseFor.Count > 0
                ? skillData.UseFor
                : skillData.Activation?.DetectionMarkers?.Count > 0
                    ? skillData.Activation.DetectionMarkers
                    : new List<string>();

            examplePrompts = GenerateFallbackPrompts(fallbackSources, skillData.DisplayName);
        }

        // Build "What it provides" with concrete capabilities from services/tools
        var whatItProvides = BuildWhatItProvides(skillData);

        return new Dictionary<string, object?>
        {
            ["name"] = skillData.Name,
            ["displayName"] = skillData.DisplayName,
            ["description"] = skillData.Description,
            ["tier"] = tierAssessment.Tier,
            ["generatedDate"] = DateTime.UtcNow.ToString("M/d/yyyy"),
            ["generatorVersion"] = "1.0.0",
            ["useFor"] = useForList,
            ["doNotUseFor"] = doNotUseForList,
            ["services"] = skillData.Services.Select(s => new Dictionary<string, object?>
            {
                ["name"] = s.Name,
                ["useWhen"] = s.UseWhen,
                ["mcpTools"] = s.McpTools,
                ["cli"] = s.Cli
            }).ToList(),
            ["mcpTools"] = skillData.McpTools.Select(t => new Dictionary<string, object?>
            {
                ["toolName"] = t.ToolName,
                ["command"] = t.Command,
                ["purpose"] = t.Purpose,
                ["toolPage"] = t.ToolPage
            }).ToList(),
            ["workflowSteps"] = skillData.WorkflowSteps,
            ["decisionGuidance"] = skillData.DecisionGuidance.Select(d => new Dictionary<string, object?>
            {
                ["topic"] = d.Topic,
                ["options"] = d.Options.Select(o => new Dictionary<string, object?>
                {
                    ["option"] = o.Option,
                    ["bestFor"] = o.BestFor,
                    ["tradeoff"] = o.Tradeoff
                }).ToList()
            }).ToList(),
            ["relatedSkills"] = skillData.RelatedSkills,
            ["sdkReferences"] = skillData.SdkReferences,
            ["shouldTrigger"] = examplePrompts,
            ["shouldNotTrigger"] = triggerData.ShouldNotTrigger,
            ["showToolsSection"] = tierAssessment.ShowToolsSection,
            ["showTriggersSection"] = tierAssessment.ShowTriggersSection,
            ["showDecisionGuidance"] = tierAssessment.ShowDecisionGuidance,
            ["showWorkflow"] = tierAssessment.ShowWorkflow,
            ["showDetailedPrompts"] = tierAssessment.ShowDetailedPrompts,
            ["prerequisites"] = new Dictionary<string, object?>
            {
                ["azure"] = new Dictionary<string, object?>
                {
                    ["requiresAzureLogin"] = prerequisites.Azure.RequiresAzureLogin,
                    ["requiresSubscription"] = prerequisites.Azure.RequiresSubscription
                },
                ["rbacRoles"] = prerequisites.RbacRoles.Select(r => new Dictionary<string, object?>
                {
                    ["roleName"] = r.RoleName,
                    ["scope"] = r.Scope,
                    ["reason"] = r.Reason
                }).ToList(),
                ["tools"] = prerequisites.Tools.Select(t => new Dictionary<string, object?>
                {
                    ["name"] = t.Name,
                    ["minVersion"] = t.MinVersion,
                    ["installCommand"] = t.InstallCommand,
                    ["required"] = t.Required
                }).ToList(),
                ["resources"] = prerequisites.Resources.Select(r => new Dictionary<string, object?>
                {
                    ["resourceType"] = r.ResourceType,
                    ["description"] = r.Description,
                    ["quickCreateCommand"] = r.QuickCreateCommand
                }).ToList(),
                ["environmentRequirements"] = prerequisites.EnvironmentRequirements
            },
            ["hasServices"] = skillData.Services.Count > 0,
            ["hasMcpTools"] = skillData.McpTools.Count > 0,
            ["hasWorkflow"] = skillData.WorkflowSteps.Count > 0,
            ["hasDecisionGuidance"] = skillData.DecisionGuidance.Count > 0,
            ["hasRelatedSkills"] = skillData.RelatedSkills.Count > 0,
            ["hasSdkReferences"] = skillData.SdkReferences.Count > 0,
            ["hasUseFor"] = useForList.Count > 0,
            ["hasDoNotUseFor"] = doNotUseForList.Count > 0,
            ["hasTriggers"] = examplePrompts.Count > 0,
            ["whatItProvides"] = whatItProvides,
            ["hasRbacRoles"] = prerequisites.RbacRoles.Count > 0,
            ["hasToolPrereqs"] = prerequisites.Tools.Count > 0,
            ["hasResources"] = prerequisites.Resources.Count > 0,
            ["hasEnvironmentReqs"] = prerequisites.EnvironmentRequirements.Count > 0,
            // Sub-skills
            ["subSkills"] = skillData.SubSkills.Select(s => new Dictionary<string, object?>
            {
                ["name"] = s.Name,
                ["displayName"] = s.DisplayName,
                ["description"] = s.Description,
                ["useFor"] = s.UseFor,
                ["hasUseFor"] = s.UseFor.Count > 0,
                ["services"] = s.Services.Select(svc => new Dictionary<string, object?>
                {
                    ["name"] = svc.Name,
                    ["useWhen"] = svc.UseWhen
                }).ToList(),
                ["hasServices"] = s.Services.Count > 0,
                ["mcpTools"] = s.McpTools.Select(t => new Dictionary<string, object?>
                {
                    ["toolName"] = t.ToolName,
                    ["command"] = t.Command,
                    ["purpose"] = t.Purpose
                }).ToList(),
                ["hasMcpTools"] = s.McpTools.Count > 0
            }).ToList(),
            ["hasSubSkills"] = skillData.SubSkills.Count > 0,
            // Activation triggers
            ["activation"] = skillData.Activation != null ? new Dictionary<string, object?>
            {
                ["directive"] = skillData.Activation.Directive,
                ["preferOver"] = skillData.Activation.PreferOver,
                ["detectionMarkers"] = skillData.Activation.DetectionMarkers,
                ["hasDetectionMarkers"] = skillData.Activation.DetectionMarkers?.Count > 0
            } : null,
            ["hasActivation"] = skillData.Activation != null
        };
    }

    /// <summary>
    /// Builds a concrete "What it provides" section from services and tools data
    /// instead of just echoing the skill description.
    /// </summary>
    private static string BuildWhatItProvides(SkillData skillData)
    {
        var parts = new List<string>();

        // Opening sentence — always present
        parts.Add($"The {skillData.DisplayName} skill gives GitHub Copilot specialized knowledge about {skillData.DisplayName} in Azure.");

        // Add concrete capabilities from services
        if (skillData.Services.Count > 0)
        {
            var serviceNames = skillData.Services.Select(s => s.Name).ToList();
            var serviceList = serviceNames.Count switch
            {
                1 => serviceNames[0],
                2 => $"{serviceNames[0]} and {serviceNames[1]}",
                _ => $"{string.Join(", ", serviceNames.Take(serviceNames.Count - 1))}, and {serviceNames[^1]}"
            };
            parts.Add($"This includes capabilities for {serviceList}.");
        }

        // Add concrete capabilities from tools
        if (skillData.McpTools.Count > 0)
        {
            var purposes = skillData.McpTools
                .Where(t => !string.IsNullOrWhiteSpace(t.Purpose))
                .Select(t => t.Purpose.TrimEnd('.').ToLowerInvariant())
                .Distinct()
                .Take(5)
                .ToList();

            if (purposes.Count > 0)
            {
                var purposeList = purposes.Count switch
                {
                    1 => purposes[0],
                    2 => $"{purposes[0]} and {purposes[1]}",
                    _ => $"{string.Join(", ", purposes.Take(purposes.Count - 1))}, and {purposes[^1]}"
                };
                parts.Add($"Use this skill to {purposeList}.");
            }
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Generates natural-language example prompts from UseFor items when triggers.test.ts is missing.
    /// </summary>
    internal static List<string> GenerateFallbackPrompts(List<string> useForItems, string displayName)
    {
        if (useForItems.Count == 0) return new List<string>();

        return useForItems
            .Take(MaxExamplePrompts)
            .Select(item => ConvertToPrompt(item, displayName))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    /// <summary>
    /// Converts a single UseFor item into a natural-language prompt question.
    /// </summary>
    internal static string ConvertToPrompt(string item, string displayName)
    {
        var trimmed = item.Trim().TrimEnd('.');

        // If it already looks like a question, keep it
        if (trimmed.EndsWith('?')) return trimmed;

        // If it starts with a verb phrase (e.g., "deploy copilot app"), frame as "How do I..."
        var verbStarters = new[] { "deploy", "create", "configure", "set up", "manage", "build",
            "monitor", "diagnose", "troubleshoot", "migrate", "optimize", "analyze", "query",
            "list", "get", "add", "remove", "update", "delete", "run", "test", "check", "find",
            "enable", "disable", "connect", "plan", "design", "review", "install", "generate" };

        var lower = trimmed.ToLowerInvariant();
        foreach (var verb in verbStarters)
        {
            if (lower.StartsWith(verb + " ") || lower.StartsWith(verb + ","))
                return $"How do I {char.ToLower(trimmed[0])}{trimmed[1..]}?";
        }

        // Noun phrase — frame as "How do I work with..."
        return $"How do I work with {char.ToLower(trimmed[0])}{trimmed[1..]}?";
    }
    /// </summary>
    internal static List<string> NaturalizeItems(List<string> items, string skillDisplayName)
    {
        if (items.Count == 0) return items;

        var result = new List<string>();
        var shortItems = new List<string>();

        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Skip items that are too short to be meaningful
            if (trimmed.Length < 4) continue;

            // Skip question-like items (leaked trigger prompts, not use cases)
            if (trimmed.StartsWith("How ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("What ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Or ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Show ", StringComparison.OrdinalIgnoreCase))
                continue;

            var wordCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            // Skip single-word items that are verbs/keywords without context
            if (wordCount == 1 && (StartsWithVerb(trimmed) || LooksLikeVerbForm(trimmed)))
                continue;

            if (wordCount >= 5)
            {
                FlushShortItems(shortItems, result);
                result.Add(CapitalizeFirst(trimmed));
                continue;
            }

            if (StartsWithVerb(trimmed))
            {
                FlushShortItems(shortItems, result);
                // Short verb phrases (2-3 words) need context — append "in Azure" unless already has it
                if (wordCount <= 3 && !trimmed.Contains("Azure", StringComparison.OrdinalIgnoreCase))
                    result.Add(CapitalizeFirst(trimmed) + " in Azure");
                else
                    result.Add(CapitalizeFirst(trimmed));
                continue;
            }

            shortItems.Add(trimmed);
            if (shortItems.Count >= 4)
                FlushShortItems(shortItems, result);
        }

        FlushShortItems(shortItems, result);

        // Fix acronym casing in all results
        result = result.Select(FixAcronymCasing).ToList();

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Returns true if a single word looks like a verb form (-ing, -ed, -tion)
    /// rather than a noun, indicating it should not be prefixed with "Work with".
    /// </summary>
    private static bool LooksLikeVerbForm(string word)
    {
        return word.EndsWith("ing", StringComparison.OrdinalIgnoreCase) ||
               word.EndsWith("ed", StringComparison.OrdinalIgnoreCase) ||
               word.EndsWith("tion", StringComparison.OrdinalIgnoreCase);
    }

    private static void FlushShortItems(List<string> shortItems, List<string> result)
    {
        if (shortItems.Count == 0) return;

        var joined = shortItems.Count == 1
            ? shortItems[0]
            : shortItems.Count == 2
                ? $"{shortItems[0]} and {shortItems[1]}"
                : $"{string.Join(", ", shortItems.Take(shortItems.Count - 1))}, and {shortItems[^1]}";

        result.Add($"Manage and configure {joined} in Azure");
        shortItems.Clear();
    }

    private static bool StartsWithVerb(string text)
    {
        var verbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "add", "adding", "analyze", "analyzing", "assign", "assigning",
            "build", "building", "check", "checking", "choose", "choosing",
            "compare", "comparing", "configure", "configuring", "connect",
            "connecting", "create", "creating", "debug", "debugging",
            "deploy", "deploying", "design", "designing", "diagnose",
            "diagnosing", "enable", "enabling", "evaluate", "evaluating",
            "execute", "executing", "find", "finding", "fix", "fixing",
            "get", "getting", "help", "helping", "implement", "implementing",
            "inspect", "inspecting", "install", "installing", "integrate",
            "integrating", "list", "listing", "manage", "managing",
            "migrate", "migrating", "monitor", "monitoring", "move", "moving",
            "optimize", "optimizing", "plan", "planning", "prepare", "preparing",
            "provision", "provisioning", "query", "querying", "recommend",
            "recommending", "request", "requesting", "resolve", "resolving",
            "review", "reviewing", "run", "running", "scale", "scaling",
            "search", "searching", "secure", "securing", "select", "selecting",
            "set", "setting", "setup", "test", "testing", "trace", "tracing",
            "troubleshoot", "troubleshooting", "understand", "understanding",
            "update", "updating", "upgrade", "upgrading", "upload", "uploading",
            "use", "using", "validate", "validating", "verify", "verifying",
            "view", "viewing", "visualize", "visualizing", "work", "working"
        };
        var firstWord = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return verbs.Contains(firstWord.TrimEnd(',', '.', ':', ';'));
    }

    private static string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpper(text[0]) + text[1..];
    }

    private static readonly (System.Text.RegularExpressions.Regex pattern, string replacement)[] AcronymFixups =
    [
        (new System.Text.RegularExpressions.Regex(@"\b[Aa][Ii]\b"), "AI"),
        (new System.Text.RegularExpressions.Regex(@"\b[Aa][Ww][Ss]\b"), "AWS"),
        (new System.Text.RegularExpressions.Regex(@"\b[Gg][Cc][Pp]\b"), "GCP"),
        (new System.Text.RegularExpressions.Regex(@"\b[Kk][Qq][Ll]\b"), "KQL"),
        (new System.Text.RegularExpressions.Regex(@"\b[Mm][Ss][Aa][Ll]\b"), "MSAL"),
        (new System.Text.RegularExpressions.Regex(@"\b[Ll][Ll][Mm]\b"), "LLM"),
        (new System.Text.RegularExpressions.Regex(@"\b[Vv][Mm]\b"), "VM"),
        (new System.Text.RegularExpressions.Regex(@"\b[Aa][Pp][Ii]\b"), "API"),
        (new System.Text.RegularExpressions.Regex(@"\b[Ss][Dd][Kk]\b"), "SDK"),
        (new System.Text.RegularExpressions.Regex(@"\b[Aa][Kk][Ss]\b"), "AKS"),
        (new System.Text.RegularExpressions.Regex(@"\b[Aa][Dd][Xx]\b"), "ADX"),
        (new System.Text.RegularExpressions.Regex(@"\b[Ii][Dd]\b"), "ID"),
    ];

    /// <summary>
    /// Fixes mis-cased acronyms (e.g., "Ai" → "AI", "Sdk" → "SDK") using word-boundary regex.
    /// </summary>
    internal static string FixAcronymCasing(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var (pattern, replacement) in AcronymFixups)
            text = pattern.Replace(text, replacement);
        return text;
    }

    private static void RegisterHelpers(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("ifeq", (output, options, context, arguments) =>
        {
            if (arguments.Length >= 2 && arguments[0]?.ToString() == arguments[1]?.ToString())
                options.Template(output, context);
            else
                options.Inverse(output, context);
        });

        handlebars.RegisterHelper("ifgt", (output, options, context, arguments) =>
        {
            if (arguments.Length >= 2 &&
                int.TryParse(arguments[0]?.ToString(), out var a) &&
                int.TryParse(arguments[1]?.ToString(), out var b) &&
                a > b)
                options.Template(output, context);
            else
                options.Inverse(output, context);
        });
    }
}
