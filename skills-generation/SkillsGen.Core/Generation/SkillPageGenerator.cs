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

    public string Generate(SkillData skillData, TriggerData triggerData, TierAssessment tierAssessment, SkillPrerequisites prerequisites)
    {
        var context = BuildContext(skillData, triggerData, tierAssessment, prerequisites);
        var result = _compiledTemplate(context);
        return result;
    }

    private static object BuildContext(SkillData skillData, TriggerData triggerData, TierAssessment tierAssessment, SkillPrerequisites prerequisites)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = skillData.Name,
            ["displayName"] = skillData.DisplayName,
            ["description"] = skillData.Description,
            ["tier"] = tierAssessment.Tier,
            ["generatedDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["generatorVersion"] = "1.0.0",
            ["useFor"] = skillData.UseFor,
            ["doNotUseFor"] = skillData.DoNotUseFor,
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
            ["shouldTrigger"] = triggerData.ShouldTrigger,
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
                }).ToList()
            },
            ["hasServices"] = skillData.Services.Count > 0,
            ["hasMcpTools"] = skillData.McpTools.Count > 0,
            ["hasWorkflow"] = skillData.WorkflowSteps.Count > 0,
            ["hasDecisionGuidance"] = skillData.DecisionGuidance.Count > 0,
            ["hasRelatedSkills"] = skillData.RelatedSkills.Count > 0,
            ["hasSdkReferences"] = skillData.SdkReferences.Count > 0,
            ["hasUseFor"] = skillData.UseFor.Count > 0,
            ["hasDoNotUseFor"] = skillData.DoNotUseFor.Count > 0,
            ["hasTriggers"] = triggerData.ShouldTrigger.Count > 0,
            ["hasRbacRoles"] = prerequisites.RbacRoles.Count > 0,
            ["hasToolPrereqs"] = prerequisites.Tools.Count > 0,
            ["hasResources"] = prerequisites.Resources.Count > 0
        };
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
