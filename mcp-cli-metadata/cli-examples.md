## acr registry

```console
azmcp acr registry list \
  [--resource-group <resource-group>]
```


## acr registry repository

```console
azmcp acr registry repository list \
  [--registry <registry>] \
  [--resource-group <resource-group>]
```


## advisor recommendation

```console
azmcp advisor recommendation list \
  [--resource-group <resource-group>]
```


## aks cluster

```console
azmcp aks cluster get \
  [--cluster <cluster>] \
  [--resource-group <resource-group>]
```


## aks nodepool

```console
azmcp aks nodepool get \
  --cluster <cluster> \
  --resource-group <resource-group> \
  [--nodepool <nodepool>]
```


## appconfig account

```console
azmcp appconfig account list \
  [--resource-group <resource-group>]
```


## appconfig kv

```console
azmcp appconfig kv delete \
  --account <account> \
  --key <key> \
  [--content-type <content-type>] \
  [--label <label>]
```

```console
azmcp appconfig kv get \
  --account <account> \
  [--key <key>] \
  [--key-filter <key-filter>] \
  [--label <label>] \
  [--label-filter <label-filter>]
```


## appconfig kv lock

```console
azmcp appconfig kv lock set \
  --account <account> \
  --key <key> \
  [--label <label>] \
  [--lock <lock>]
```


## appconfig kv

```console
azmcp appconfig kv set \
  --account <account> \
  --key <key> \
  --value <value> \
  [--content-type <content-type>] \
  [--label <label>] \
  [--tags <tags>]
```


## applens resource

```console
azmcp applens resource diagnose \
  --question <question> \
  --resource <resource> \
  [--resource-group <resource-group>] \
  [--resource-type <resource-type>]
```


## applicationinsights recommendation

```console
azmcp applicationinsights recommendation list \
  [--resource-group <resource-group>]
```


## appservice database

```console
azmcp appservice database add \
  --app <app> \
  --database <database> \
  --database-server <database-server> \
  --database-type <database-type> \
  --resource-group <resource-group> \
  [--connection-string <connection-string>]
```


## appservice webapp

```console
azmcp appservice webapp change-state \
  --app <app> \
  --resource-group <resource-group> \
  --state-change <state-change> \
  [--soft-restart <soft-restart>] \
  [--wait-for-completion <wait-for-completion>]
```


## appservice webapp deployment

```console
azmcp appservice webapp deployment get \
  --app <app> \
  --resource-group <resource-group> \
  [--deployment-id <deployment-id>]
```


## appservice webapp diagnostic

```console
azmcp appservice webapp diagnostic diagnose \
  --app <app> \
  --detector-id <detector-id> \
  --resource-group <resource-group> \
  [--end-time <end-time>] \
  [--interval <interval>] \
  [--start-time <start-time>]
```

```console
azmcp appservice webapp diagnostic list \
  --app <app> \
  --resource-group <resource-group>
```


## appservice webapp

```console
azmcp appservice webapp get \
  [--app <app>] \
  [--resource-group <resource-group>]
```


## appservice webapp settings

```console
azmcp appservice webapp settings get-appsettings \
  --app <app> \
  --resource-group <resource-group>
```

```console
azmcp appservice webapp settings update-appsettings \
  --app <app> \
  --resource-group <resource-group> \
  --setting-name <setting-name> \
  --setting-update-type <setting-update-type> \
  [--setting-value <setting-value>]
```


## azurebackup backup

```console
azmcp azurebackup backup status \
  --datasource-id <datasource-id> \
  --location <location>
```


## azurebackup disasterrecovery

```console
azmcp azurebackup disasterrecovery enable-crr \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>]
```


## azurebackup governance

```console
azmcp azurebackup governance find-unprotected \
  [--resource-group <resource-group>] \
  [--resource-type-filter <resource-type-filter>] \
  [--tag-filter <tag-filter>]
```

```console
azmcp azurebackup governance immutability \
  --immutability-state <immutability-state> \
  --resource-group <resource-group> \
  --vault <vault> \
  [--vault-type <vault-type>]
```

```console
azmcp azurebackup governance soft-delete \
  --resource-group <resource-group> \
  --soft-delete <soft-delete> \
  --vault <vault> \
  [--soft-delete-retention-days <soft-delete-retention-days>] \
  [--vault-type <vault-type>]
```


## azurebackup job

```console
azmcp azurebackup job get \
  --resource-group <resource-group> \
  --vault <vault> \
  [--job <job>] \
  [--vault-type <vault-type>]
```


## azurebackup policy

```console
azmcp azurebackup policy create \
  --policy <policy> \
  --resource-group <resource-group> \
  --vault <vault> \
  --workload-type <workload-type> \
  [--archive-tier-after-days <archive-tier-after-days>] \
  [--archive-tier-mode <archive-tier-mode>] \
  [--backup-mode <backup-mode>] \
  [--daily-retention-days <daily-retention-days>] \
  [--differential-retention-days <differential-retention-days>] \
  [--differential-schedule-days-of-week <differential-schedule-days-of-week>] \
  [--enable-snapshot-backup <enable-snapshot-backup>] \
  [--enable-vault-tier-copy <enable-vault-tier-copy>] \
  [--full-schedule-days-of-week <full-schedule-days-of-week>] \
  [--full-schedule-frequency <full-schedule-frequency>] \
  [--hourly-interval-hours <hourly-interval-hours>] \
  [--hourly-window-duration-hours <hourly-window-duration-hours>] \
  [--hourly-window-start-time <hourly-window-start-time>] \
  [--incremental-retention-days <incremental-retention-days>] \
  [--incremental-schedule-days-of-week <incremental-schedule-days-of-week>] \
  [--instant-rp-resource-group <instant-rp-resource-group>] \
  [--instant-rp-retention-days <instant-rp-retention-days>] \
  [--is-compression <is-compression>] \
  [--is-sql-compression <is-sql-compression>] \
  [--log-frequency-minutes <log-frequency-minutes>] \
  [--log-retention-days <log-retention-days>] \
  [--monthly-retention-days-of-month <monthly-retention-days-of-month>] \
  [--monthly-retention-days-of-week <monthly-retention-days-of-week>] \
  [--monthly-retention-months <monthly-retention-months>] \
  [--monthly-retention-week-of-month <monthly-retention-week-of-month>] \
  [--pitr-retention-days <pitr-retention-days>] \
  [--policy-sub-type <policy-sub-type>] \
  [--policy-tags <policy-tags>] \
  [--schedule-days-of-week <schedule-days-of-week>] \
  [--schedule-frequency <schedule-frequency>] \
  [--schedule-times <schedule-times>] \
  [--smart-tier <smart-tier>] \
  [--snapshot-consistency <snapshot-consistency>] \
  [--snapshot-instant-rp-resource-group <snapshot-instant-rp-resource-group>] \
  [--snapshot-instant-rp-retention-days <snapshot-instant-rp-retention-days>] \
  [--time-zone <time-zone>] \
  [--vault-tier-copy-after-days <vault-tier-copy-after-days>] \
  [--vault-type <vault-type>] \
  [--weekly-retention-days-of-week <weekly-retention-days-of-week>] \
  [--weekly-retention-weeks <weekly-retention-weeks>] \
  [--yearly-retention-days-of-month <yearly-retention-days-of-month>] \
  [--yearly-retention-days-of-week <yearly-retention-days-of-week>] \
  [--yearly-retention-months <yearly-retention-months>] \
  [--yearly-retention-week-of-month <yearly-retention-week-of-month>] \
  [--yearly-retention-years <yearly-retention-years>]
```

```console
azmcp azurebackup policy get \
  --resource-group <resource-group> \
  --vault <vault> \
  [--policy <policy>] \
  [--vault-type <vault-type>]
```

```console
azmcp azurebackup policy update \
  --policy <policy> \
  --resource-group <resource-group> \
  --vault <vault> \
  [--daily-retention-days <daily-retention-days>] \
  [--schedule-time <schedule-time>] \
  [--vault-type <vault-type>]
```


## azurebackup protectableitem

```console
azmcp azurebackup protectableitem list \
  --resource-group <resource-group> \
  --vault <vault> \
  [--container <container>] \
  [--vault-type <vault-type>] \
  [--workload-type <workload-type>]
```


## azurebackup protecteditem

```console
azmcp azurebackup protecteditem get \
  --resource-group <resource-group> \
  --vault <vault> \
  [--container <container>] \
  [--protected-item <protected-item>] \
  [--vault-type <vault-type>]
```

```console
azmcp azurebackup protecteditem protect \
  --datasource-id <datasource-id> \
  --policy <policy> \
  --resource-group <resource-group> \
  --vault <vault> \
  [--aks-excluded-namespaces <aks-excluded-namespaces>] \
  [--aks-include-cluster-scope-resources <aks-include-cluster-scope-resources>] \
  [--aks-included-namespaces <aks-included-namespaces>] \
  [--aks-label-selectors <aks-label-selectors>] \
  [--aks-snapshot-resource-group <aks-snapshot-resource-group>] \
  [--container <container>] \
  [--datasource-type <datasource-type>] \
  [--vault-type <vault-type>]
```

```console
azmcp azurebackup protecteditem undelete \
  --datasource-id <datasource-id> \
  --resource-group <resource-group> \
  --vault <vault> \
  [--container <container>] \
  [--vault-type <vault-type>]
```


## azurebackup recoverypoint

```console
azmcp azurebackup recoverypoint get \
  --protected-item <protected-item> \
  --resource-group <resource-group> \
  --vault <vault> \
  [--container <container>] \
  [--recovery-point <recovery-point>] \
  [--vault-type <vault-type>]
```


## azurebackup security

```console
azmcp azurebackup security configure-encryption \
  --identity-type <identity-type> \
  --key-name <key-name> \
  --key-vault-uri <key-vault-uri> \
  --resource-group <resource-group> \
  --vault <vault> \
  [--key-version <key-version>] \
  [--user-assigned-identity-id <user-assigned-identity-id>] \
  [--vault-type <vault-type>]
```

```console
azmcp azurebackup security configure-mua \
  --resource-group <resource-group> \
  --vault <vault> \
  [--resource-guard-id <resource-guard-id>] \
  [--vault-type <vault-type>]
```


## azurebackup vault

```console
azmcp azurebackup vault create \
  --location <location> \
  --resource-group <resource-group> \
  --vault <vault> \
  [--sku <sku>] \
  [--storage-type <storage-type>] \
  [--vault-type <vault-type>]
```

```console
azmcp azurebackup vault get \
  [--resource-group <resource-group>] \
  [--vault <vault>] \
  [--vault-type <vault-type>]
```

```console
azmcp azurebackup vault update \
  --resource-group <resource-group> \
  --vault <vault> \
  [--identity-type <identity-type>] \
  [--immutability-state <immutability-state>] \
  [--redundancy <redundancy>] \
  [--soft-delete <soft-delete>] \
  [--soft-delete-retention-days <soft-delete-retention-days>] \
  [--tags <tags>] \
  [--vault-type <vault-type>]
```


## azuremigrate platformlandingzone

```console
azmcp azuremigrate platformlandingzone getguidance \
  --scenario <scenario> \
  [--list-policies <list-policies>] \
  [--policy-name <policy-name>]
```

```console
azmcp azuremigrate platformlandingzone request \
  --action <action> \
  --migrate-project-name <migrate-project-name> \
  --resource-group <resource-group> \
  [--connectivity-subscription-id <connectivity-subscription-id>] \
  [--environment-name <environment-name>] \
  [--firewall-type <firewall-type>] \
  [--identity-subscription-id <identity-subscription-id>] \
  [--location <location>] \
  [--management-subscription-id <management-subscription-id>] \
  [--migrate-project-resource-id <migrate-project-resource-id>] \
  [--network-architecture <network-architecture>] \
  [--organization-name <organization-name>] \
  [--region-type <region-type>] \
  [--regions <regions>] \
  [--security-subscription-id <security-subscription-id>] \
  [--version-control-system <version-control-system>]
```


## azureterraform avm

```console
azmcp azureterraform avm get \
  --module-name <module-name> \
  --module-version <module-version>
```

```console
azmcp azureterraform avm list
```

```console
azmcp azureterraform avm versions \
  --module-name <module-name>
```


## azureterraform azapi

```console
azmcp azureterraform azapi get \
  --resource-type <resource-type> \
  [--api-version <api-version>]
```


## azureterraform aztfexport

```console
azmcp azureterraform aztfexport query \
  --query <query> \
  [--continue-on-error <continue-on-error>] \
  [--include-role-assignment <include-role-assignment>] \
  [--name-pattern <name-pattern>] \
  [--output-folder <output-folder>] \
  [--parallelism <parallelism>] \
  [--provider <provider>]
```

```console
azmcp azureterraform aztfexport resource \
  --resource-id <resource-id> \
  [--continue-on-error <continue-on-error>] \
  [--include-role-assignment <include-role-assignment>] \
  [--output-folder <output-folder>] \
  [--parallelism <parallelism>] \
  [--provider <provider>] \
  [--terraform-resource-name <terraform-resource-name>]
```

```console
azmcp azureterraform aztfexport resourcegroup \
  --resource-group <resource-group> \
  [--continue-on-error <continue-on-error>] \
  [--include-role-assignment <include-role-assignment>] \
  [--name-pattern <name-pattern>] \
  [--output-folder <output-folder>] \
  [--parallelism <parallelism>] \
  [--provider <provider>]
```


## azureterraform azurerm

```console
azmcp azureterraform azurerm get \
  --resource-type <resource-type> \
  [--argument <argument>] \
  [--attribute <attribute>] \
  [--doc-type <doc-type>]
```


## azureterraform conftest

```console
azmcp azureterraform conftest plan \
  --plan-folder <plan-folder> \
  [--custom-policies <custom-policies>] \
  [--policy-set <policy-set>] \
  [--severity-filter <severity-filter>]
```

```console
azmcp azureterraform conftest workspace \
  --workspace-folder <workspace-folder> \
  [--custom-policies <custom-policies>] \
  [--policy-set <policy-set>] \
  [--severity-filter <severity-filter>]
```


## azureterraformbestpractices

```console
azmcp azureterraformbestpractices get
```


## bicepschema

```console
azmcp bicepschema get \
  --resource-type <resource-type>
```


## cloudarchitect

```console
azmcp cloudarchitect design \
  [--answer <answer>] \
  [--confidence-score <confidence-score>] \
  [--next-question-needed <next-question-needed>] \
  [--question <question>] \
  [--question-number <question-number>] \
  [--state <state>] \
  [--total-questions <total-questions>]
```


## communication email

```console
azmcp communication email send \
  --endpoint <endpoint> \
  --from <from> \
  --message <message> \
  --subject <subject> \
  --to <to> \
  [--bcc <bcc>] \
  [--cc <cc>] \
  [--is-html <is-html>] \
  [--reply-to <reply-to>] \
  [--sender-name <sender-name>]
```


## communication sms

```console
azmcp communication sms send \
  --endpoint <endpoint> \
  --from <from> \
  --message <message> \
  --to <to> \
  [--enable-delivery-report <enable-delivery-report>] \
  [--tag <tag>]
```


## compute disk

```console
azmcp compute disk create \
  --disk-name <disk-name> \
  --resource-group <resource-group> \
  [--disk-access <disk-access>] \
  [--disk-encryption-set <disk-encryption-set>] \
  [--disk-iops-read-write <disk-iops-read-write>] \
  [--disk-mbps-read-write <disk-mbps-read-write>] \
  [--enable-bursting <enable-bursting>] \
  [--encryption-type <encryption-type>] \
  [--gallery-image-reference <gallery-image-reference>] \
  [--gallery-image-reference-lun <gallery-image-reference-lun>] \
  [--hyper-v-generation <hyper-v-generation>] \
  [--location <location>] \
  [--max-shares <max-shares>] \
  [--network-access-policy <network-access-policy>] \
  [--os-type <os-type>] \
  [--security-type <security-type>] \
  [--size-gb <size-gb>] \
  [--sku <sku>] \
  [--source <source>] \
  [--tags <tags>] \
  [--tier <tier>] \
  [--upload-size-bytes <upload-size-bytes>] \
  [--upload-type <upload-type>] \
  [--zone <zone>]
```

```console
azmcp compute disk delete \
  --disk-name <disk-name> \
  --resource-group <resource-group>
```

```console
azmcp compute disk get \
  [--disk-name <disk-name>] \
  [--resource-group <resource-group>]
```

```console
azmcp compute disk update \
  --disk-name <disk-name> \
  [--disk-access <disk-access>] \
  [--disk-encryption-set <disk-encryption-set>] \
  [--disk-iops-read-write <disk-iops-read-write>] \
  [--disk-mbps-read-write <disk-mbps-read-write>] \
  [--enable-bursting <enable-bursting>] \
  [--encryption-type <encryption-type>] \
  [--max-shares <max-shares>] \
  [--network-access-policy <network-access-policy>] \
  [--resource-group <resource-group>] \
  [--size-gb <size-gb>] \
  [--sku <sku>] \
  [--tags <tags>] \
  [--tier <tier>]
```


## compute vm

```console
azmcp compute vm create \
  --admin-username <admin-username> \
  --image <image> \
  --location <location> \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  [--admin-password <admin-password>] \
  [--network-security-group <network-security-group>] \
  [--no-public-ip <no-public-ip>] \
  [--os-disk-size-gb <os-disk-size-gb>] \
  [--os-disk-type <os-disk-type>] \
  [--os-type <os-type>] \
  [--public-ip-address <public-ip-address>] \
  [--source-address-prefix <source-address-prefix>] \
  [--ssh-public-key <ssh-public-key>] \
  [--subnet <subnet>] \
  [--virtual-network <virtual-network>] \
  [--vm-size <vm-size>] \
  [--zone <zone>]
```

```console
azmcp compute vm delete \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  [--force-deletion <force-deletion>]
```

```console
azmcp compute vm get \
  [--instance-view <instance-view>] \
  [--resource-group <resource-group>] \
  [--vm-name <vm-name>]
```

```console
azmcp compute vm power-state \
  --power-action <power-action> \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  [--no-wait <no-wait>] \
  [--skip-shutdown <skip-shutdown>]
```

```console
azmcp compute vm update \
  --resource-group <resource-group> \
  --vm-name <vm-name> \
  [--boot-diagnostics <boot-diagnostics>] \
  [--license-type <license-type>] \
  [--tags <tags>] \
  [--user-data <user-data>] \
  [--vm-size <vm-size>]
```


## compute vmss

```console
azmcp compute vmss create \
  --admin-username <admin-username> \
  --image <image> \
  --location <location> \
  --resource-group <resource-group> \
  --vmss-name <vmss-name> \
  [--admin-password <admin-password>] \
  [--instance-count <instance-count>] \
  [--os-disk-size-gb <os-disk-size-gb>] \
  [--os-disk-type <os-disk-type>] \
  [--os-type <os-type>] \
  [--ssh-public-key <ssh-public-key>] \
  [--subnet <subnet>] \
  [--upgrade-policy <upgrade-policy>] \
  [--virtual-network <virtual-network>] \
  [--vm-size <vm-size>] \
  [--zone <zone>]
```

```console
azmcp compute vmss delete \
  --resource-group <resource-group> \
  --vmss-name <vmss-name> \
  [--force-deletion <force-deletion>]
```

```console
azmcp compute vmss get \
  [--instance-id <instance-id>] \
  [--resource-group <resource-group>] \
  [--vmss-name <vmss-name>]
```

```console
azmcp compute vmss update \
  --resource-group <resource-group> \
  --vmss-name <vmss-name> \
  [--capacity <capacity>] \
  [--enable-auto-os-upgrade <enable-auto-os-upgrade>] \
  [--overprovision <overprovision>] \
  [--scale-in-policy <scale-in-policy>] \
  [--tags <tags>] \
  [--upgrade-policy <upgrade-policy>] \
  [--vm-size <vm-size>]
```


## confidentialledger entries

```console
azmcp confidentialledger entries append \
  --content <content> \
  --ledger <ledger> \
  [--collection-id <collection-id>]
```

```console
azmcp confidentialledger entries get \
  --ledger <ledger> \
  --transaction-id <transaction-id> \
  [--collection-id <collection-id>]
```


## containerapps

```console
azmcp containerapps list \
  [--resource-group <resource-group>]
```


## cosmos database container item

```console
azmcp cosmos database container item query \
  --account <account> \
  --container <container> \
  --database <database> \
  [--query <query>]
```


## cosmos

```console
azmcp cosmos list \
  [--account <account>] \
  [--database <database>]
```


## datadog monitoredresources

```console
azmcp datadog monitoredresources list \
  --datadog-resource <datadog-resource> \
  --resource-group <resource-group>
```


## deploy app logs

```console
azmcp deploy app logs get \
  --azd-env-name <azd-env-name> \
  --workspace-folder <workspace-folder> \
  [--limit <limit>]
```


## deploy architecture diagram

```console
azmcp deploy architecture diagram generate \
  --raw-mcp-tool-input <raw-mcp-tool-input>
```


## deploy iac rules

```console
azmcp deploy iac rules get \
  --deployment-tool <deployment-tool> \
  [--iac-type <iac-type>] \
  [--resource-types <resource-types>]
```


## deploy pipeline guidance

```console
azmcp deploy pipeline guidance get \
  [--deploy-option <deploy-option>] \
  [--is-azd-project <is-azd-project>] \
  [--pipeline-platform <pipeline-platform>]
```


## deploy plan

```console
azmcp deploy plan get \
  --project-name <project-name> \
  --workspace-folder <workspace-folder> \
  [--deploy-option <deploy-option>] \
  [--iac-options <iac-options>] \
  [--provisioning-tool <provisioning-tool>] \
  [--resource-group <resource-group>] \
  [--source-type <source-type>] \
  [--target-app-service <target-app-service>]
```


## deviceregistry namespace

```console
azmcp deviceregistry namespace list \
  [--resource-group <resource-group>]
```


## eventgrid events

```console
azmcp eventgrid events publish \
  --data <data> \
  --topic <topic> \
  [--resource-group <resource-group>] \
  [--schema <schema>]
```


## eventgrid subscription

```console
azmcp eventgrid subscription list \
  [--location <location>] \
  [--resource-group <resource-group>] \
  [--topic <topic>]
```


## eventgrid topic

```console
azmcp eventgrid topic list \
  [--resource-group <resource-group>]
```


## eventhubs eventhub consumergroup

```console
azmcp eventhubs eventhub consumergroup delete \
  --consumer-group <consumer-group> \
  --eventhub <eventhub> \
  --namespace <namespace> \
  --resource-group <resource-group>
```

```console
azmcp eventhubs eventhub consumergroup get \
  --eventhub <eventhub> \
  --namespace <namespace> \
  --resource-group <resource-group> \
  [--consumer-group <consumer-group>]
```

```console
azmcp eventhubs eventhub consumergroup update \
  --consumer-group <consumer-group> \
  --eventhub <eventhub> \
  --namespace <namespace> \
  --resource-group <resource-group> \
  [--user-metadata <user-metadata>]
```


## eventhubs eventhub

```console
azmcp eventhubs eventhub delete \
  --eventhub <eventhub> \
  --namespace <namespace> \
  --resource-group <resource-group>
```

```console
azmcp eventhubs eventhub get \
  --namespace <namespace> \
  --resource-group <resource-group> \
  [--eventhub <eventhub>]
```

```console
azmcp eventhubs eventhub update \
  --eventhub <eventhub> \
  --namespace <namespace> \
  --resource-group <resource-group> \
  [--message-retention-in-hours <message-retention-in-hours>] \
  [--partition-count <partition-count>] \
  [--status <status>]
```


## eventhubs namespace

```console
azmcp eventhubs namespace delete \
  --namespace <namespace> \
  --resource-group <resource-group>
```

```console
azmcp eventhubs namespace get \
  [--namespace <namespace>] \
  [--resource-group <resource-group>]
```

```console
azmcp eventhubs namespace update \
  --namespace <namespace> \
  --resource-group <resource-group> \
  [--is-auto-inflate-enabled <is-auto-inflate-enabled>] \
  [--kafka-enabled <kafka-enabled>] \
  [--location <location>] \
  [--maximum-throughput-units <maximum-throughput-units>] \
  [--sku-capacity <sku-capacity>] \
  [--sku-name <sku-name>] \
  [--sku-tier <sku-tier>] \
  [--tags <tags>] \
  [--zone-redundant <zone-redundant>]
```


## extension

```console
azmcp extension azqr \
  [--resource-group <resource-group>]
```


## extension cli

```console
azmcp extension cli generate \
  --cli-type <cli-type> \
  --intent <intent>
```

```console
azmcp extension cli install \
  --cli-type <cli-type>
```


## fileshares fileshare

```console
azmcp fileshares fileshare check-name-availability \
  --location <location> \
  --name <name>
```

```console
azmcp fileshares fileshare create \
  --location <location> \
  --name <name> \
  --resource-group <resource-group> \
  [--allowed-subnets <allowed-subnets>] \
  [--media-tier <media-tier>] \
  [--mount-name <mount-name>] \
  [--nfs-encryption-in-transit <nfs-encryption-in-transit>] \
  [--nfs-root-squash <nfs-root-squash>] \
  [--protocol <protocol>] \
  [--provisioned-io-per-sec <provisioned-io-per-sec>] \
  [--provisioned-storage-in-gib <provisioned-storage-in-gib>] \
  [--provisioned-throughput-mib-per-sec <provisioned-throughput-mib-per-sec>] \
  [--public-network-access <public-network-access>] \
  [--redundancy <redundancy>] \
  [--tags <tags>]
```

```console
azmcp fileshares fileshare delete \
  --name <name> \
  --resource-group <resource-group>
```

```console
azmcp fileshares fileshare get \
  [--name <name>] \
  [--resource-group <resource-group>]
```


## fileshares fileshare peconnection

```console
azmcp fileshares fileshare peconnection get \
  --file-share-name <file-share-name> \
  --resource-group <resource-group> \
  [--connection-name <connection-name>]
```

```console
azmcp fileshares fileshare peconnection update \
  --connection-name <connection-name> \
  --file-share-name <file-share-name> \
  --resource-group <resource-group> \
  --status <status> \
  [--description <description>]
```


## fileshares fileshare snapshot

```console
azmcp fileshares fileshare snapshot create \
  --file-share-name <file-share-name> \
  --resource-group <resource-group> \
  --snapshot-name <snapshot-name> \
  [--metadata <metadata>]
```

```console
azmcp fileshares fileshare snapshot delete \
  --file-share-name <file-share-name> \
  --resource-group <resource-group> \
  --snapshot-name <snapshot-name>
```

```console
azmcp fileshares fileshare snapshot get \
  --file-share-name <file-share-name> \
  --resource-group <resource-group> \
  [--snapshot-name <snapshot-name>]
```

```console
azmcp fileshares fileshare snapshot update \
  --file-share-name <file-share-name> \
  --resource-group <resource-group> \
  --snapshot-name <snapshot-name> \
  [--metadata <metadata>]
```


## fileshares fileshare

```console
azmcp fileshares fileshare update \
  --name <name> \
  --resource-group <resource-group> \
  [--allowed-subnets <allowed-subnets>] \
  [--nfs-encryption-in-transit <nfs-encryption-in-transit>] \
  [--nfs-root-squash <nfs-root-squash>] \
  [--provisioned-io-per-sec <provisioned-io-per-sec>] \
  [--provisioned-storage-in-gib <provisioned-storage-in-gib>] \
  [--provisioned-throughput-mib-per-sec <provisioned-throughput-mib-per-sec>] \
  [--public-network-access <public-network-access>] \
  [--tags <tags>]
```


## fileshares

```console
azmcp fileshares limits \
  --location <location>
```

```console
azmcp fileshares rec \
  --location <location> \
  --provisioned-storage-in-gib <provisioned-storage-in-gib>
```

```console
azmcp fileshares usage \
  --location <location>
```


## foundryextensions knowledge index

```console
azmcp foundryextensions knowledge index list \
  --endpoint <endpoint>
```

```console
azmcp foundryextensions knowledge index schema \
  --endpoint <endpoint> \
  --index <index>
```


## foundryextensions openai

```console
azmcp foundryextensions openai chat-completions-create \
  --deployment <deployment> \
  --message-array <message-array> \
  --resource-group <resource-group> \
  --resource-name <resource-name> \
  [--frequency-penalty <frequency-penalty>] \
  [--max-tokens <max-tokens>] \
  [--presence-penalty <presence-penalty>] \
  [--seed <seed>] \
  [--stop <stop>] \
  [--stream <stream>] \
  [--temperature <temperature>] \
  [--top-p <top-p>] \
  [--user <user>]
```

```console
azmcp foundryextensions openai create-completion \
  --deployment <deployment> \
  --prompt-text <prompt-text> \
  --resource-group <resource-group> \
  --resource-name <resource-name> \
  [--max-tokens <max-tokens>] \
  [--temperature <temperature>]
```

```console
azmcp foundryextensions openai embeddings-create \
  --deployment <deployment> \
  --input-text <input-text> \
  --resource-group <resource-group> \
  --resource-name <resource-name> \
  [--dimensions <dimensions>] \
  [--encoding-format <encoding-format>] \
  [--user <user>]
```

```console
azmcp foundryextensions openai models-list \
  --resource-group <resource-group> \
  --resource-name <resource-name>
```


## foundryextensions resource

```console
azmcp foundryextensions resource get \
  [--resource-group <resource-group>] \
  [--resource-name <resource-name>]
```


## functionapp

```console
azmcp functionapp get \
  [--function-app <function-app>] \
  [--resource-group <resource-group>]
```


## functions language

```console
azmcp functions language list
```


## functions project

```console
azmcp functions project get \
  --language <language>
```


## functions template

```console
azmcp functions template get \
  --language <language> \
  [--output <output>] \
  [--runtime-version <runtime-version>] \
  [--template <template>]
```


## get azure bestpractices ai

```console
azmcp get azure bestpractices ai app
```


## get azure bestpractices

```console
azmcp get azure bestpractices get \
  --action <action> \
  --resource <resource>
```


## grafana

```console
azmcp grafana list \
  [--resource-group <resource-group>]
```


## group

```console
azmcp group list
```


## group resource

```console
azmcp group resource list \
  --resource-group <resource-group>
```


## keyvault admin settings

```console
azmcp keyvault admin settings get \
  --vault <vault>
```


## keyvault certificate

```console
azmcp keyvault certificate create \
  --certificate <certificate> \
  --vault <vault>
```

```console
azmcp keyvault certificate get \
  --vault <vault> \
  [--certificate <certificate>]
```

```console
azmcp keyvault certificate import \
  --certificate <certificate> \
  --certificate-data <certificate-data> \
  --vault <vault> \
  [--password <password>]
```


## keyvault key

```console
azmcp keyvault key create \
  --key <key> \
  --key-type <key-type> \
  --vault <vault>
```

```console
azmcp keyvault key get \
  --vault <vault> \
  [--include-managed <include-managed>] \
  [--key <key>]
```


## keyvault secret

```console
azmcp keyvault secret create \
  --secret <secret> \
  --value <value> \
  --vault <vault>
```

```console
azmcp keyvault secret get \
  --vault <vault> \
  [--secret <secret>]
```


## kusto cluster

```console
azmcp kusto cluster get \
  --cluster <cluster>
```

```console
azmcp kusto cluster list \
  [--resource-group <resource-group>]
```


## kusto database

```console
azmcp kusto database list \
  [--cluster <cluster>] \
  [--cluster-uri <cluster-uri>]
```


## kusto

```console
azmcp kusto query \
  --database <database> \
  --query <query> \
  [--cluster <cluster>] \
  [--cluster-uri <cluster-uri>]
```

```console
azmcp kusto sample \
  --database <database> \
  --limit <limit> \
  --table <table> \
  [--cluster <cluster>] \
  [--cluster-uri <cluster-uri>]
```


## kusto table

```console
azmcp kusto table list \
  --database <database> \
  [--cluster <cluster>] \
  [--cluster-uri <cluster-uri>]
```

```console
azmcp kusto table schema \
  --database <database> \
  --table <table> \
  [--cluster <cluster>] \
  [--cluster-uri <cluster-uri>]
```


## loadtesting test

```console
azmcp loadtesting test create \
  --test-id <test-id> \
  --test-resource-name <test-resource-name> \
  [--description <description>] \
  [--display-name <display-name>] \
  [--duration <duration>] \
  [--endpoint <endpoint>] \
  [--ramp-up-time <ramp-up-time>] \
  [--resource-group <resource-group>] \
  [--virtual-users <virtual-users>]
```

```console
azmcp loadtesting test get \
  --test-id <test-id> \
  --test-resource-name <test-resource-name> \
  [--resource-group <resource-group>]
```


## loadtesting testresource

```console
azmcp loadtesting testresource create \
  --resource-group <resource-group> \
  [--test-resource-name <test-resource-name>]
```

```console
azmcp loadtesting testresource list \
  [--resource-group <resource-group>] \
  [--test-resource-name <test-resource-name>]
```


## loadtesting testrun

```console
azmcp loadtesting testrun createorupdate \
  --test-id <test-id> \
  --test-resource-name <test-resource-name> \
  --testrun-id <testrun-id> \
  [--description <description>] \
  [--display-name <display-name>] \
  [--old-testrun-id <old-testrun-id>] \
  [--resource-group <resource-group>]
```

```console
azmcp loadtesting testrun get \
  --test-resource-name <test-resource-name> \
  [--resource-group <resource-group>] \
  [--test-id <test-id>] \
  [--testrun-id <testrun-id>]
```


## managedlustre fs blob autoexport

```console
azmcp managedlustre fs blob autoexport cancel \
  --filesystem-name <filesystem-name> \
  --job-name <job-name> \
  --resource-group <resource-group>
```

```console
azmcp managedlustre fs blob autoexport create \
  --filesystem-name <filesystem-name> \
  --resource-group <resource-group> \
  [--admin-status <admin-status>] \
  [--autoexport-prefix <autoexport-prefix>] \
  [--job-name <job-name>]
```

```console
azmcp managedlustre fs blob autoexport delete \
  --filesystem-name <filesystem-name> \
  --job-name <job-name> \
  --resource-group <resource-group>
```

```console
azmcp managedlustre fs blob autoexport get \
  --filesystem-name <filesystem-name> \
  --resource-group <resource-group> \
  [--job-name <job-name>]
```


## managedlustre fs blob autoimport

```console
azmcp managedlustre fs blob autoimport cancel \
  --filesystem-name <filesystem-name> \
  --job-name <job-name> \
  --resource-group <resource-group>
```

```console
azmcp managedlustre fs blob autoimport create \
  --filesystem-name <filesystem-name> \
  --resource-group <resource-group> \
  [--admin-status <admin-status>] \
  [--autoimport-prefixes <autoimport-prefixes>] \
  [--conflict-resolution-mode <conflict-resolution-mode>] \
  [--enable-deletions <enable-deletions>] \
  [--job-name <job-name>] \
  [--maximum-errors <maximum-errors>]
```

```console
azmcp managedlustre fs blob autoimport delete \
  --filesystem-name <filesystem-name> \
  --job-name <job-name> \
  --resource-group <resource-group>
```

```console
azmcp managedlustre fs blob autoimport get \
  --filesystem-name <filesystem-name> \
  --resource-group <resource-group> \
  [--job-name <job-name>]
```


## managedlustre fs blob import

```console
azmcp managedlustre fs blob import cancel \
  --filesystem-name <filesystem-name> \
  --job-name <job-name> \
  --resource-group <resource-group>
```

```console
azmcp managedlustre fs blob import create \
  --filesystem-name <filesystem-name> \
  --resource-group <resource-group> \
  [--conflict-resolution-mode <conflict-resolution-mode>] \
  [--import-prefixes <import-prefixes>] \
  [--job-name <job-name>] \
  [--maximum-errors <maximum-errors>]
```

```console
azmcp managedlustre fs blob import delete \
  --filesystem-name <filesystem-name> \
  --job-name <job-name> \
  --resource-group <resource-group>
```

```console
azmcp managedlustre fs blob import get \
  --filesystem-name <filesystem-name> \
  --resource-group <resource-group> \
  [--job-name <job-name>]
```


## managedlustre fs

```console
azmcp managedlustre fs create \
  --location <location> \
  --maintenance-day <maintenance-day> \
  --maintenance-time <maintenance-time> \
  --name <name> \
  --resource-group <resource-group> \
  --size <size> \
  --sku <sku> \
  --subnet-id <subnet-id> \
  --zone <zone> \
  [--custom-encryption <custom-encryption>] \
  [--hsm-container <hsm-container>] \
  [--hsm-log-container <hsm-log-container>] \
  [--import-prefix <import-prefix>] \
  [--key-url <key-url>] \
  [--no-squash-nid-list <no-squash-nid-list>] \
  [--root-squash-mode <root-squash-mode>] \
  [--source-vault <source-vault>] \
  [--squash-gid <squash-gid>] \
  [--squash-uid <squash-uid>] \
  [--user-assigned-identity-id <user-assigned-identity-id>]
```

```console
azmcp managedlustre fs list \
  [--resource-group <resource-group>]
```


## managedlustre fs sku

```console
azmcp managedlustre fs sku get \
  [--location <location>]
```


## managedlustre fs subnetsize

```console
azmcp managedlustre fs subnetsize ask \
  --size <size> \
  --sku <sku>
```

```console
azmcp managedlustre fs subnetsize validate \
  --location <location> \
  --size <size> \
  --sku <sku> \
  --subnet-id <subnet-id>
```


## managedlustre fs

```console
azmcp managedlustre fs update \
  --name <name> \
  --resource-group <resource-group> \
  [--maintenance-day <maintenance-day>] \
  [--maintenance-time <maintenance-time>] \
  [--no-squash-nid-list <no-squash-nid-list>] \
  [--root-squash-mode <root-squash-mode>] \
  [--squash-gid <squash-gid>] \
  [--squash-uid <squash-uid>]
```


## marketplace product

```console
azmcp marketplace product get \
  --product-id <product-id> \
  [--include-service-instruction-templates <include-service-instruction-templates>] \
  [--include-stop-sold-plans <include-stop-sold-plans>] \
  [--language <language>] \
  [--lookup-offer-in-tenant-level <lookup-offer-in-tenant-level>] \
  [--plan-id <plan-id>] \
  [--sku-id <sku-id>]
```

```console
azmcp marketplace product list \
  [--expand <expand>] \
  [--filter <filter>] \
  [--language <language>] \
  [--next-cursor <next-cursor>] \
  [--orderby <orderby>] \
  [--search <search>] \
  [--select <select>]
```


## monitor activitylog

```console
azmcp monitor activitylog list \
  --resource-name <resource-name> \
  [--event-level <event-level>] \
  [--hours <hours>] \
  [--resource-group <resource-group>] \
  [--resource-type <resource-type>] \
  [--top <top>]
```


## monitor healthmodels entity

```console
azmcp monitor healthmodels entity get \
  --entity <entity> \
  --health-model <health-model> \
  --resource-group <resource-group>
```


## monitor instrumentation

```console
azmcp monitor instrumentation get-learning-resource \
  [--path <path>]
```

```console
azmcp monitor instrumentation orchestrator-next \
  --completion-note <completion-note> \
  --session-id <session-id>
```

```console
azmcp monitor instrumentation orchestrator-start \
  --workspace-path <workspace-path>
```

```console
azmcp monitor instrumentation send-brownfield-analysis \
  --findings-json <findings-json> \
  --session-id <session-id>
```

```console
azmcp monitor instrumentation send-enhancement-select \
  --enhancement-keys <enhancement-keys> \
  --session-id <session-id>
```


## monitor metrics

```console
azmcp monitor metrics definitions \
  --resource <resource> \
  [--limit <limit>] \
  [--metric-namespace <metric-namespace>] \
  [--resource-group <resource-group>] \
  [--resource-type <resource-type>] \
  [--search-string <search-string>]
```

```console
azmcp monitor metrics query \
  --metric-names <metric-names> \
  --metric-namespace <metric-namespace> \
  --resource <resource> \
  [--aggregation <aggregation>] \
  [--end-time <end-time>] \
  [--filter <filter>] \
  [--interval <interval>] \
  [--max-buckets <max-buckets>] \
  [--resource-group <resource-group>] \
  [--resource-type <resource-type>] \
  [--start-time <start-time>]
```


## monitor resource log

```console
azmcp monitor resource log query \
  --query <query> \
  --resource-id <resource-id> \
  --table <table> \
  [--hours <hours>] \
  [--limit <limit>]
```


## monitor table

```console
azmcp monitor table list \
  --resource-group <resource-group> \
  --table-type <table-type> \
  --workspace <workspace>
```


## monitor table type

```console
azmcp monitor table type list \
  --resource-group <resource-group> \
  --workspace <workspace>
```


## monitor webtests

```console
azmcp monitor webtests createorupdate \
  --resource-group <resource-group> \
  --webtest-resource <webtest-resource> \
  [--appinsights-component <appinsights-component>] \
  [--description <description>] \
  [--enabled <enabled>] \
  [--expected-status-code <expected-status-code>] \
  [--follow-redirects <follow-redirects>] \
  [--frequency <frequency>] \
  [--headers <headers>] \
  [--http-verb <http-verb>] \
  [--ignore-status-code <ignore-status-code>] \
  [--location <location>] \
  [--parse-requests <parse-requests>] \
  [--request-body <request-body>] \
  [--request-url <request-url>] \
  [--retry-enabled <retry-enabled>] \
  [--ssl-check <ssl-check>] \
  [--ssl-lifetime-check <ssl-lifetime-check>] \
  [--timeout <timeout>] \
  [--webtest <webtest>] \
  [--webtest-locations <webtest-locations>]
```

```console
azmcp monitor webtests get \
  [--resource-group <resource-group>] \
  [--webtest-resource <webtest-resource>]
```


## monitor workspace

```console
azmcp monitor workspace list \
  [--resource-group <resource-group>]
```


## monitor workspace log

```console
azmcp monitor workspace log query \
  --query <query> \
  --resource-group <resource-group> \
  --table <table> \
  --workspace <workspace> \
  [--hours <hours>] \
  [--limit <limit>]
```


## mysql database

```console
azmcp mysql database query \
  --database <database> \
  --query <query> \
  --resource-group <resource-group> \
  --server <server> \
  --user <user>
```


## mysql

```console
azmcp mysql list \
  --resource-group <resource-group> \
  --user <user> \
  [--database <database>] \
  [--server <server>]
```


## mysql server config

```console
azmcp mysql server config get \
  --resource-group <resource-group> \
  --server <server> \
  --user <user>
```


## mysql server param

```console
azmcp mysql server param get \
  --param <param> \
  --resource-group <resource-group> \
  --server <server> \
  --user <user>
```

```console
azmcp mysql server param set \
  --param <param> \
  --resource-group <resource-group> \
  --server <server> \
  --user <user> \
  --value <value>
```


## mysql table schema

```console
azmcp mysql table schema get \
  --database <database> \
  --resource-group <resource-group> \
  --server <server> \
  --table <table> \
  --user <user>
```


## policy assignment

```console
azmcp policy assignment list \
  [--scope <scope>]
```


## postgres database

```console
azmcp postgres database query \
  --database <database> \
  --query <query> \
  --resource-group <resource-group> \
  --server <server> \
  --user <user> \
  [--auth-type <auth-type>] \
  [--password <password>]
```


## postgres

```console
azmcp postgres list \
  [--auth-type <auth-type>] \
  [--database <database>] \
  [--password <password>] \
  [--resource-group <resource-group>] \
  [--server <server>] \
  [--user <user>]
```


## postgres server config

```console
azmcp postgres server config get \
  --resource-group <resource-group> \
  --server <server> \
  --user <user>
```


## postgres server param

```console
azmcp postgres server param get \
  --param <param> \
  --resource-group <resource-group> \
  --server <server> \
  --user <user>
```

```console
azmcp postgres server param set \
  --param <param> \
  --resource-group <resource-group> \
  --server <server> \
  --user <user> \
  --value <value>
```


## postgres table schema

```console
azmcp postgres table schema get \
  --database <database> \
  --resource-group <resource-group> \
  --server <server> \
  --table <table> \
  --user <user> \
  [--auth-type <auth-type>] \
  [--password <password>]
```


## pricing

```console
azmcp pricing get \
  [--currency <currency>] \
  [--filter <filter>] \
  [--include-savings-plan <include-savings-plan>] \
  [--price-type <price-type>] \
  [--region <region>] \
  [--service <service>] \
  [--service-family <service-family>] \
  [--sku <sku>]
```


## quota region availability

```console
azmcp quota region availability list \
  --resource-types <resource-types> \
  [--cognitive-service-deployment-sku-name <cognitive-service-deployment-sku-name>] \
  [--cognitive-service-model-name <cognitive-service-model-name>] \
  [--cognitive-service-model-version <cognitive-service-model-version>]
```


## quota usage

```console
azmcp quota usage check \
  --region <region> \
  --resource-types <resource-types>
```


## redis

```console
azmcp redis create \
  --location <location> \
  --resource <resource> \
  --resource-group <resource-group> \
  [--access-keys-authentication <access-keys-authentication>] \
  [--modules <modules>] \
  [--public-network-access <public-network-access>] \
  [--sku <sku>]
```

```console
azmcp redis list
```


## resourcehealth availability-status

```console
azmcp resourcehealth availability-status get \
  [--resource-group <resource-group>] \
  [--resourceId <resourceId>]
```


## resourcehealth health-events

```console
azmcp resourcehealth health-events list \
  [--event-type <event-type>] \
  [--filter <filter>] \
  [--query-end-time <query-end-time>] \
  [--query-start-time <query-start-time>] \
  [--status <status>] \
  [--tracking-id <tracking-id>]
```


## role assignment

```console
azmcp role assignment list \
  --scope <scope>
```


## search index

```console
azmcp search index get \
  --service <service> \
  [--index <index>]
```

```console
azmcp search index query \
  --index <index> \
  --query <query> \
  --service <service>
```


## search knowledge base

```console
azmcp search knowledge base get \
  --service <service> \
  [--knowledge-base <knowledge-base>]
```

```console
azmcp search knowledge base retrieve \
  --knowledge-base <knowledge-base> \
  --service <service> \
  [--messages <messages>] \
  [--query <query>]
```


## search knowledge source

```console
azmcp search knowledge source get \
  --service <service> \
  [--knowledge-source <knowledge-source>]
```


## search service

```console
azmcp search service list \
  [--resource-group <resource-group>]
```


## servicebus queue

```console
azmcp servicebus queue details \
  --namespace <namespace> \
  --queue <queue>
```


## servicebus topic

```console
azmcp servicebus topic details \
  --namespace <namespace> \
  --topic <topic>
```


## servicebus topic subscription

```console
azmcp servicebus topic subscription details \
  --namespace <namespace> \
  --subscription-name <subscription-name> \
  --topic <topic>
```


## servicefabric managedcluster node

```console
azmcp servicefabric managedcluster node get \
  --cluster <cluster> \
  --resource-group <resource-group> \
  [--node <node>]
```


## servicefabric managedcluster nodetype

```console
azmcp servicefabric managedcluster nodetype restart \
  --cluster <cluster> \
  --node-type <node-type> \
  --nodes <nodes> \
  --resource-group <resource-group> \
  [--update-type <update-type>]
```


## signalr runtime

```console
azmcp signalr runtime get \
  [--resource-group <resource-group>] \
  [--signalr <signalr>]
```


## speech stt

```console
azmcp speech stt recognize \
  --endpoint <endpoint> \
  --file <file> \
  [--format <format>] \
  [--language <language>] \
  [--phrases <phrases>] \
  [--profanity <profanity>]
```


## speech tts

```console
azmcp speech tts synthesize \
  --endpoint <endpoint> \
  --outputAudio <outputAudio> \
  --text <text> \
  [--endpointId <endpointId>] \
  [--format <format>] \
  [--language <language>] \
  [--voice <voice>]
```


## sql db

```console
azmcp sql db create \
  --database <database> \
  --resource-group <resource-group> \
  --server <server> \
  [--collation <collation>] \
  [--elastic-pool-name <elastic-pool-name>] \
  [--max-size-bytes <max-size-bytes>] \
  [--read-scale <read-scale>] \
  [--sku-capacity <sku-capacity>] \
  [--sku-name <sku-name>] \
  [--sku-tier <sku-tier>] \
  [--zone-redundant <zone-redundant>]
```

```console
azmcp sql db delete \
  --database <database> \
  --resource-group <resource-group> \
  --server <server>
```

```console
azmcp sql db get \
  --resource-group <resource-group> \
  --server <server> \
  [--database <database>]
```

```console
azmcp sql db rename \
  --database <database> \
  --new-database-name <new-database-name> \
  --resource-group <resource-group> \
  --server <server>
```

```console
azmcp sql db update \
  --database <database> \
  --resource-group <resource-group> \
  --server <server> \
  [--collation <collation>] \
  [--elastic-pool-name <elastic-pool-name>] \
  [--max-size-bytes <max-size-bytes>] \
  [--read-scale <read-scale>] \
  [--sku-capacity <sku-capacity>] \
  [--sku-name <sku-name>] \
  [--sku-tier <sku-tier>] \
  [--zone-redundant <zone-redundant>]
```


## sql elastic-pool

```console
azmcp sql elastic-pool list \
  --resource-group <resource-group> \
  --server <server>
```


## sql server

```console
azmcp sql server create \
  --administrator-login <administrator-login> \
  --administrator-password <administrator-password> \
  --location <location> \
  --resource-group <resource-group> \
  --server <server> \
  [--public-network-access <public-network-access>] \
  [--version <version>]
```

```console
azmcp sql server delete \
  --resource-group <resource-group> \
  --server <server> \
  [--force <force>]
```


## sql server entra-admin

```console
azmcp sql server entra-admin list \
  --resource-group <resource-group> \
  --server <server>
```


## sql server firewall-rule

```console
azmcp sql server firewall-rule create \
  --end-ip-address <end-ip-address> \
  --firewall-rule-name <firewall-rule-name> \
  --resource-group <resource-group> \
  --server <server> \
  --start-ip-address <start-ip-address>
```

```console
azmcp sql server firewall-rule delete \
  --firewall-rule-name <firewall-rule-name> \
  --resource-group <resource-group> \
  --server <server>
```

```console
azmcp sql server firewall-rule list \
  --resource-group <resource-group> \
  --server <server>
```


## sql server

```console
azmcp sql server get \
  --resource-group <resource-group> \
  [--server <server>]
```


## storage account

```console
azmcp storage account create \
  --account <account> \
  --location <location> \
  --resource-group <resource-group> \
  [--access-tier <access-tier>] \
  [--enable-hierarchical-namespace <enable-hierarchical-namespace>] \
  [--sku <sku>]
```

```console
azmcp storage account get \
  [--account <account>] \
  [--resource-group <resource-group>]
```


## storage blob container

```console
azmcp storage blob container create \
  --account <account> \
  --container <container>
```

```console
azmcp storage blob container get \
  --account <account> \
  [--container <container>] \
  [--prefix <prefix>]
```


## storage blob

```console
azmcp storage blob get \
  --account <account> \
  --container <container> \
  [--blob <blob>] \
  [--prefix <prefix>]
```

```console
azmcp storage blob upload \
  --account <account> \
  --blob <blob> \
  --container <container> \
  --local-file-path <local-file-path>
```


## storage table

```console
azmcp storage table list \
  --account <account>
```


## storagesync cloudendpoint

```console
azmcp storagesync cloudendpoint changedetection \
  --cloud-endpoint-name <cloud-endpoint-name> \
  --directory-path <directory-path> \
  --name <name> \
  --resource-group <resource-group> \
  --sync-group-name <sync-group-name> \
  [--change-detection-mode <change-detection-mode>] \
  [--paths <paths>]
```

```console
azmcp storagesync cloudendpoint create \
  --azure-file-share-name <azure-file-share-name> \
  --cloud-endpoint-name <cloud-endpoint-name> \
  --name <name> \
  --resource-group <resource-group> \
  --storage-account-resource-id <storage-account-resource-id> \
  --sync-group-name <sync-group-name>
```

```console
azmcp storagesync cloudendpoint delete \
  --cloud-endpoint-name <cloud-endpoint-name> \
  --name <name> \
  --resource-group <resource-group> \
  --sync-group-name <sync-group-name>
```

```console
azmcp storagesync cloudendpoint get \
  --name <name> \
  --resource-group <resource-group> \
  --sync-group-name <sync-group-name> \
  [--cloud-endpoint-name <cloud-endpoint-name>]
```


## storagesync registeredserver

```console
azmcp storagesync registeredserver get \
  --name <name> \
  --resource-group <resource-group> \
  [--server-id <server-id>]
```

```console
azmcp storagesync registeredserver unregister \
  --name <name> \
  --resource-group <resource-group> \
  --server-id <server-id>
```

```console
azmcp storagesync registeredserver update \
  --name <name> \
  --resource-group <resource-group> \
  --server-id <server-id>
```


## storagesync serverendpoint

```console
azmcp storagesync serverendpoint create \
  --name <name> \
  --resource-group <resource-group> \
  --server-endpoint-name <server-endpoint-name> \
  --server-local-path <server-local-path> \
  --server-resource-id <server-resource-id> \
  --sync-group-name <sync-group-name> \
  [--cloud-tiering <cloud-tiering>] \
  [--local-cache-mode <local-cache-mode>] \
  [--tier-files-older-than-days <tier-files-older-than-days>] \
  [--volume-free-space-percent <volume-free-space-percent>]
```

```console
azmcp storagesync serverendpoint delete \
  --name <name> \
  --resource-group <resource-group> \
  --server-endpoint-name <server-endpoint-name> \
  --sync-group-name <sync-group-name>
```

```console
azmcp storagesync serverendpoint get \
  --name <name> \
  --resource-group <resource-group> \
  --sync-group-name <sync-group-name> \
  [--server-endpoint-name <server-endpoint-name>]
```

```console
azmcp storagesync serverendpoint update \
  --name <name> \
  --resource-group <resource-group> \
  --server-endpoint-name <server-endpoint-name> \
  --sync-group-name <sync-group-name> \
  [--cloud-tiering <cloud-tiering>] \
  [--local-cache-mode <local-cache-mode>] \
  [--tier-files-older-than-days <tier-files-older-than-days>] \
  [--volume-free-space-percent <volume-free-space-percent>]
```


## storagesync service

```console
azmcp storagesync service create \
  --location <location> \
  --name <name> \
  --resource-group <resource-group>
```

```console
azmcp storagesync service delete \
  --name <name> \
  --resource-group <resource-group>
```

```console
azmcp storagesync service get \
  [--name <name>] \
  [--resource-group <resource-group>]
```

```console
azmcp storagesync service update \
  --name <name> \
  --resource-group <resource-group> \
  [--identity-type <identity-type>] \
  [--incoming-traffic-policy <incoming-traffic-policy>] \
  [--tags <tags>]
```


## storagesync syncgroup

```console
azmcp storagesync syncgroup create \
  --name <name> \
  --resource-group <resource-group> \
  --sync-group-name <sync-group-name>
```

```console
azmcp storagesync syncgroup delete \
  --name <name> \
  --resource-group <resource-group> \
  --sync-group-name <sync-group-name>
```

```console
azmcp storagesync syncgroup get \
  --name <name> \
  --resource-group <resource-group> \
  [--sync-group-name <sync-group-name>]
```


## subscription

```console
azmcp subscription list
```


## virtualdesktop hostpool host

```console
azmcp virtualdesktop hostpool host list \
  [--hostpool <hostpool>] \
  [--hostpool-resource-id <hostpool-resource-id>] \
  [--resource-group <resource-group>]
```

```console
azmcp virtualdesktop hostpool host user-list \
  --sessionhost <sessionhost> \
  [--hostpool <hostpool>] \
  [--hostpool-resource-id <hostpool-resource-id>] \
  [--resource-group <resource-group>]
```


## virtualdesktop hostpool

```console
azmcp virtualdesktop hostpool list \
  [--resource-group <resource-group>]
```


## wellarchitectedframework serviceguide

```console
azmcp wellarchitectedframework serviceguide get \
  [--service <service>]
```


## workbooks

```console
azmcp workbooks create \
  --display-name <display-name> \
  --resource-group <resource-group> \
  --serialized-content <serialized-content> \
  [--source-id <source-id>]
```

```console
azmcp workbooks delete \
  --workbook-ids <workbook-ids>
```

```console
azmcp workbooks list \
  [--category <category>] \
  [--include-total-count <include-total-count>] \
  [--kind <kind>] \
  [--max-results <max-results>] \
  [--modified-after <modified-after>] \
  [--name-contains <name-contains>] \
  [--output-format <output-format>] \
  [--resource-group <resource-group>] \
  [--source-id <source-id>]
```

```console
azmcp workbooks show \
  --workbook-ids <workbook-ids>
```

```console
azmcp workbooks update \
  --workbook-id <workbook-id> \
  [--display-name <display-name>] \
  [--serialized-content <serialized-content>]
```
