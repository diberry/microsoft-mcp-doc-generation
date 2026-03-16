using System.IO;
using ExamplePromptGeneratorStandalone;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

public class ValidationFeedbackFileTests
{
    [Fact]
    public async Task LoadValidationFeedbackAsync_ReadsExistingFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"validation-feedback-{Guid.NewGuid():N}.md");

        try
        {
            await File.WriteAllTextAsync(filePath, "feedback content");

            var content = await Program.LoadValidationFeedbackAsync(filePath);

            Assert.Equal("feedback content", content);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
