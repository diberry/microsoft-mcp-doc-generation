namespace ExamplePromptGeneratorStandalone.Utilities;

internal static class AiCallStatusReporter
{
    internal static void WriteSkipped(
        TextWriter writer,
        string command,
        string mode,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine(
            $"[Azure OpenAI] status=skipped outcome=not-called " +
            $"operation=GenerateExamplePrompts target=\"{command}\" mode={mode} reason={reason}");
    }
}
