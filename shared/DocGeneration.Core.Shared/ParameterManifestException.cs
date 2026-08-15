// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Thrown when a parameter manifest fails validation. Carries a stable error code
/// and the path to the manifest file.
/// </summary>
public sealed class ParameterManifestException : Exception
{
    public string ErrorCode { get; }
    public string ManifestPath { get; }

    public ParameterManifestException(string errorCode, string manifestPath, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        ManifestPath = manifestPath;
    }

    public ParameterManifestException(string errorCode, string manifestPath, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ManifestPath = manifestPath;
    }
}
