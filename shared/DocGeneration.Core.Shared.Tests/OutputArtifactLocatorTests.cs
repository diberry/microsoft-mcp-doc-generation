// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.TestInfrastructure;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

public sealed class OutputArtifactLocatorTests : IDisposable
{
    private readonly string? _originalOutputRoot =
        Environment.GetEnvironmentVariable(OutputArtifactLocator.OutputRootEnvironmentVariable);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(OutputArtifactLocator.OutputRootEnvironmentVariable, _originalOutputRoot);
    }

    [Fact]
    public void GetOutputRoot_UsesEnvironmentOverride()
    {
        var root = Path.Combine(Path.GetTempPath(), $"docgen-output-root-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(OutputArtifactLocator.OutputRootEnvironmentVariable, root);

        Assert.Equal(Path.GetFullPath(root), OutputArtifactLocator.GetOutputRoot());
    }

    [Fact]
    public void GetNamespaceDirectories_ReturnsOnlyGeneratedDirectoriesFromOverride()
    {
        var root = Path.Combine(Path.GetTempPath(), $"docgen-output-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "generated-advisor"));
        Directory.CreateDirectory(Path.Combine(root, "generated-storage"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        Environment.SetEnvironmentVariable(OutputArtifactLocator.OutputRootEnvironmentVariable, root);

        try
        {
            var directoryNames = OutputArtifactLocator.GetNamespaceDirectories()
                .Select(path => Path.GetFileName(path)!)
                .ToArray();

            Assert.Equal(new[] { "generated-advisor", "generated-storage" }, directoryNames);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
