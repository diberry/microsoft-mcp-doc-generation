using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Validation;

/// <summary>
/// Result of a quality gate check on a generated skill page section.
/// </summary>
public record QualityGateResult(bool IsValid, int ItemCount, string? Warning);

/// <summary>
/// Universal quality gate checks applied during skill page generation.
/// All checks are service-agnostic and pattern-based.
/// </summary>
public static class QualityGateValidator
{
    private const int MinWhenToUseItems = 3;

    /// <summary>
    /// Validates that the "When to use" section contains enough distinct items to be useful.
    /// Flags sections with fewer than 3 distinct items as thin.
    /// </summary>
    /// <param name="useForItems">The list of "When to use" items built for this skill.</param>
    /// <param name="skillName">Skill name for log context.</param>
    /// <param name="logger">Optional logger; warning is emitted when the gate fails.</param>
    /// <returns>A <see cref="QualityGateResult"/> describing the outcome.</returns>
    public static QualityGateResult ValidateWhenToUse(
        IReadOnlyList<string> useForItems,
        string skillName,
        ILogger? logger = null)
    {
        var distinctCount = useForItems
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinctCount >= MinWhenToUseItems)
            return new QualityGateResult(true, distinctCount, null);

        var warning = $"QUALITY-GATE [{skillName}]: 'When to use' has only {distinctCount} distinct item(s) " +
                      $"(minimum {MinWhenToUseItems}). Consider enriching the UseFor section in SKILL.md.";

        logger?.LogWarning("{Warning}", warning);

        return new QualityGateResult(false, distinctCount, warning);
    }
}
