// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ExamplePromptGeneratorStandalone.Generators;

/// <summary>
/// Shared bank of safe parameter values for deterministic prompt generation and repair.
/// Extracted from DeterministicExamplePromptGenerator for reuse.
/// IMPORTANT: The "value" key is intentionally excluded — it contains credential-shaped
/// strings that would be replaced by CredentialSanitizer, breaking coverage verification.
/// </summary>
public static class ParameterValueBank
{
    /// <summary>
    /// Safe concrete values indexed by canonical parameter name (no leading '--').
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> Bank = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["account"] = ["mystorageacct", "prodstore2026", "companydata2024", "webappstorage", "mediaacct2024"],
        ["vault"] = ["prod-kv", "dev-keyvault", "finance-kv", "webapp-kv", "backup-kv"],
        ["vaultname"] = ["prod-kv", "dev-keyvault", "finance-kv", "webapp-kv", "backup-kv"],
        ["resource-group"] = ["rg-prod", "my-resource-group", "rg-dev", "rg-company", "rg-analytics"],
        ["subscription"] = ["my-subscription", "contoso-sub", "dev-subscription", "prod-sub", "test-sub"],
        ["location"] = ["eastus", "westus2", "centralus", "northcentralus", "eastus2"],
        ["server-name"] = ["prod-sql-server", "dev-pg-server", "test-server-01", "analytics-server", "backup-server"],
        ["servername"] = ["prod-sql-server", "dev-pg-server", "test-server-01", "analytics-server", "backup-server"],
        ["database-name"] = ["mydb", "prod-database", "analytics-db", "app-data", "user-store"],
        ["databasename"] = ["mydb", "prod-database", "analytics-db", "app-data", "user-store"],
        ["container-name"] = ["backups", "documents", "images", "logs", "media"],
        ["containername"] = ["backups", "documents", "images", "logs", "media"],
        ["name"] = ["my-resource", "prod-item-01", "dev-config", "test-resource-2026", "analytics-asset"],
        ["secret-name"] = ["db-password", "api-key", "oauth-token", "storage-conn-string", "payment-key"],
        ["secretname"] = ["db-password", "api-key", "oauth-token", "storage-conn-string", "payment-key"],
        ["key-name"] = ["signing-key", "encryption-key", "rsa-key-01", "auth-key", "backup-key"],
        ["keyname"] = ["signing-key", "encryption-key", "rsa-key-01", "auth-key", "backup-key"],
        ["query"] = ["Heartbeat | take 10", "AzureMetrics | summarize count()", "requests | where success == false", "traces | top 5 by timestamp", "exceptions | count"],
        ["planid"] = ["plan-001", "marketing-plan", "dev-sprint-q1", "onboarding-plan", "project-alpha"],
        ["taskid"] = ["task-001", "review-docs", "fix-bug-42", "deploy-staging", "update-config"],
        ["groupid"] = ["group-engineering", "team-marketing", "dept-finance", "org-contoso", "project-alpha"],
        ["indexname"] = ["products-index", "search-docs", "knowledge-base", "catalog-idx", "content-index"],
    };

    /// <summary>
    /// Default fallback values when no specific bank entry exists.
    /// </summary>
    public static readonly string[] DefaultValues = ["my-value-1", "prod-value-02", "test-config-a", "dev-item-2026", "sample-value"];
}
