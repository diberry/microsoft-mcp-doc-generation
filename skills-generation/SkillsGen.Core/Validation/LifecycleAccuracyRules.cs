using System.Text.RegularExpressions;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Validation;

/// <summary>
/// Lifecycle phase of an azd-style skill, derived from the skill's own source text.
/// Used to constrain the verbs allowed in generated capability prose (Issue #734).
/// </summary>
public enum SkillLifecyclePhase
{
    /// <summary>Phase could not be determined from source — no verb constraints apply.</summary>
    Unknown,

    /// <summary>Authoring/preparation phase — generates IaC + a deployment plan. Provisioning happens later.</summary>
    Author,

    /// <summary>Validation phase — validates the plan/infrastructure before deploying.</summary>
    Validate,

    /// <summary>Deployment phase — provisions/creates/executes. Provisioning verbs are correct here.</summary>
    Deploy
}

/// <summary>
/// Universal, service-agnostic doc-accuracy rules for Azure Skills generation.
///
/// Covers three classes of generated-doc defects, all detected with pattern-based logic
/// (no hardcoded Azure service names) so they apply to every skill:
///   - #734 Phase-aware verbs: authoring/validate-phase skills must not claim to provision/create resources.
///   - #735 User-facing phrasing: internal build/protocol/ML-pipeline jargon must not leak into prose.
///   - #737 Title fidelity: the article title must match the canonical "Azure skill for {DisplayName}".
/// </summary>
public static class LifecycleAccuracyRules
{
    // --- #734: phase detection (source-derived, not a hardcoded skill-name list) ---

    private static readonly Regex[] AuthoringSignals =
    [
        new(@"\bpreparation only\b", RegexOptions.IgnoreCase),
        new(@"generat\w*\b[^.\n]{0,40}\binfrastructure\b", RegexOptions.IgnoreCase),
        new(@"generat\w*\b[^.\n]{0,30}\b(bicep|terraform)\b", RegexOptions.IgnoreCase),
        new(@"deployment[- ]plan", RegexOptions.IgnoreCase),
        new(@"provisioning happens later", RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] ValidateSignals =
    [
        new(@"validat\w*\b[^.\n]{0,30}\b(deployment|infrastructure|plan)\b", RegexOptions.IgnoreCase),
        new(@"\bpreflight\b", RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] DeployExecutionSignals =
    [
        new(@"execut\w*\b[^.\n]{0,20}\bdeployment\b", RegexOptions.IgnoreCase),
        new(@"\brun[s]?\b[^.\n]{0,10}\bazd up\b", RegexOptions.IgnoreCase),
    ];

    /// <summary>
    /// Detects the lifecycle phase from the skill's own description + body. Authoring wins over
    /// validate/deploy because a prepare skill's source both mentions authoring AND references the
    /// later deploy step — the authoring signal is the discriminating one.
    /// </summary>
    public static SkillLifecyclePhase DetectPhase(SkillData skill)
    {
        if (skill is null) return SkillLifecyclePhase.Unknown;
        var text = (skill.Description ?? "") + "\n" + (skill.RawBody ?? "");
        if (string.IsNullOrWhiteSpace(text)) return SkillLifecyclePhase.Unknown;

        if (AuthoringSignals.Any(r => r.IsMatch(text))) return SkillLifecyclePhase.Author;
        if (DeployExecutionSignals.Any(r => r.IsMatch(text))) return SkillLifecyclePhase.Deploy;
        if (ValidateSignals.Any(r => r.IsMatch(text))) return SkillLifecyclePhase.Validate;
        return SkillLifecyclePhase.Unknown;
    }

    // --- #734: forbidden provisioning/creation verbs for Author/Validate phases ---

    // "provision" (any inflection) — flagged unless it's the sanctioned deferral phrasing
    // ("provisioning happens later", "provision ... azure-deploy", "when you run ... deploy").
    private static readonly Regex ProvisionVerb = new(@"\bprovision\w*\b", RegexOptions.IgnoreCase);

    // Active creation of live resources (not "define ... in the generated templates").
    private static readonly Regex[] CreationVerbs =
    [
        new(@"\b(create|creates|creating|creation of)\b[^.\n]{0,30}\bresource group", RegexOptions.IgnoreCase),
        new(@"\bmanaged identity setup\b", RegexOptions.IgnoreCase),
        new(@"\bset[s]?\s+up\b[^.\n]{0,30}\b(managed identit|service|resource|infrastructure)", RegexOptions.IgnoreCase),
    ];

    /// <summary>
    /// Returns provisioning/creation action phrases that must not appear in the prose of an
    /// authoring- or validate-phase skill. Returns empty for Deploy/Unknown phases (where such
    /// verbs are correct or unconstrained). The sanctioned "provisioning happens later" deferral
    /// is intentionally not flagged.
    /// </summary>
    public static IReadOnlyList<string> FindForbiddenPhaseVerbs(string? prose, SkillLifecyclePhase phase)
    {
        var hits = new List<string>();
        if (string.IsNullOrWhiteSpace(prose)) return hits;
        if (phase != SkillLifecyclePhase.Author && phase != SkillLifecyclePhase.Validate) return hits;

        foreach (Match m in ProvisionVerb.Matches(prose))
        {
            var start = Math.Max(0, m.Index - 40);
            var end = Math.Min(prose.Length, m.Index + m.Length + 40);
            var window = prose[start..end];
            // Correct deferral to the deploy phase — not a defect.
            if (window.Contains("later", StringComparison.OrdinalIgnoreCase) ||
                window.Contains("azure-deploy", StringComparison.OrdinalIgnoreCase) ||
                window.Contains("deploy skill", StringComparison.OrdinalIgnoreCase) ||
                window.Contains("when you run", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            hits.Add(m.Value);
        }

        foreach (var rx in CreationVerbs)
        {
            var m = rx.Match(prose);
            if (m.Success) hits.Add(m.Value.Trim());
        }

        return hits;
    }

    // --- #735: internal / protocol / ML-pipeline jargon that must not appear in customer prose ---

    private static readonly Regex[] JargonPhrases =
    [
        new(@"\bDocker build\b", RegexOptions.IgnoreCase),
        new(@"\bACR push\b", RegexOptions.IgnoreCase),
        new(@"\bcontainer start\b", RegexOptions.IgnoreCase),
        new(@"\bagent\.yaml\b", RegexOptions.IgnoreCase),
        new(@"\binvocations_ws\b", RegexOptions.IgnoreCase),
        new(@"\bduplex WebSocket\b", RegexOptions.IgnoreCase),
        new(@"\bout-of-band\b", RegexOptions.IgnoreCase),
        new(@"\bgrader calibration\b", RegexOptions.IgnoreCase),
        new(@"\bcheckpoint selection\b", RegexOptions.IgnoreCase),
        new(@"\bdataset versioning\b", RegexOptions.IgnoreCase),
        new(@"\bskill-version\b", RegexOptions.IgnoreCase),
        // ML-training acronyms — case-sensitive to avoid matching unrelated lowercase text.
        new(@"\b(SFT|DPO|RFT)\b"),
        // Version strings do not belong in body prose (frontmatter ms.custom only).
        new(@"\bv\d+\.\d+(\.\d+)?\b"),
    ];

    /// <summary>
    /// Returns internal-jargon tokens found in the article body. Caller must pass the body with
    /// YAML frontmatter already stripped (version strings and skill-version are legal in frontmatter).
    /// </summary>
    public static IReadOnlyList<string> FindInternalJargon(string? articleBody)
    {
        var hits = new List<string>();
        if (string.IsNullOrWhiteSpace(articleBody)) return hits;

        foreach (var rx in JargonPhrases)
        {
            var m = rx.Match(articleBody);
            if (m.Success) hits.Add(m.Value);
        }
        return hits;
    }

    // --- #737: title fidelity ---

    /// <summary>
    /// Builds the canonical article title for a skill's display name.
    /// </summary>
    public static string CanonicalTitle(string displayName) => $"Azure skill for {displayName}";

    /// <summary>
    /// True when the frontmatter title exactly matches the canonical "Azure skill for {DisplayName}"
    /// form (paraphrasing, abbreviating, or reformatting the skill name fails this check).
    /// </summary>
    public static bool IsTitleCanonical(string? title, string displayName, out string expected)
    {
        expected = CanonicalTitle(displayName);
        if (string.IsNullOrWhiteSpace(title)) return false;
        return string.Equals(title.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
