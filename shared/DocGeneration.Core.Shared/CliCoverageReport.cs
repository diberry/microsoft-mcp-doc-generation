// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Tracks CLI content coverage across a namespace.
/// </summary>
public class CliCoverageReport
{
    public string Namespace { get; init; } = "";
    public int TotalTools { get; init; }
    public int ToolsWithCliData { get; init; }
    public int ToolsWithCliTabs { get; init; }
    public int ToolsWithAiImprovedProse { get; init; }
    public int AiValidationFailures { get; init; }

    public double CliDataCoverage => TotalTools > 0 ? (double)ToolsWithCliData / TotalTools * 100 : 0;
    public double CliTabCoverage => TotalTools > 0 ? (double)ToolsWithCliTabs / TotalTools * 100 : 0;
    public double AiImprovementRate => ToolsWithCliData > 0
        ? (double)ToolsWithAiImprovedProse / ToolsWithCliData * 100 : 0;
    public double AiViolationRate => ToolsWithCliData > 0
        ? (double)AiValidationFailures / ToolsWithCliData * 100 : 0;

    public override string ToString()
        => $"[{Namespace}] CLI: {ToolsWithCliData}/{TotalTools} ({CliDataCoverage:F0}%), " +
           $"Tabs: {ToolsWithCliTabs}/{TotalTools} ({CliTabCoverage:F0}%), " +
           $"AI: {ToolsWithAiImprovedProse} improved, {AiValidationFailures} failures ({AiViolationRate:F1}%)";
}
