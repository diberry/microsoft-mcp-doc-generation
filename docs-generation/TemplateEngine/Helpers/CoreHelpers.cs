// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using HandlebarsDotNet;

namespace TemplateEngine.Helpers;

/// <summary>
/// Generic Handlebars helpers reusable by any project.
/// </summary>
public static class CoreHelpers
{
    /// <summary>
    /// Registers all core helpers on the given Handlebars instance.
    /// </summary>
    public static void Register(IHandlebars handlebars)
    {
        // Format date helper
        handlebars.RegisterHelper("formatDate", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (arguments[0] is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");

            if (DateTime.TryParse(arguments[0].ToString(), out var parsedDate))
                return parsedDate.ToString("yyyy-MM-dd HH:mm:ss UTC");

            return arguments[0].ToString();
        });

        // Format date short helper (for metadata headers)
        handlebars.RegisterHelper("formatDateShort", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return DateTime.UtcNow.ToString("MM/dd/yyyy");

            if (arguments[0] is DateTime dateTime)
                return dateTime.ToString("MM/dd/yyyy");

            if (DateTime.TryParse(arguments[0].ToString(), out var parsedDate))
                return parsedDate.ToString("MM/dd/yyyy");

            return arguments[0].ToString();
        });

        // Kebab case helper
        handlebars.RegisterHelper("kebabCase", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var str = arguments[0].ToString();
            return str?.ToLowerInvariant()
                .Replace(' ', '-')
                .Replace('_', '-')
                .RegexReplace("[^a-z0-9-]", "") ?? string.Empty;
        });

        // Slugify helper - converts text to URL-safe slug for anchor links
        handlebars.RegisterHelper("slugify", (context, arguments) =>
        {
            if (arguments.Length == 0 || arguments[0] == null)
                return string.Empty;

            var str = arguments[0].ToString();
            return str?.ToLowerInvariant()
                .Replace(' ', '-')
                .Replace('_', '-')
                .RegexReplace("[^a-z0-9-]", "") ?? string.Empty;
        });

        // Math helpers
        handlebars.RegisterHelper("add", (context, arguments) =>
        {
            if (arguments.Length < 2) return 0;

            if (double.TryParse(arguments[0]?.ToString(), out var a) &&
                double.TryParse(arguments[1]?.ToString(), out var b))
                return a + b;

            return 0;
        });

        handlebars.RegisterHelper("divide", (context, arguments) =>
        {
            if (arguments.Length < 2) return 0;

            if (double.TryParse(arguments[0]?.ToString(), out var a) &&
                double.TryParse(arguments[1]?.ToString(), out var b) && b != 0)
                return a / b;

            return 0;
        });

        handlebars.RegisterHelper("round", (context, arguments) =>
        {
            if (arguments.Length < 1) return 0;

            if (!double.TryParse(arguments[0]?.ToString(), out var num))
                return 0;

            var precision = 1;
            if (arguments.Length > 1 && int.TryParse(arguments[1]?.ToString(), out var p))
                precision = p;

            return Math.Round(num, precision);
        });

        // Required helper for boolean display
        handlebars.RegisterHelper("requiredIcon", (context, arguments) =>
        {
            if (arguments.Length == 0) return "❌";

            var value = arguments[0];
            if (value is bool boolValue)
                return boolValue ? "✅" : "❌";

            if (bool.TryParse(value?.ToString(), out var parsedBool))
                return parsedBool ? "✅" : "❌";

            return "❌";
        });

        // Concatenate strings
        handlebars.RegisterHelper("concat", (context, arguments) =>
        {
            return string.Join("", arguments.Select(arg => arg?.ToString() ?? string.Empty));
        });

        // Equality comparison helper
        handlebars.RegisterHelper("eq", (context, arguments) =>
        {
            if (arguments.Length < 2)
                return false;

            var left = arguments[0]?.ToString();
            var right = arguments[1]?.ToString();

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        });

        // String replacement helper
        handlebars.RegisterHelper("replace", (context, arguments) =>
        {
            if (arguments.Length < 3) return arguments.Length > 0 ? arguments[0]?.ToString() : string.Empty;

            var str = arguments[0]?.ToString() ?? string.Empty;
            var oldValue = arguments[1]?.ToString() ?? string.Empty;
            var newValue = arguments[2]?.ToString() ?? string.Empty;

            return str.Replace(oldValue, newValue);
        });
    }

    /// <summary>
    /// Regex-based string replacement extension method.
    /// </summary>
    internal static string RegexReplace(this string input, string pattern, string replacement)
    {
        return Regex.Replace(input, pattern, replacement);
    }
}
