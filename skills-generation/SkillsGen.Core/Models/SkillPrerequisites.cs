namespace SkillsGen.Core.Models;

public record AzureRequirements(bool RequiresAzureLogin = true, bool RequiresSubscription = true);
public record RbacRequirement(string RoleName, string Scope, string? Reason = null);
public record ToolRequirement(string Name, string? MinVersion = null, string? InstallCommand = null, bool Required = true);
public record ResourceRequirement(string ResourceType, string Description, string? QuickCreateCommand = null);

public record SkillPrerequisites
{
    public AzureRequirements Azure { get; init; } = new();
    public List<RbacRequirement> RbacRoles { get; init; } = [];
    public List<ToolRequirement> Tools { get; init; } = [];
    public List<ResourceRequirement> Resources { get; init; } = [];
}
