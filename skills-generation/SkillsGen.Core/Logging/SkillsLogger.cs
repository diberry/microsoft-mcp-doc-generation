using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.Logging;

public class SkillsLogger : ISkillsLogger
{
    private readonly ILogger<SkillsLogger> _consoleLogger;
    private readonly string _logDirectory;

    public SkillsLogger(ILogger<SkillsLogger> consoleLogger, string logDirectory = "./logs")
    {
        _consoleLogger = consoleLogger;
        _logDirectory = logDirectory;
    }

    public void LogParseResult(string skillName, int serviceCount, int toolCount, int triggerCount)
    {
        var msg = $"[PARSE] {skillName}: {serviceCount} services, {toolCount} tools, {triggerCount} triggers";
        _consoleLogger.LogInformation("{Message}", msg);
        WriteToSkillLog(skillName, msg);
    }

    public void LogTierAssessment(string skillName, int tier, string rationale)
    {
        var emoji = tier == 1 ? "🟢" : "🟡";
        var msg = $"[TIER] {skillName}: {emoji} Tier {tier} — {rationale}";
        _consoleLogger.LogInformation("{Message}", msg);
        WriteToSkillLog(skillName, msg);
    }

    public void LogLlmCall(string skillName, string operation, long durationMs)
    {
        var msg = $"[LLM] {skillName}: {operation} ({durationMs}ms)";
        _consoleLogger.LogInformation("{Message}", msg);
        WriteToSkillLog(skillName, msg);
    }

    public void LogTemplateRender(string skillName, int wordCount)
    {
        var msg = $"[RENDER] {skillName}: {wordCount} words";
        _consoleLogger.LogInformation("{Message}", msg);
        WriteToSkillLog(skillName, msg);
    }

    public void LogValidation(string skillName, bool isValid, int errorCount, int warningCount)
    {
        var status = isValid ? "✅ passed" : "❌ failed";
        var msg = $"[VALIDATE] {skillName}: {status} ({errorCount} errors, {warningCount} warnings)";
        _consoleLogger.LogInformation("{Message}", msg);
        WriteToSkillLog(skillName, msg);
    }

    public void LogBatchSummary(int total, int succeeded, int failed, long totalDurationMs)
    {
        var msg = $"\n[SUMMARY] {succeeded}/{total} skills generated ({failed} failed) in {totalDurationMs}ms";
        _consoleLogger.LogInformation("{Message}", msg);
    }

    public void LogError(string skillName, string message, Exception? ex = null)
    {
        var msg = $"[ERROR] {skillName}: {message}";
        if (ex != null)
            _consoleLogger.LogError(ex, "{Message}", msg);
        else
            _consoleLogger.LogError("{Message}", msg);
        WriteToSkillLog(skillName, msg + (ex != null ? $"\n{ex}" : ""));
    }

    public void LogInfo(string message)
    {
        _consoleLogger.LogInformation("{Message}", message);
    }

    private void WriteToSkillLog(string skillName, string message)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var logPath = Path.Combine(_logDirectory, $"{skillName}.log");
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch
        {
            // Don't fail generation because of logging issues
        }
    }
}
