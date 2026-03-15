// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HandlebarsDotNet;
using TemplateEngine.Helpers;

namespace TemplateEngine;

/// <summary>
/// Manages Handlebars template compilation and custom helper registration.
/// Responsible for all template-related operations and rendering logic.
/// </summary>
public static class HandlebarsTemplateEngine
{
    /// <summary>
    /// Creates a configured Handlebars instance with all custom helpers registered.
    /// </summary>
    public static IHandlebars CreateEngine()
    {
        var handlebars = Handlebars.Create();
        CoreHelpers.Register(handlebars);
        McpHelpers.Register(handlebars);
        return handlebars;
    }

    /// <summary>
    /// Processes a template file with data and returns the rendered result.
    /// </summary>
    public static async Task<string> ProcessTemplateAsync(string templateFile, Dictionary<string, object> data)
    {
        var handlebars = CreateEngine();

        var templateContent = await File.ReadAllTextAsync(templateFile);
        var template = handlebars.Compile(templateContent);

        return template(data);
    }

    /// <summary>
    /// Processes a template string with data and returns the rendered result.
    /// </summary>
    public static string ProcessTemplateString(string templateContent, Dictionary<string, object> data)
    {
        var handlebars = CreateEngine();
        var template = handlebars.Compile(templateContent);
        return template(data);
    }
}
