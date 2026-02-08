using HandlebarsDotNet;

namespace ExamplePromptGeneratorStandalone.Utilities;

/// <summary>
/// Simple Handlebars template processor for example prompts.
/// </summary>
public static class TemplateEngine
{
    public static async Task<string> ProcessAsync(string templateFile, Dictionary<string, object> data)
    {
        var handlebars = Handlebars.Create();
        RegisterHelpers(handlebars);

        var content = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(content);
        return template(data);
    }

    private static void RegisterHelpers(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("formatDate", (context, args) =>
        {
            if (args.Length == 0 || args[0] == null)
                return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (args[0] is DateTime dt)
                return dt.ToString("yyyy-MM-dd HH:mm:ss UTC");

            return args[0].ToString();
        });
    }
}
