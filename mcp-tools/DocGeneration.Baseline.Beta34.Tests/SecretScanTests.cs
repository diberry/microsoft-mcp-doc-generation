using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// T17–T22: secret / PII / path-leak scanning across ALL fixtures, the manifest, AND the source
/// inventory, plus the sanitization determinism proxy (only approved placeholder tokens, no residual
/// absolute paths). Every scan first pins the frozen-input inventory so an empty/missing baseline
/// fails RED rather than passing vacuously.
/// </summary>
public sealed class SecretScanTests
{
    private sealed record ScanTarget(string Label, string Text);

    private static List<ScanTarget> ScanTargets()
    {
        string[] fixtures = BaselineContext.FixtureFiles();
        Assert.Equal(BaselineContext.ExpectedRecordCount, fixtures.Length);
        Assert.True(File.Exists(BaselineContext.ManifestPath),
            "Frozen manifest must exist to scan: " + BaselineContext.ManifestPath);
        Assert.True(File.Exists(BaselineContext.SourceInventoryPath),
            "Frozen source inventory must exist to scan: " + BaselineContext.SourceInventoryPath);

        var targets = new List<ScanTarget>();
        foreach (string f in fixtures)
        {
            targets.Add(new ScanTarget(Path.GetFileName(f), File.ReadAllText(f)));
        }
        targets.Add(new ScanTarget("beta34-baseline-manifest.json",
            File.ReadAllText(BaselineContext.ManifestPath)));
        targets.Add(new ScanTarget("source-inventory.json",
            File.ReadAllText(BaselineContext.SourceInventoryPath)));
        return targets;
    }

    private static void AssertNoRegex(Regex pattern, string reason)
    {
        foreach (ScanTarget t in ScanTargets())
        {
            Match m = pattern.Match(t.Text);
            Assert.False(m.Success,
                $"{reason} — '{t.Label}' contains disallowed match '{Trunc(m.Value)}' at index {m.Index}.");
        }
    }

    private static void AssertNoLiteral(string token, string reason)
    {
        foreach (ScanTarget t in ScanTargets())
        {
            int idx = t.Text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            Assert.True(idx < 0,
                $"{reason} — '{t.Label}' contains disallowed token '{token}' at index {idx}.");
        }
    }

    private static string Trunc(string s) => s.Length <= 60 ? s : s[..60] + "…";

    // T17
    [Fact]
    public void T17_Fixtures_Contain_No_UserHomePaths()
    {
        AssertNoRegex(new Regex(@"[A-Za-z]:\\+Users\\+", RegexOptions.IgnoreCase),
            "User home path leaked");
        AssertNoRegex(new Regex(@"/(?:home|Users)/[^/""\\ ]+", RegexOptions.IgnoreCase),
            "POSIX home path leaked");
    }

    // T18
    [Fact]
    public void T18_Fixtures_Contain_No_MachineOrUserNames()
    {
        AssertNoLiteral("diberry", "Capture username leaked");
    }

    // T19
    [Fact]
    public void T19_Fixtures_Contain_No_TempGuidPipelinePaths()
    {
        AssertNoRegex(new Regex(@"AppData[\\/]+Local[\\/]+Temp", RegexOptions.IgnoreCase),
            "Temp directory path leaked");
        AssertNoRegex(new Regex(@"pipeline-runner-step\d+-[0-9a-fA-F]{16,}", RegexOptions.None),
            "Pipeline-runner temp GUID directory leaked");
    }

    // T20
    [Fact]
    public void T20_Fixtures_Contain_No_Secret_Shaped_Values()
    {
        AssertNoLiteral("password", "Credential-shaped token 'password'");
        AssertNoLiteral("apikey", "Credential-shaped token 'apikey'");
        AssertNoLiteral("api-key", "Credential-shaped token 'api-key'");
        AssertNoLiteral("secret", "Credential-shaped token 'secret'");
        AssertNoLiteral("bearer ", "Bearer token prefix");
        AssertNoLiteral("AccountKey=", "Storage AccountKey secret");
        AssertNoLiteral("SharedAccessSignature", "SAS credential");

        // Connection-string shapes.
        AssertNoRegex(new Regex(@"DefaultEndpointsProtocol=", RegexOptions.IgnoreCase),
            "Storage connection string");
        AssertNoRegex(new Regex(@"Endpoint=sb://", RegexOptions.IgnoreCase),
            "Service Bus connection string");
        // JWT-shaped strings (header.payload.signature).
        AssertNoRegex(new Regex(@"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]+", RegexOptions.None),
            "JWT-shaped token");
        // OpenAI-style keys and SAS signatures.
        AssertNoRegex(new Regex(@"sk-[A-Za-z0-9]{20,}", RegexOptions.None), "OpenAI-style API key");
        AssertNoRegex(new Regex(@"[?&]sig=[A-Za-z0-9%]{10,}", RegexOptions.None), "SAS signature");
    }

    // T21
    [Fact]
    public void T21_Fixtures_Contain_No_AbsoluteRepoPaths()
    {
        AssertNoLiteral("my-squad-projects", "Absolute repo-root path leaked");
        AssertNoLiteral("microsoft-mcp-doc-generation\\", "Absolute repo path segment leaked");
    }

    // T22 — sanitization determinism / idempotence proxy.
    [Fact]
    public void T22_Sanitization_Only_Uses_Approved_Placeholders_And_No_Absolute_Paths()
    {
        // (a) No residual absolute Windows drive paths where a placeholder should be.
        AssertNoRegex(new Regex(@"[A-Za-z]:\\", RegexOptions.None),
            "Residual absolute Windows path (sanitizer non-idempotent or incomplete)");

        // (b) Every angle-bracket placeholder token must be one of the approved set (8 tokens).
        var placeholder = new Regex(@"<[A-Z][A-Z_]*>", RegexOptions.None);
        foreach (ScanTarget t in ScanTargets())
        {
            foreach (Match m in placeholder.Matches(t.Text))
            {
                Assert.True(BaselineContext.ApprovedPlaceholders.Contains(m.Value),
                    $"'{t.Label}' uses non-approved placeholder '{m.Value}'. " +
                    $"Approved ({BaselineContext.ApprovedPlaceholders.Count}): " +
                    $"{string.Join(", ", BaselineContext.ApprovedPlaceholders.OrderBy(x => x, StringComparer.Ordinal))}.");
            }
        }
    }

    // SourceInventory_Contains_No_Environment_Leakage (Ellis blocking-2 follow-on):
    // the capture inventory is a committed artifact — it must be as clean as the fixtures.
    [Fact]
    public void SourceInventory_Contains_No_Environment_Leakage()
    {
        Assert.True(File.Exists(BaselineContext.SourceInventoryPath),
            "Frozen source inventory must exist to scan: " + BaselineContext.SourceInventoryPath);
        string text = File.ReadAllText(BaselineContext.SourceInventoryPath);

        void NoRegex(Regex pattern, string reason)
        {
            Match m = pattern.Match(text);
            Assert.False(m.Success, $"{reason} — source-inventory.json contains '{Trunc(m.Value)}' at index {m.Index}.");
        }

        void NoLiteral(string token, string reason)
        {
            int idx = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            Assert.True(idx < 0, $"{reason} — source-inventory.json contains '{token}' at index {idx}.");
        }

        // Absolute paths (relativePath values must stay repo-relative, starting with generated-…).
        NoRegex(new Regex(@"[A-Za-z]:\\", RegexOptions.None), "Absolute Windows path leaked");
        NoRegex(new Regex(@"[A-Za-z]:\\+Users\\+", RegexOptions.IgnoreCase), "User home path leaked");
        NoRegex(new Regex(@"/(?:home|Users)/[^/""\\ ]+", RegexOptions.IgnoreCase), "POSIX home path leaked");
        NoRegex(new Regex(@"AppData[\\/]+Local[\\/]+Temp", RegexOptions.IgnoreCase), "Temp directory path leaked");
        NoRegex(new Regex(@"pipeline-runner-step\d+-[0-9a-fA-F]{16,}", RegexOptions.None), "Pipeline-runner temp GUID leaked");

        // User / machine names and repo-root path segments.
        NoLiteral("diberry", "Capture username leaked");
        NoLiteral("my-squad-projects", "Absolute repo-root path leaked");

        // Credential shapes.
        NoLiteral("password", "Credential-shaped token 'password'");
        NoLiteral("apikey", "Credential-shaped token 'apikey'");
        NoLiteral("secret", "Credential-shaped token 'secret'");
        NoLiteral("AccountKey=", "Storage AccountKey secret");
        NoRegex(new Regex(@"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]+", RegexOptions.None), "JWT-shaped token");
        NoRegex(new Regex(@"sk-[A-Za-z0-9]{20,}", RegexOptions.None), "OpenAI-style API key");
    }
}
