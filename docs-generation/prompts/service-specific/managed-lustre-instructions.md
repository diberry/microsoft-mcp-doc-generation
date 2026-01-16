# =============================================================================
# AZURE MANAGED LUSTRE SERVICE-SPECIFIC INSTRUCTIONS
# =============================================================================
# These instructions apply ONLY to Azure Managed Lustre example prompts.
# They are based on PR review feedback from the Azure Managed Lustre team.
# Source: https://github.com/MicrosoftDocs/azure-dev-docs-pr/pull/8353
# =============================================================================

## TERMINOLOGY REQUIREMENTS

### Use "jobs" or "tasks" terminology for auto-import/auto-export operations
When referring to auto-import or auto-export operations, use "jobs" or "tasks" terminology, NOT "settings", "configuration", or "details":
- ✅ CORRECT: "Get the autoimport jobs for filesystem 'LustreFs01'"
- ✅ CORRECT: "Show me autoimport tasks for filesystem 'TrainingDataFs'"
- ✅ CORRECT: "List all autoimport jobs on filesystem 'LustreMain'"
- ❌ WRONG: "Get the autoimport settings for filesystem 'LustreFs01'"
- ❌ WRONG: "Show me the blob autoimport configuration for filesystem..."
- ❌ WRONG: "Retrieve autoimport details of the Managed Lustre filesystem..."
- ❌ WRONG: "Fetch the autoimport info for filesystem..."

### Same rule applies to auto-export operations
- ✅ CORRECT: "Get autoexport jobs for filesystem 'DataLakeFS'"
- ✅ CORRECT: "Show me the autoexport tasks on filesystem 'ResearchFs'"
- ❌ WRONG: "Get the blob autoexport settings for filesystem..."
- ❌ WRONG: "Show me the autoexport configuration of the Managed Lustre filesystem..."

## EXAMPLE NAMING CONVENTIONS

### Use HPC/Training-focused naming for examples
Azure Managed Lustre is primarily used for HPC (High Performance Computing) and AI/ML training workloads. Example names should reflect these use cases:

**Preferred filesystem names:**
- ✅ 'TrainingDataFs', 'trainingLustre01', 'ResearchFs', 'HPCDataFs'
- ✅ 'ComputeClusterFs', 'MLTrainingFs', 'SimulationFs'
- ⚠️ LESS PREFERRED: 'SalesDataFs', 'AnalyticsFs', 'ArchiveFs' (not typical Lustre use cases)

**Preferred resource group names:**
- ✅ 'rg-training', 'rg-training-lustre', 'rg-hpc-cluster', 'rg-research'
- ✅ 'rg-compute-westus', 'rg-ml-training', 'rg-simulation'
- ⚠️ LESS PREFERRED: 'rg-salesapp', 'rg-enterprise-services' (not typical Lustre use cases)

## INCLUDE OPTIONAL PARAMETERS IN EXAMPLES

### Auto-import: Include examples with prefix and conflict resolution mode
For auto-import create operations, include at least one example that demonstrates optional parameters:

- ✅ CORRECT: "Create an autoimport job for filesystem 'DataFS' in resource group 'rg-training' with prefix '/data/incoming' and conflict resolution mode 'OverwriteIfDirty'"
- ✅ CORRECT: "Set up autoimport on filesystem 'TrainingFs' with prefixes '/models' and '/datasets'"
- ❌ INCOMPLETE: Only providing examples with just filesystem and resource group names

### Auto-export: Include examples with prefix parameter
For auto-export create operations, include at least one example with the optional prefix parameter:

- ✅ CORRECT: "Create an autoexport job for filesystem 'TrainingFs' with prefix '/results'"
- ✅ CORRECT: "Set up autoexport on filesystem 'MLOutputFs' exporting from '/training-output'"

## PROMPT STRUCTURE GUIDELINES

### Preferred prompt patterns for Managed Lustre

**For auto-import get (list jobs):**
- "Get all autoimport jobs for filesystem 'TrainingDataFs' in resource group 'rg-training'"
- "List autoimport jobs on filesystem 'HPCClusterFs' in resource group 'rg-hpc'"
- "Show me the autoimport job 'dailySyncJob' on filesystem 'ResearchFs'"

**For auto-import create:**
- "Create an autoimport job for filesystem 'TrainingDataFs' in resource group 'rg-training'"
- "Create an autoimport job on filesystem 'MLDataFs' with prefix '/datasets' and conflict resolution mode 'Skip'"
- "Start an autoimport job for filesystem 'ComputeFs' with prefixes '/input' and '/models'"

**For auto-import cancel/delete:**
- "Cancel the autoimport job 'nightlySync' on filesystem 'TrainingFs'"
- "Delete the autoimport job 'importJob123' from filesystem 'HPCDataFs'"

**For auto-export get (list jobs):**
- "Get all autoexport jobs for filesystem 'TrainingDataFs' in resource group 'rg-training'"
- "List autoexport jobs on filesystem 'ResearchFs'"
- "Show me autoexport job 'backupJob' on filesystem 'ComputeOutputFs'"

**For auto-export create:**
- "Create an autoexport job for filesystem 'TrainingOutputFs' in resource group 'rg-ml'"
- "Set up autoexport on filesystem 'SimulationFs' with prefix '/results'"

**For auto-export cancel/delete:**
- "Cancel the autoexport job 'dailyBackup' on filesystem 'TrainingFs'"
- "Delete the autoexport job 'exportJob01' from filesystem 'HPCOutputFs'"

## EXAMPLE CORRECTIONS FROM PR REVIEW

### Original (Incorrect)
```
- "Get the autoimport settings for filesystem 'LustreFs01' in resource group 'rg-storage-prod'"
- "Show me the blob autoimport configuration for filesystem 'archiveLustre' within resource group 'rg-data-lake'"
- "Can you provide the autoexport information for the Lustre filesystem 'SalesDataFs' under resource group 'rg-salesapp'?"
```

### Corrected
```
- "Get the autoimport jobs for filesystem 'TrainingDataFs' in resource group 'rg-training'"
- "List all autoimport jobs on filesystem 'HPCClusterFs' in resource group 'rg-hpc-cluster'"
- "Show me the autoexport jobs for filesystem 'TrainingOutputFs' in resource group 'rg-training-lustre'"
```

## SUMMARY OF KEY RULES

1. **Jobs, not settings** - Always use "jobs" or "tasks" for auto-import/auto-export, never "settings", "configuration", or "details"
2. **HPC/Training context** - Use names that reflect HPC and training workloads (Training, Research, Simulation, HPC, Compute)
3. **Include optional parameters** - Show examples using prefix paths and conflict resolution modes
4. **Resource group naming** - Use rg-training, rg-hpc, rg-research style naming
