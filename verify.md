🔍 Parsing cli-output.json...
✅ Found 231 tools

🔎 Verifying generated files...

================================================================================
  AZURE MCP DOCUMENTATION GENERATION VERIFICATION REPORT
================================================================================

📊 Total Tools: 231

┌──────────────────────────────────────────────────────────────────────────────┐
│ 📝 Annotations      │ ████████████████████████████████████████ │    231/231  100.0% ✅ │
│ ⚙️ Parameters       │ ███████████████████████████████████████░ │    225/231   97.4% ⚠️  │
│ 💡 Example Prompts  │ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │      0/231    0.0% ❌ │
│ 📋 Param+Annotation │ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │      0/231    0.0% ❌ │
│ ✅ Complete Tools    │ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │      0/231    0.0% ❌ │
└──────────────────────────────────────────────────────────────────────────────┘

📈 SUMMARY:
────────────────────────────────────────────────────────────────────────────────
  Example Prompts     : ❌ Missing 231
  Param+Annotation    : ❌ Missing 231
  Complete Tools      : ❌ Missing 231
  Parameters          : ❌ Missing 6
  Annotations         : ✅ COMPLETE

🔍 ANALYSIS:
────────────────────────────────────────────────────────────────────────────────

📊 BREAKDOWN BY MISSING TYPE:

  ⚙️ Parameters - Missing 6:
     advisor (1): advisor recommendation list
     azuremigrate (2): azuremigrate platformlandingzone getguidance, azuremigrate platformlandingzone request
     compute (2): compute vm get, compute vmss get
     pricing (1): pricing get

  💡 Example Prompts - Missing 231:
     acr (2): acr registry list, acr registry repository list
     advisor (1): advisor recommendation list
     aks (2): aks cluster get, aks nodepool get
     appconfig (5): appconfig account list, appconfig kv delete, appconfig kv get, appconfig kv lock set, appconfig kv set
     applens (1): applens resource diagnose
     applicationinsights (1): applicationinsights recommendation list
     appservice (1): appservice database add
     azuremigrate (2): azuremigrate platformlandingzone getguidance, azuremigrate platformlandingzone request
     azureterraformbestpractices (1): azureterraformbestpractices get
     bicepschema (1): bicepschema get
     cloudarchitect (1): cloudarchitect design
     communication (2): communication email send, communication sms send
     compute (2): compute vm get, compute vmss get
     confidentialledger (2): confidentialledger entries append, confidentialledger entries get
     cosmos (4): cosmos account list, cosmos database container item query, cosmos database container list, cosmos database list
     datadog (1): datadog monitoredresources list
     deploy (5): deploy app logs get, deploy architecture diagram generate, deploy iac rules get, deploy pipeline guidance get, deploy plan get
     eventgrid (3): eventgrid events publish, eventgrid subscription list, eventgrid topic list
     eventhubs (9): eventhubs eventhub consumergroup delete, eventhubs eventhub consumergroup get, eventhubs eventhub consumergroup update, eventhubs eventhub delete, eventhubs eventhub get, eventhubs eventhub update, eventhubs namespace delete, eventhubs namespace get, eventhubs namespace update
     extension (3): extension azqr, extension cli generate, extension cli install
     fileshares (12): fileshares fileshare check-name-availability, fileshares fileshare create, fileshares fileshare delete, fileshares fileshare get, fileshares fileshare snapshot create, fileshares fileshare snapshot delete, fileshares fileshare snapshot get, fileshares fileshare snapshot update, fileshares fileshare update, fileshares limits, fileshares rec, fileshares usage
     foundry (19): foundry agents connect, foundry agents create, foundry agents evaluate, foundry agents get-sdk-sample, foundry agents list, foundry agents query-and-evaluate, foundry knowledge index list, foundry knowledge index schema, foundry models deploy, foundry models deployments list, foundry models list, foundry openai chat-completions-create, foundry openai create-completion, foundry openai embeddings-create, foundry openai models-list, foundry resource get, foundry threads create, foundry threads get-messages, foundry threads list
     functionapp (1): functionapp get
     get (2): get azure bestpractices ai app, get azure bestpractices get
     grafana (1): grafana list
     group (1): group list
     keyvault (11): keyvault admin settings get, keyvault certificate create, keyvault certificate get, keyvault certificate import, keyvault certificate list, keyvault key create, keyvault key get, keyvault key list, keyvault secret create, keyvault secret get, keyvault secret list
     kusto (7): kusto cluster get, kusto cluster list, kusto database list, kusto query, kusto sample, kusto table list, kusto table schema
     loadtesting (8): loadtesting test create, loadtesting test get, loadtesting testresource create, loadtesting testresource list, loadtesting testrun create, loadtesting testrun get, loadtesting testrun list, loadtesting testrun update
     managedlustre (18): managedlustre fs blob autoexport cancel, managedlustre fs blob autoexport create, managedlustre fs blob autoexport delete, managedlustre fs blob autoexport get, managedlustre fs blob autoimport cancel, managedlustre fs blob autoimport create, managedlustre fs blob autoimport delete, managedlustre fs blob autoimport get, managedlustre fs blob import cancel, managedlustre fs blob import create, managedlustre fs blob import delete, managedlustre fs blob import get, managedlustre fs create, managedlustre fs list, managedlustre fs sku get, managedlustre fs subnetsize ask, managedlustre fs subnetsize validate, managedlustre fs update
     marketplace (2): marketplace product get, marketplace product list
     monitor (13): monitor activitylog list, monitor healthmodels entity get, monitor metrics definitions, monitor metrics query, monitor resource log query, monitor table list, monitor table type list, monitor webtests create, monitor webtests get, monitor webtests list, monitor webtests update, monitor workspace list, monitor workspace log query
     mysql (8): mysql database list, mysql database query, mysql server config get, mysql server list, mysql server param get, mysql server param set, mysql table list, mysql table schema get
     policy (1): policy assignment list
     postgres (8): postgres database list, postgres database query, postgres server config get, postgres server list, postgres server param get, postgres server param set, postgres table list, postgres table schema get
     pricing (1): pricing get
     quota (2): quota region availability list, quota usage check
     redis (2): redis create, redis list
     resourcehealth (3): resourcehealth availability-status get, resourcehealth availability-status list, resourcehealth health-events list
     role (1): role assignment list
     search (6): search index get, search index query, search knowledge base get, search knowledge base retrieve, search knowledge source get, search service list
     servicebus (3): servicebus queue details, servicebus topic details, servicebus topic subscription details
     signalr (1): signalr runtime get
     speech (2): speech stt recognize, speech tts synthesize
     sql (15): sql db create, sql db delete, sql db list, sql db rename, sql db show, sql db update, sql elastic-pool list, sql server create, sql server delete, sql server entra-admin list, sql server firewall-rule create, sql server firewall-rule delete, sql server firewall-rule list, sql server list, sql server show
     storage (7): storage account create, storage account get, storage blob container create, storage blob container get, storage blob get, storage blob upload, storage table list
     storagesync (18): storagesync cloudendpoint changedetection, storagesync cloudendpoint create, storagesync cloudendpoint delete, storagesync cloudendpoint get, storagesync registeredserver get, storagesync registeredserver unregister, storagesync registeredserver update, storagesync serverendpoint create, storagesync serverendpoint delete, storagesync serverendpoint get, storagesync serverendpoint update, storagesync service create, storagesync service delete, storagesync service get, storagesync service update, storagesync syncgroup create, storagesync syncgroup delete, storagesync syncgroup get
     subscription (1): subscription list
     virtualdesktop (3): virtualdesktop hostpool host list, virtualdesktop hostpool host user-list, virtualdesktop hostpool list
     workbooks (5): workbooks create, workbooks delete, workbooks list, workbooks show, workbooks update

  📋 Param+Annotation - Missing 231:
     acr (2): acr registry list, acr registry repository list
     advisor (1): advisor recommendation list
     aks (2): aks cluster get, aks nodepool get
     appconfig (5): appconfig account list, appconfig kv delete, appconfig kv get, appconfig kv lock set, appconfig kv set
     applens (1): applens resource diagnose
     applicationinsights (1): applicationinsights recommendation list
     appservice (1): appservice database add
     azuremigrate (2): azuremigrate platformlandingzone getguidance, azuremigrate platformlandingzone request
     azureterraformbestpractices (1): azureterraformbestpractices get
     bicepschema (1): bicepschema get
     cloudarchitect (1): cloudarchitect design
     communication (2): communication email send, communication sms send
     compute (2): compute vm get, compute vmss get
     confidentialledger (2): confidentialledger entries append, confidentialledger entries get
     cosmos (4): cosmos account list, cosmos database container item query, cosmos database container list, cosmos database list
     datadog (1): datadog monitoredresources list
     deploy (5): deploy app logs get, deploy architecture diagram generate, deploy iac rules get, deploy pipeline guidance get, deploy plan get
     eventgrid (3): eventgrid events publish, eventgrid subscription list, eventgrid topic list
     eventhubs (9): eventhubs eventhub consumergroup delete, eventhubs eventhub consumergroup get, eventhubs eventhub consumergroup update, eventhubs eventhub delete, eventhubs eventhub get, eventhubs eventhub update, eventhubs namespace delete, eventhubs namespace get, eventhubs namespace update
     extension (3): extension azqr, extension cli generate, extension cli install
     fileshares (12): fileshares fileshare check-name-availability, fileshares fileshare create, fileshares fileshare delete, fileshares fileshare get, fileshares fileshare snapshot create, fileshares fileshare snapshot delete, fileshares fileshare snapshot get, fileshares fileshare snapshot update, fileshares fileshare update, fileshares limits, fileshares rec, fileshares usage
     foundry (19): foundry agents connect, foundry agents create, foundry agents evaluate, foundry agents get-sdk-sample, foundry agents list, foundry agents query-and-evaluate, foundry knowledge index list, foundry knowledge index schema, foundry models deploy, foundry models deployments list, foundry models list, foundry openai chat-completions-create, foundry openai create-completion, foundry openai embeddings-create, foundry openai models-list, foundry resource get, foundry threads create, foundry threads get-messages, foundry threads list
     functionapp (1): functionapp get
     get (2): get azure bestpractices ai app, get azure bestpractices get
     grafana (1): grafana list
     group (1): group list
     keyvault (11): keyvault admin settings get, keyvault certificate create, keyvault certificate get, keyvault certificate import, keyvault certificate list, keyvault key create, keyvault key get, keyvault key list, keyvault secret create, keyvault secret get, keyvault secret list
     kusto (7): kusto cluster get, kusto cluster list, kusto database list, kusto query, kusto sample, kusto table list, kusto table schema
     loadtesting (8): loadtesting test create, loadtesting test get, loadtesting testresource create, loadtesting testresource list, loadtesting testrun create, loadtesting testrun get, loadtesting testrun list, loadtesting testrun update
     managedlustre (18): managedlustre fs blob autoexport cancel, managedlustre fs blob autoexport create, managedlustre fs blob autoexport delete, managedlustre fs blob autoexport get, managedlustre fs blob autoimport cancel, managedlustre fs blob autoimport create, managedlustre fs blob autoimport delete, managedlustre fs blob autoimport get, managedlustre fs blob import cancel, managedlustre fs blob import create, managedlustre fs blob import delete, managedlustre fs blob import get, managedlustre fs create, managedlustre fs list, managedlustre fs sku get, managedlustre fs subnetsize ask, managedlustre fs subnetsize validate, managedlustre fs update
     marketplace (2): marketplace product get, marketplace product list
     monitor (13): monitor activitylog list, monitor healthmodels entity get, monitor metrics definitions, monitor metrics query, monitor resource log query, monitor table list, monitor table type list, monitor webtests create, monitor webtests get, monitor webtests list, monitor webtests update, monitor workspace list, monitor workspace log query
     mysql (8): mysql database list, mysql database query, mysql server config get, mysql server list, mysql server param get, mysql server param set, mysql table list, mysql table schema get
     policy (1): policy assignment list
     postgres (8): postgres database list, postgres database query, postgres server config get, postgres server list, postgres server param get, postgres server param set, postgres table list, postgres table schema get
     pricing (1): pricing get
     quota (2): quota region availability list, quota usage check
     redis (2): redis create, redis list
     resourcehealth (3): resourcehealth availability-status get, resourcehealth availability-status list, resourcehealth health-events list
     role (1): role assignment list
     search (6): search index get, search index query, search knowledge base get, search knowledge base retrieve, search knowledge source get, search service list
     servicebus (3): servicebus queue details, servicebus topic details, servicebus topic subscription details
     signalr (1): signalr runtime get
     speech (2): speech stt recognize, speech tts synthesize
     sql (15): sql db create, sql db delete, sql db list, sql db rename, sql db show, sql db update, sql elastic-pool list, sql server create, sql server delete, sql server entra-admin list, sql server firewall-rule create, sql server firewall-rule delete, sql server firewall-rule list, sql server list, sql server show
     storage (7): storage account create, storage account get, storage blob container create, storage blob container get, storage blob get, storage blob upload, storage table list
     storagesync (18): storagesync cloudendpoint changedetection, storagesync cloudendpoint create, storagesync cloudendpoint delete, storagesync cloudendpoint get, storagesync registeredserver get, storagesync registeredserver unregister, storagesync registeredserver update, storagesync serverendpoint create, storagesync serverendpoint delete, storagesync serverendpoint get, storagesync serverendpoint update, storagesync service create, storagesync service delete, storagesync service get, storagesync service update, storagesync syncgroup create, storagesync syncgroup delete, storagesync syncgroup get
     subscription (1): subscription list
     virtualdesktop (3): virtualdesktop hostpool host list, virtualdesktop hostpool host user-list, virtualdesktop hostpool list
     workbooks (5): workbooks create, workbooks delete, workbooks list, workbooks show, workbooks update

  ✅ Complete Tools - Missing 231:
     acr (2): acr registry list, acr registry repository list
     advisor (1): advisor recommendation list
     aks (2): aks cluster get, aks nodepool get
     appconfig (5): appconfig account list, appconfig kv delete, appconfig kv get, appconfig kv lock set, appconfig kv set
     applens (1): applens resource diagnose
     applicationinsights (1): applicationinsights recommendation list
     appservice (1): appservice database add
     azuremigrate (2): azuremigrate platformlandingzone getguidance, azuremigrate platformlandingzone request
     azureterraformbestpractices (1): azureterraformbestpractices get
     bicepschema (1): bicepschema get
     cloudarchitect (1): cloudarchitect design
     communication (2): communication email send, communication sms send
     compute (2): compute vm get, compute vmss get
     confidentialledger (2): confidentialledger entries append, confidentialledger entries get
     cosmos (4): cosmos account list, cosmos database container item query, cosmos database container list, cosmos database list
     datadog (1): datadog monitoredresources list
     deploy (5): deploy app logs get, deploy architecture diagram generate, deploy iac rules get, deploy pipeline guidance get, deploy plan get
     eventgrid (3): eventgrid events publish, eventgrid subscription list, eventgrid topic list
     eventhubs (9): eventhubs eventhub consumergroup delete, eventhubs eventhub consumergroup get, eventhubs eventhub consumergroup update, eventhubs eventhub delete, eventhubs eventhub get, eventhubs eventhub update, eventhubs namespace delete, eventhubs namespace get, eventhubs namespace update
     extension (3): extension azqr, extension cli generate, extension cli install
     fileshares (12): fileshares fileshare check-name-availability, fileshares fileshare create, fileshares fileshare delete, fileshares fileshare get, fileshares fileshare snapshot create, fileshares fileshare snapshot delete, fileshares fileshare snapshot get, fileshares fileshare snapshot update, fileshares fileshare update, fileshares limits, fileshares rec, fileshares usage
     foundry (19): foundry agents connect, foundry agents create, foundry agents evaluate, foundry agents get-sdk-sample, foundry agents list, foundry agents query-and-evaluate, foundry knowledge index list, foundry knowledge index schema, foundry models deploy, foundry models deployments list, foundry models list, foundry openai chat-completions-create, foundry openai create-completion, foundry openai embeddings-create, foundry openai models-list, foundry resource get, foundry threads create, foundry threads get-messages, foundry threads list
     functionapp (1): functionapp get
     get (2): get azure bestpractices ai app, get azure bestpractices get
     grafana (1): grafana list
     group (1): group list
     keyvault (11): keyvault admin settings get, keyvault certificate create, keyvault certificate get, keyvault certificate import, keyvault certificate list, keyvault key create, keyvault key get, keyvault key list, keyvault secret create, keyvault secret get, keyvault secret list
     kusto (7): kusto cluster get, kusto cluster list, kusto database list, kusto query, kusto sample, kusto table list, kusto table schema
     loadtesting (8): loadtesting test create, loadtesting test get, loadtesting testresource create, loadtesting testresource list, loadtesting testrun create, loadtesting testrun get, loadtesting testrun list, loadtesting testrun update
     managedlustre (18): managedlustre fs blob autoexport cancel, managedlustre fs blob autoexport create, managedlustre fs blob autoexport delete, managedlustre fs blob autoexport get, managedlustre fs blob autoimport cancel, managedlustre fs blob autoimport create, managedlustre fs blob autoimport delete, managedlustre fs blob autoimport get, managedlustre fs blob import cancel, managedlustre fs blob import create, managedlustre fs blob import delete, managedlustre fs blob import get, managedlustre fs create, managedlustre fs list, managedlustre fs sku get, managedlustre fs subnetsize ask, managedlustre fs subnetsize validate, managedlustre fs update
     marketplace (2): marketplace product get, marketplace product list
     monitor (13): monitor activitylog list, monitor healthmodels entity get, monitor metrics definitions, monitor metrics query, monitor resource log query, monitor table list, monitor table type list, monitor webtests create, monitor webtests get, monitor webtests list, monitor webtests update, monitor workspace list, monitor workspace log query
     mysql (8): mysql database list, mysql database query, mysql server config get, mysql server list, mysql server param get, mysql server param set, mysql table list, mysql table schema get
     policy (1): policy assignment list
     postgres (8): postgres database list, postgres database query, postgres server config get, postgres server list, postgres server param get, postgres server param set, postgres table list, postgres table schema get
     pricing (1): pricing get
     quota (2): quota region availability list, quota usage check
     redis (2): redis create, redis list
     resourcehealth (3): resourcehealth availability-status get, resourcehealth availability-status list, resourcehealth health-events list
     role (1): role assignment list
     search (6): search index get, search index query, search knowledge base get, search knowledge base retrieve, search knowledge source get, search service list
     servicebus (3): servicebus queue details, servicebus topic details, servicebus topic subscription details
     signalr (1): signalr runtime get
     speech (2): speech stt recognize, speech tts synthesize
     sql (15): sql db create, sql db delete, sql db list, sql db rename, sql db show, sql db update, sql elastic-pool list, sql server create, sql server delete, sql server entra-admin list, sql server firewall-rule create, sql server firewall-rule delete, sql server firewall-rule list, sql server list, sql server show
     storage (7): storage account create, storage account get, storage blob container create, storage blob container get, storage blob get, storage blob upload, storage table list
     storagesync (18): storagesync cloudendpoint changedetection, storagesync cloudendpoint create, storagesync cloudendpoint delete, storagesync cloudendpoint get, storagesync registeredserver get, storagesync registeredserver unregister, storagesync registeredserver update, storagesync serverendpoint create, storagesync serverendpoint delete, storagesync serverendpoint get, storagesync serverendpoint update, storagesync service create, storagesync service delete, storagesync service get, storagesync service update, storagesync syncgroup create, storagesync syncgroup delete, storagesync syncgroup get
     subscription (1): subscription list
     virtualdesktop (3): virtualdesktop hostpool host list, virtualdesktop hostpool host user-list, virtualdesktop hostpool list
     workbooks (5): workbooks create, workbooks delete, workbooks list, workbooks show, workbooks update

================================================================================


📄 Detailed report written to: ..\generated\missing-tools.md
