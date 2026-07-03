namespace DocGeneration.Tools.Fingerprint.Tests;

public sealed class GoldenSnapshotVerifierTests
{
    [Fact]
    public void Verify_DetectsDeterministicHashAndAiStructuralRegressions()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            WriteDeterministicAndAiFiles(outputDirectory);

            var capture = new GoldenSnapshotCapture(repoRoot);
            var manifest = capture.CaptureFromOutputDirectory(outputDirectory, "advisor");

            WriteFile(outputDirectory, Path.Combine("annotations", "advisor.md"), "changed deterministic output");
            WriteFile(outputDirectory, Path.Combine("tools", "advisor-tool.md"), """
                ---
                title: Advisor tool
                description: Generated tool article.
                ---

                # Advisor tool

                ## Usage

                Details.

                ## Examples

                More details.

                ## Extra

                Extra details.

                ## Another extra

                Too many sections.
                """);

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Violations, violation => violation.Contains("Deterministic hash mismatch: annotations/advisor.md", StringComparison.Ordinal));
            Assert.Contains(result.Violations, violation => violation.Contains("AI required keys missing: tools/advisor-tool.md (ms.topic)", StringComparison.Ordinal));
            Assert.Contains(result.Violations, violation => violation.Contains("AI section count mismatch: tools/advisor-tool.md", StringComparison.Ordinal));
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    [Fact]
    public void Verify_AllowsAiSectionDeltaOfOne()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            WriteDeterministicAndAiFiles(outputDirectory);

            var capture = new GoldenSnapshotCapture(repoRoot);
            var manifest = capture.CaptureFromOutputDirectory(outputDirectory, "advisor");

            WriteFile(outputDirectory, Path.Combine("tools", "advisor-tool.md"), """
                ---
                title: Advisor tool
                ms.topic: how-to
                description: Generated tool article.
                ---

                # Advisor tool

                ## Usage

                Details.

                ## Examples

                More details.

                ## Related content

                More related details.
                """);

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.True(result.Succeeded);
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    [Fact]
    public void Verify_SkipsAiChecksWhenNoAiOutputIsPresent()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            WriteFile(outputDirectory, Path.Combine("annotations", "advisor.md"), "deterministic annotations");
            var capture = new GoldenSnapshotCapture(repoRoot);
            var capturedManifest = capture.CaptureFromOutputDirectory(outputDirectory, "advisor");
            var manifest = new GoldenManifest
            {
                Namespace = capturedManifest.Namespace,
                CapturedAt = capturedManifest.CapturedAt,
                DeterministicFiles = capturedManifest.DeterministicFiles,
                AiFiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["tools/advisor-tool.md"] = new AiFileEntry
                    {
                        SectionCount = 2,
                        RequiredKeys = ["title", "ms.topic"]
                    }
                }
            };

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.True(result.Succeeded);
            Assert.Single(result.Notes);
            Assert.Contains("Skipping AI structural verification", result.Notes[0], StringComparison.Ordinal);
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    [Fact]
    public void Verify_DetectsMissingDeterministicFiles()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            var manifest = new GoldenManifest
            {
                Namespace = "advisor",
                DeterministicFiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["annotations/advisor.md"] = new DeterministicFileEntry { Sha256 = "abc123", SizeBytes = 10 },
                    ["parameters/params.json"] = new DeterministicFileEntry { Sha256 = "def456", SizeBytes = 20 }
                }
            };

            WriteFile(outputDirectory, Path.Combine("annotations", "advisor.md"), "deterministic annotations");

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Violations, v => v.Contains("Deterministic file missing: parameters/params.json", StringComparison.Ordinal));
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    [Fact]
    public void Verify_DetectsUnexpectedDeterministicFiles()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            WriteFile(outputDirectory, Path.Combine("annotations", "advisor.md"), "deterministic annotations");
            WriteFile(outputDirectory, Path.Combine("annotations", "extra.md"), "unexpected file");

            var capture = new GoldenSnapshotCapture(repoRoot);
            var fullManifest = capture.CaptureFromOutputDirectory(outputDirectory, "advisor");

            // Remove the extra file from manifest to simulate it being unexpected
            var manifest = new GoldenManifest
            {
                Namespace = "advisor",
                DeterministicFiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["annotations/advisor.md"] = fullManifest.DeterministicFiles["annotations/advisor.md"]
                }
            };

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Violations, v => v.Contains("Unexpected deterministic file: annotations/extra.md", StringComparison.Ordinal));
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    [Fact]
    public void Verify_AiSectionCountExactlyAtPlusOneBoundaryPasses()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            // Write AI file with 3 sections (## headings)
            WriteFile(outputDirectory, Path.Combine("tools", "tool.md"), """
                ---
                title: Tool
                ---

                # Tool

                ## One

                Content.

                ## Two

                Content.

                ## Three

                Content.
                """);

            // Create manifest expecting 2 sections (delta = +1, within tolerance)
            var manifest = new GoldenManifest
            {
                Namespace = "advisor",
                AiFiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["tools/tool.md"] = new AiFileEntry { SectionCount = 2, RequiredKeys = ["title"] }
                }
            };

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.True(result.Succeeded);
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    [Fact]
    public void Verify_AiSectionCountAtPlusTwoBoundaryFails()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            // Write AI file with 4 sections
            WriteFile(outputDirectory, Path.Combine("tools", "tool.md"), """
                ---
                title: Tool
                ---

                # Tool

                ## One

                Content.

                ## Two

                Content.

                ## Three

                Content.

                ## Four

                Content.
                """);

            // Create manifest expecting 2 sections (delta = +2, exceeds tolerance)
            var manifest = new GoldenManifest
            {
                Namespace = "advisor",
                AiFiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["tools/tool.md"] = new AiFileEntry { SectionCount = 2, RequiredKeys = ["title"] }
                }
            };

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Violations, v => v.Contains("AI section count mismatch", StringComparison.Ordinal));
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    [Fact]
    public void Verify_DetectsMissingAiFiles()
    {
        var repoRoot = CreateScratchDirectory();
        try
        {
            var outputDirectory = Path.Combine(repoRoot, "generated-advisor");
            Directory.CreateDirectory(outputDirectory);

            // Create tools directory but no files
            Directory.CreateDirectory(Path.Combine(outputDirectory, "tools"));
            WriteFile(outputDirectory, Path.Combine("tools", "existing.md"), """
                ---
                title: Existing
                ---

                # Existing

                ## Section

                Content.
                """);

            var manifest = new GoldenManifest
            {
                Namespace = "advisor",
                AiFiles = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["tools/existing.md"] = new AiFileEntry { SectionCount = 1, RequiredKeys = ["title"] },
                    ["tools/missing.md"] = new AiFileEntry { SectionCount = 2, RequiredKeys = ["title"] }
                }
            };

            var verifier = new GoldenSnapshotVerifier(repoRoot);
            var result = verifier.Verify(manifest, outputDirectory);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Violations, v => v.Contains("AI file missing: tools/missing.md", StringComparison.Ordinal));
        }
        finally
        {
            DeleteScratchDirectory(repoRoot);
        }
    }

    private static string CreateScratchDirectory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestScratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteScratchDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void WriteDeterministicAndAiFiles(string outputDirectory)
    {
        WriteFile(outputDirectory, Path.Combine("annotations", "advisor.md"), "deterministic annotations");
        WriteFile(outputDirectory, Path.Combine("tools", "advisor-tool.md"), """
            ---
            title: Advisor tool
            ms.topic: how-to
            description: Generated tool article.
            ---

            # Advisor tool

            ## Usage

            Details.

            ## Examples

            More details.
            """);
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }
}
