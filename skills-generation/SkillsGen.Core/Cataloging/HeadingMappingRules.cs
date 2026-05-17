namespace SkillsGen.Core.Cataloging;

public static class HeadingMappingRules
{
    public static readonly IReadOnlyDictionary<string, string?> Rules =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            // Customer-facing mappings
            ["Use cases"]          = "When to use this skill",
            ["Negative use cases"] = "When not to use this skill",
            ["Azure services"]     = "Azure services knowledge",
            ["Prerequisites"]      = "Prerequisites",
            ["Required Inputs"]    = "Prerequisites",
            ["Rules"]              = "Prerequisites",
            ["RBAC"]               = "Prerequisites (RBAC sub-section)",
            ["Required Roles"]     = "Prerequisites (RBAC sub-section)",
            ["Role Based Access"]  = "Prerequisites (RBAC sub-section)",
            ["Related Skills"]     = "Related skills",
            ["Decision Guidance"]  = "Decision guidance",
            ["Decision"]           = "Decision guidance",
            ["Guidance"]           = "Decision guidance",

            // Excluded (implementation detail) — mapped to null
            ["MCP tools"]          = null,
            ["Workflow steps"]     = null,
            ["Steps"]              = null,
            ["Workflows"]          = null,
            ["Workflow"]           = null,
        };

    public static string? GetMapping(string headingText)
        => Rules.TryGetValue(headingText.Trim(), out var dest) ? dest : null;

    public static bool IsKnown(string headingText)
        => Rules.ContainsKey(headingText.Trim());
}
