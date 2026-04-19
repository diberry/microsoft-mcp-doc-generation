namespace SkillsGen.Core.Models;

public record ServiceEntry(string Name, string UseWhen, string? McpTools = null, string? Cli = null);
public record McpToolEntry(string ToolName, string Command, string Purpose, string? ToolPage = null);
public record DecisionOption(string Option, string BestFor, string? Tradeoff = null);
public record DecisionEntry(string Topic, List<DecisionOption> Options);

/// <summary>
/// Activation trigger parsed from MANDATORY/PREFER OVER directives and codebase detection markers.
/// </summary>
public record ActivationTrigger(string Directive, string? PreferOver = null, List<string>? DetectionMarkers = null);

/// <summary>
/// A sub-skill discovered from a subdirectory under the parent skill.
/// </summary>
public record SubSkillData
{
    public required string Name { get; init; }
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public List<string> UseFor { get; init; } = [];
    public List<ServiceEntry> Services { get; init; } = [];
    public List<McpToolEntry> McpTools { get; init; } = [];
    public List<RbacRequirement> RbacRoles { get; init; } = [];
}

public record SkillData
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public List<string> UseFor { get; init; } = [];
    public List<string> DoNotUseFor { get; init; } = [];
    public List<ServiceEntry> Services { get; init; } = [];
    public List<McpToolEntry> McpTools { get; init; } = [];
    public List<string> WorkflowSteps { get; init; } = [];
    public List<DecisionEntry> DecisionGuidance { get; init; } = [];
    public List<string> RelatedSkills { get; init; } = [];
    public List<string> SdkReferences { get; init; } = [];
    public List<string> Prerequisites { get; init; } = [];
    public List<string> Compatibility { get; init; } = [];
    public string RawBody { get; init; } = "";
    public ActivationTrigger? Activation { get; init; }
    public List<SubSkillData> SubSkills { get; init; } = [];
}
