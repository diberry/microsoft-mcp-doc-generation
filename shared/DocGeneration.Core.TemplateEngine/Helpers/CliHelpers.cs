// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HandlebarsDotNet;

namespace TemplateEngine.Helpers;

/// <summary>
/// CLI-specific Handlebars helpers for Azure MCP CLI documentation generation.
/// </summary>
public static class CliHelpers
{
    /// <summary>
    /// Registers all CLI-specific helpers on the given Handlebars instance.
    /// </summary>
    public static void Register(IHandlebars handlebars)
    {
        // tabHeader: generates ### [Label](#tab/tab-id) for tabbed conceptual tags
        handlebars.RegisterHelper("tabHeader", (context, arguments) =>
        {
            if (arguments.Length < 2) return "";
            var label = arguments[0]?.ToString() ?? "";
            var tabId = arguments[1]?.ToString() ?? "";
            return $"### [{label}](#tab/{tabId})";
        });

        // cliCommand: renders full CLI command with azmcp prefix
        handlebars.RegisterHelper("cliCommand", (context, arguments) =>
        {
            if (arguments.Length == 0) return "";
            var command = arguments[0]?.ToString() ?? "";
            return $"azmcp {command}";
        });

        // cliSwitchDefault: renders default value or dash, with pipe escaping for table cells
        handlebars.RegisterHelper("cliSwitchDefault", (context, arguments) =>
        {
            if (arguments.Length == 0) return "-";
            var arg = arguments[0];
            if (arg == null || arg is HandlebarsDotNet.UndefinedBindingResult) return "-";
            var val = arg.ToString();
            return string.IsNullOrWhiteSpace(val) ? "-" : EscapePipe(val);
        });

        // escapeTableCell: escapes pipe characters for markdown table cells
        handlebars.RegisterHelper("escapeTableCell", (context, arguments) =>
        {
            if (arguments.Length == 0) return "";
            var arg = arguments[0];
            if (arg == null || arg is HandlebarsDotNet.UndefinedBindingResult) return "";
            var val = arg.ToString() ?? "";
            return EscapePipe(val);
        });
    }

    /// <summary>
    /// Escapes pipe characters for use in markdown table cells.
    /// </summary>
    internal static string EscapePipe(string text)
        => text.Replace("|", "\\|");
}
