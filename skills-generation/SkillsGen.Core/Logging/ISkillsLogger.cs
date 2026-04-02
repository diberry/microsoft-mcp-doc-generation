using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Logging;

public interface ISkillsLogger
{
    void LogParseResult(string skillName, int serviceCount, int toolCount, int triggerCount);
    void LogTierAssessment(string skillName, int tier, string rationale);
    void LogLlmCall(string skillName, string operation, long durationMs);
    void LogTemplateRender(string skillName, int wordCount);
    void LogValidation(string skillName, bool isValid, int errorCount, int warningCount);
    void LogBatchSummary(int total, int succeeded, int failed, long totalDurationMs);
    void LogError(string skillName, string message, Exception? ex = null);
    void LogInfo(string message);
}
