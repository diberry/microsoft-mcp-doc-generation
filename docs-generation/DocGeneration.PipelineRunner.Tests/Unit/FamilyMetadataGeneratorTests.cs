// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class FamilyMetadataGeneratorTests
{
    [Fact]
    public void EnsureCorrectToolCount_FixesIncorrectLlmCount()
    {
        // Arrange
        var metadata = @"---
title: Azure Compute Operations
description: Manage Azure Virtual Machines and compute resources
ms.service: azure
ms.topic: reference
tool_count: 8
---

# Azure Compute Operations

Manage and monitor Azure Virtual Machines, scale sets, and compute resources.";

        var actualToolCount = 9;

        // Act
        var result = InvokeEnsureCorrectToolCount(metadata, actualToolCount);

        // Assert
        Assert.Contains("tool_count: 9", result);
        Assert.DoesNotContain("tool_count: 8", result);
    }

    [Fact]
    public void EnsureCorrectToolCount_HandlesVariousWhitespace()
    {
        // Arrange
        var metadata = @"---
title: Test
tool_count:    15
---";

        var actualToolCount = 20;

        // Act
        var result = InvokeEnsureCorrectToolCount(metadata, actualToolCount);

        // Assert
        Assert.Contains("tool_count: 20", result);
        Assert.DoesNotContain("tool_count:    15", result);
    }

    [Fact]
    public void EnsureCorrectToolCount_PreservesOtherFrontmatter()
    {
        // Arrange
        var metadata = @"---
title: Azure SQL Operations
description: Manage Azure SQL databases and servers
ms.service: azure-sql
ms.topic: reference
tool_count: 12
author: Test Author
---

# Azure SQL Operations

Content here.";

        var actualToolCount = 15;

        // Act
        var result = InvokeEnsureCorrectToolCount(metadata, actualToolCount);

        // Assert
        Assert.Contains("title: Azure SQL Operations", result);
        Assert.Contains("description: Manage Azure SQL databases and servers", result);
        Assert.Contains("ms.service: azure-sql", result);
        Assert.Contains("ms.topic: reference", result);
        Assert.Contains("tool_count: 15", result);
        Assert.Contains("author: Test Author", result);
        Assert.Contains("# Azure SQL Operations", result);
    }

    [Fact]
    public void EnsureCorrectToolCount_HandlesNoToolCountInMetadata()
    {
        // Arrange
        var metadata = @"---
title: Test
---

# Content";

        var actualToolCount = 10;

        // Act
        var result = InvokeEnsureCorrectToolCount(metadata, actualToolCount);

        // Assert - Should not crash, just return original
        Assert.Equal(metadata, result);
    }

    [Fact]
    public void EnsureCorrectToolCount_HandlesEmptyString()
    {
        // Arrange
        var metadata = "";
        var actualToolCount = 5;

        // Act
        var result = InvokeEnsureCorrectToolCount(metadata, actualToolCount);

        // Assert
        Assert.Equal("", result);
    }

    /// <summary>
    /// Uses reflection to invoke the private static EnsureCorrectToolCount method.
    /// This is necessary because the method is private in FamilyMetadataGenerator.
    /// </summary>
    private static string InvokeEnsureCorrectToolCount(string metadata, int actualToolCount)
    {
        var generatorType = Type.GetType("ToolFamilyCleanup.Services.FamilyMetadataGenerator, ToolFamilyCleanup");
        if (generatorType == null)
        {
            throw new InvalidOperationException("Could not load FamilyMetadataGenerator type");
        }

        var method = generatorType.GetMethod(
            "EnsureCorrectToolCount",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(int) },
            null);

        if (method == null)
        {
            throw new InvalidOperationException("Could not find EnsureCorrectToolCount method");
        }

        var result = method.Invoke(null, new object[] { metadata, actualToolCount });
        return result?.ToString() ?? string.Empty;
    }
}
