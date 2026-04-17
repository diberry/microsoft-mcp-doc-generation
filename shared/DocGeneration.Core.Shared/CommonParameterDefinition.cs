// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Model for deserializing common parameters from JSON configuration file.
/// Represents the structure of entries in common-parameters.json.
/// </summary>
public class CommonParameterDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsRequired { get; set; }
    public string Description { get; set; } = "";
}
