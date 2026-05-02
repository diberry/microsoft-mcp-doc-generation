# Key Vault Migration Guide

## Migrating from FOUNDRY_API_KEY env var to Key Vault

### What changed
The infrastructure no longer outputs FOUNDRY_API_KEY as a plain-text Bicep output.
The API key is now stored in Azure Key Vault. Applications retrieve it via DefaultAzureCredential.

### How to migrate
1. No code changes needed if you use DefaultAzureCredential.
2. New env vars from azd up: FOUNDRY_USE_DEFAULT_CREDENTIAL=true, KEYVAULT_URI, KEYVAULT_SECRET_NAME
3. Assign Key Vault Secrets User RBAC role to the consuming identity.
4. Fallback: local FOUNDRY_API_KEY env var still works for development.

### SECONDARY_FOUNDRY_API_KEY removal
This was only a Bicep output. No app code, workflow, or script consumed it. Removal is safe.
