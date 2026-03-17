namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

public sealed class AzmcpCommandsDocument
{
    public string Title { get; set; } = string.Empty;

    public List<AzmcpGlobalOption> GlobalOptions { get; set; } = [];

    public List<AzmcpServiceSection> ServiceSections { get; set; } = [];
}

public sealed class AzmcpGlobalOption
{
    public string Name { get; set; } = string.Empty;

    public string? ValuePlaceholder { get; set; }

    public bool IsRequired { get; set; }

    public string? Default { get; set; }

    public string? Description { get; set; }

    public List<string>? AllowedValues { get; set; }
}

public sealed class AzmcpServiceSection
{
    public string Heading { get; set; } = string.Empty;

    public string AreaName { get; set; } = string.Empty;

    public List<AzmcpSubSection> SubSections { get; set; } = [];

    public List<AzmcpCommand> Commands { get; set; } = [];
}

public sealed class AzmcpSubSection
{
    public string Heading { get; set; } = string.Empty;

    public List<AzmcpCommand> Commands { get; set; } = [];
}

public sealed class AzmcpCommand
{
    public string CommandText { get; set; } = string.Empty;

    public List<AzmcpCommandParameter> Parameters { get; set; } = [];

    public AzmcpCommandMetadata? Metadata { get; set; }

    public string RawBlock { get; set; } = string.Empty;

    public bool IsExample { get; set; }
}

public sealed class AzmcpCommandMetadata
{
    public bool Destructive { get; set; }

    public bool Idempotent { get; set; }

    public bool OpenWorld { get; set; }

    public bool ReadOnly { get; set; }

    public bool Secret { get; set; }

    public bool LocalRequired { get; set; }
}

public sealed class AzmcpCommandParameter
{
    public string Name { get; set; } = string.Empty;

    public string? ValuePlaceholder { get; set; }

    public bool IsRequired { get; set; }

    public bool IsFlag { get; set; }

    public string? ShortAlias { get; set; }

    public string? Default { get; set; }

    public List<string>? AllowedValues { get; set; }
}
