namespace CSharpGenerator.Models;

/// <summary>
/// Represents a tool parameter (option) with its requirement level.
/// 
/// Parameter requirement levels:
///   - Required:  Always required for every invocation.
///   - Optional:  Never required; provides additional context or filtering.
///   - Required*: Conditionally required — required when certain other parameters are absent.
///   - Optional*: Conditionally required — optional by default but becomes required depending
///                on how other parameters are used (e.g., "at least one of X or Y is required").
///
/// A parameter can be both optional AND conditional. The <see cref="Required"/> boolean captures
/// the base level; the asterisk is appended by <c>ParameterGenerator.BuildRequiredText</c> when
/// the parameter appears in the tool's conditional-required set.
/// </summary>
public class Option
{
    public string? Name { get; set; }
    public string? NL_Name { get; set; }
    public string? Type { get; set; }
    public bool Required { get; set; }
    public string RequiredText { get; set; } = "";
    public string? Description { get; set; }
}
