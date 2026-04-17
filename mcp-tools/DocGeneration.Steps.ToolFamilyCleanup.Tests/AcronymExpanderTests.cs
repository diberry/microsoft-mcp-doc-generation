// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for AcronymExpander — deterministic post-processor that expands
/// acronyms on first body occurrence per Microsoft style guide
/// (Acrolinx TM-3: "Did you define the acronym in your content?").
/// Generalizes the existing PostProcessor.ExpandMcpAcronym() pattern.
/// Fixes: #142 (generalized), #215
/// </summary>
public class AcronymExpanderTests
{
    // ── Core expansion — first body occurrence only ──────────────────

    [Fact]
    public void ExpandAll_VM_ExpandedOnFirstUse()
    {
        var markdown = @"---
title: Azure MCP Server tools for Compute
---

# Azure MCP Server tools for Compute

Use Azure MCP Server to manage VM resources. You can create a VM or list VMs.";

        var result = AcronymExpander.ExpandAll(markdown);

        Assert.Contains("virtual machine (VM) resources", result);
        // Second occurrence stays as "VM"
        Assert.Contains("create a VM", result);
    }

    [Fact]
    public void ExpandAll_VMSS_ExpandedOnFirstUse()
    {
        var markdown = @"---
title: Test
---

# Test

Deploy to VMSS instances. Manage your VMSS clusters.";

        var result = AcronymExpander.ExpandAll(markdown);

        Assert.Contains("virtual machine scale set (VMSS) instances", result);
        // Second stays as-is
        Assert.Contains("Manage your VMSS", result);
    }

    [Fact]
    public void ExpandAll_RBAC_ExpandedOnFirstUse()
    {
        var markdown = @"---
title: Test
---

# Test

Configure RBAC for your resources. RBAC controls access.";

        var result = AcronymExpander.ExpandAll(markdown);

        Assert.Contains("role-based access control (RBAC) for your resources", result);
        Assert.Contains("RBAC controls access", result);
    }

    [Fact]
    public void ExpandAll_MCP_ExpandedOnFirstUse()
    {
        var markdown = @"---
title: Azure MCP Server tools
---

# Azure MCP Server tools

The Azure MCP Server lets you manage resources. Use Azure MCP Server daily.";

        var result = AcronymExpander.ExpandAll(markdown);

        Assert.Contains("Model Context Protocol (MCP)", result);
    }

    // ── Headings should NOT be expanded ─────────────────────────────

    [Fact]
    public void ExpandAll_AcronymInHeading_NotExpanded()
    {
        var markdown = @"---
title: Test
---

# VM Management

## List VMs

Use VM tools to manage resources.";

        var result = AcronymExpander.ExpandAll(markdown);

        // H1 and H2 should not be modified
        Assert.Contains("# VM Management", result);
        Assert.Contains("## List VMs", result);
        // Body text should have expansion
        Assert.Contains("virtual machine (VM) tools", result);
    }

    // ── Backticks should NOT be expanded ────────────────────────────

    [Fact]
    public void ExpandAll_AcronymInsideBackticks_NotExpanded()
    {
        var markdown = @"---
title: Test
---

# Test

Use the `VM` parameter. Create a VM in the portal.";

        var result = AcronymExpander.ExpandAll(markdown);

        // Backtick content preserved
        Assert.Contains("`VM`", result);
        // Body text gets expansion on first non-backtick occurrence
        Assert.Contains("virtual machine (VM) in the portal", result);
    }

    // ── Already expanded — idempotent ───────────────────────────────

    [Fact]
    public void ExpandAll_AlreadyExpanded_NoDoubleExpansion()
    {
        var markdown = @"---
title: Test
---

# Test

Use virtual machine (VM) resources. Create a VM instance.";

        var result = AcronymExpander.ExpandAll(markdown);

        // Should not create "virtual machine (virtual machine (VM))"
        var count = CountOccurrences(result, "virtual machine (VM)");
        Assert.Equal(1, count);
    }

    // ── Frontmatter should NOT be expanded ──────────────────────────

    [Fact]
    public void ExpandAll_AcronymInFrontmatter_NotExpanded()
    {
        var markdown = @"---
title: Azure MCP Server tools for VM management
description: Use Azure MCP Server to manage VM resources.
---

# Azure MCP Server tools for VM management

Use Azure MCP Server to manage VM resources.";

        var result = AcronymExpander.ExpandAll(markdown);

        // Frontmatter untouched
        Assert.Contains("title: Azure MCP Server tools for VM management", result);
        Assert.Contains("description: Use Azure MCP Server to manage VM resources.", result);
    }

    // ── Multiple different acronyms ─────────────────────────────────

    [Fact]
    public void ExpandAll_MultipleDifferentAcronyms_AllExpanded()
    {
        var markdown = @"---
title: Test
---

# Test

Configure RBAC and deploy to AKS. Manage VM resources in AKS clusters.";

        var result = AcronymExpander.ExpandAll(markdown);

        Assert.Contains("role-based access control (RBAC)", result);
        Assert.Contains("Azure Kubernetes Service (AKS)", result);
        Assert.Contains("virtual machine (VM)", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ExpandAll_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", AcronymExpander.ExpandAll(""));
        Assert.Equal("", AcronymExpander.ExpandAll(null!));
    }

    [Fact]
    public void ExpandAll_NoAcronyms_ReturnsUnchanged()
    {
        var markdown = @"---
title: Test
---

# Test

This is a normal sentence about Azure resources.";

        var result = AcronymExpander.ExpandAll(markdown);
        Assert.Equal(markdown, result);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
