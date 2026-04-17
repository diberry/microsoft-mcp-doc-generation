namespace AzmcpCommandParser.Models;

/// <summary>
/// Root document model representing the entire azmcp-commands.md file.
/// </summary>
public sealed class CommandDocument
{
    /// <summary>Document title (H1 heading).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Introductory text/notes before the first section.</summary>
    public string Introduction { get; set; } = string.Empty;

    /// <summary>Global options applicable to all commands.</summary>
    public List<GlobalOption> GlobalOptions { get; set; } = [];

    /// <summary>Server operations section (non-service commands).</summary>
    public ServerOperations? ServerOperations { get; set; }

    /// <summary>Service sections (e.g., "Azure Storage Operations").</summary>
    public List<ServiceSection> ServiceSections { get; set; } = [];

    /// <summary>Response format documentation.</summary>
    public ResponseFormat? ResponseFormat { get; set; }

    /// <summary>Error handling documentation.</summary>
    public string ErrorHandling { get; set; } = string.Empty;
}

/// <summary>
/// A global option from the Global Options table.
/// </summary>
public sealed class GlobalOption
{
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Default { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Server operations section containing modes and server start options.
/// </summary>
public sealed class ServerOperations
{
    public string Description { get; set; } = string.Empty;
    public List<ServerMode> Modes { get; set; } = [];
    public List<ServerStartOption> StartOptions { get; set; } = [];
    public string RawContent { get; set; } = string.Empty;
}

/// <summary>
/// A server mode (namespace, all, single, consolidated, etc.).
/// </summary>
public sealed class ServerMode
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> CodeBlocks { get; set; } = [];
}

/// <summary>
/// A server start command option from the options table.
/// </summary>
public sealed class ServerStartOption
{
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Default { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// A service section (e.g., "Azure Storage Operations", "Azure Key Vault Operations").
/// </summary>
public sealed class ServiceSection
{
    /// <summary>Section heading (e.g., "Azure Storage Operations").</summary>
    public string Heading { get; set; } = string.Empty;

    /// <summary>
    /// Derived service area name from the heading
    /// (e.g., "storage", "keyvault").
    /// </summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>Sub-sections (e.g., "Account", "Blob Storage", "Keys").</summary>
    public List<SubSection> SubSections { get; set; } = [];

    /// <summary>Commands directly under this section (not in a sub-section).</summary>
    public List<Command> Commands { get; set; } = [];

    /// <summary>Descriptive prose at the section level.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Parameter tables at the section level (not attached to a command).</summary>
    public List<ParameterTable> ParameterTables { get; set; } = [];
}

/// <summary>
/// A sub-section within a service section (H4 heading).
/// </summary>
public sealed class SubSection
{
    public string Heading { get; set; } = string.Empty;
    public List<Command> Commands { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public List<ParameterTable> ParameterTables { get; set; } = [];
}

/// <summary>
/// A single CLI command parsed from a code block.
/// </summary>
public sealed class Command
{
    /// <summary>Human-readable description from the comment line.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Full command string (e.g., "azmcp storage account get").</summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>
    /// The namespace/area (first word after "azmcp"),
    /// e.g., "storage", "keyvault".
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Sub-command parts after the namespace,
    /// e.g., ["account", "get"] or ["blob", "container", "create"].
    /// </summary>
    public List<string> SubCommands { get; set; } = [];

    /// <summary>Tool metadata flags parsed from the comment line.</summary>
    public ToolMetadata? Metadata { get; set; }

    /// <summary>Parameters extracted from the command syntax.</summary>
    public List<CommandParameter> Parameters { get; set; } = [];

    /// <summary>
    /// Mutually exclusive parameter groups parsed from [--a | --b --c] syntax.
    /// Each group contains two or more alternatives; each alternative is a set of
    /// parameters that must be provided together.
    /// </summary>
    public List<ParameterAlternativeGroup> ParameterAlternativeGroups { get; set; } = [];

    /// <summary>Whether this is an example (concrete values) vs. a definition (placeholders).</summary>
    public bool IsExample { get; set; }

    /// <summary>The raw code block text this command was parsed from.</summary>
    public string RawBlock { get; set; } = string.Empty;

    /// <summary>Line number in the source markdown where this command starts.</summary>
    public int SourceLine { get; set; }
}

/// <summary>
/// Tool metadata flags (6 booleans parsed from the checkmark comment line).
/// </summary>
public sealed class ToolMetadata
{
    /// <summary>Whether the command is destructive (modifies/deletes resources).</summary>
    public bool Destructive { get; set; }

    /// <summary>Whether the command is idempotent (safe to repeat).</summary>
    public bool Idempotent { get; set; }

    /// <summary>Whether the command can access external resources.</summary>
    public bool OpenWorld { get; set; }

    /// <summary>Whether the command only reads data (no modifications).</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Whether the command handles secrets/sensitive data.</summary>
    public bool Secret { get; set; }

    /// <summary>Whether a local file/resource is required.</summary>
    public bool LocalRequired { get; set; }
}

/// <summary>
/// A parameter parsed from command syntax.
/// </summary>
public sealed class CommandParameter
{
    /// <summary>Parameter name (e.g., "--subscription", "--resource-group").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Placeholder value name (e.g., "subscription", "resource-group").</summary>
    public string ValuePlaceholder { get; set; } = string.Empty;

    /// <summary>Whether this parameter is required (not wrapped in [...]).</summary>
    public bool IsRequired { get; set; }

    /// <summary>Whether this is a flag/switch with no value (e.g., "--read-only", "--is-html").</summary>
    public bool IsFlag { get; set; }

    /// <summary>Short alias if present (e.g., "-g" for "--resource-group").</summary>
    public string? ShortAlias { get; set; }

    /// <summary>
    /// Allowed values when specified inline (e.g., "simple|detailed", "Enabled|Disabled").
    /// </summary>
    public List<string>? AllowedValues { get; set; }

}

/// <summary>
/// Represents mutually exclusive parameter alternatives in command syntax.
/// E.g., [--cluster-uri &lt;cluster-uri&gt; | --subscription &lt;subscription&gt; --cluster &lt;cluster&gt;]
/// has two alternatives: {cluster-uri} OR {subscription, cluster}.
/// </summary>
public sealed class ParameterAlternativeGroup
{
    /// <summary>
    /// Each alternative is a list of parameters that must be used together.
    /// Exactly one alternative should be provided.
    /// </summary>
    public List<List<CommandParameter>> Alternatives { get; set; } = [];
}

/// <summary>
/// A parameter table parsed from markdown table syntax.
/// </summary>
public sealed class ParameterTable
{
    public List<ParameterTableEntry> Entries { get; set; } = [];
}

/// <summary>
/// A single row from a parameter table.
/// </summary>
public sealed class ParameterTableEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Default { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Response format documentation section.
/// </summary>
public sealed class ResponseFormat
{
    public string Description { get; set; } = string.Empty;
    public string JsonSchema { get; set; } = string.Empty;
    public List<ResponseField> Fields { get; set; } = [];
}

/// <summary>
/// A field in the response format.
/// </summary>
public sealed class ResponseField
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
