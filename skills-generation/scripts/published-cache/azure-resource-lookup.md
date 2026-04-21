Table of contents 
			
			
				
				Exit editor mode
			
		
	
			
				
					
		
			
				
		
			
				
					
				
			
			
		

		
	 
		
			
		
			
				
			
		
		
			
				
			
			Ask Learn
		
		
			
				
			
			Ask Learn
		
	 

			
				
					
						
					
				
				
					
		
			
				
			
			Reading mode
		
	 
		
			
			Table of contents
		
	 
		
			
				
			
			Read in English
		
	
					
		
			
				
			
			Add
		
	 
		
			
				
			
			Add to plan
		
	 
					
		
			
				
			
			Edit
		
	 
		
		Share via
		
					
						
							
						
						Facebook
					

					
						
							
						
						x.com
					

					
						
							
						
						LinkedIn
					
					
						
							
						
						Email
					
			  
	 
		
		
				
					
						
						
						
					
					Copy Markdown
				
		   
				
					
						
					
					Print
				
		  
	
				
			
		
	
			
		
	  
		
		
			
			
				
					
						
						Note
					


					
						Access to this page requires authorization. You can try signing in or changing directories.
					


					
						Access to this page requires authorization. You can try changing directories.
					


				
			
		
	
					Azure skill for resource lookup
					
		
			 
				
					
		
			
				
			
			Feedback
		
	
				
		  
		
	 
		
			
				
					
				
				
					
						Summarize this article for me
					
				
			
			
			
		
	 
		
			## 
				In this article
			

		
	
					List, find, and show Azure resources across subscriptions or resource groups. Handles prompts like "list websites", "list virtual machines", "list my VMs", "show storage accounts", "find container apps", and "what resources do I have".


Skill: azure-resource-lookup | Source code


## What it provides

This skill provides GitHub Copilot with specialized knowledge. List, find, and show Azure resources across subscriptions or resource groups. Handles prompts like "list websites", "list virtual machines", "list my VMs", "show storage accounts", "find container apps", and "what resources do I have".


## Prerequisites


- Azure subscription: Create a free account if you don't have one.

- AI assistant with Azure Skills: GitHub Copilot for Azure, Visual Studio Code with Azure MCP extension, Claude Code, or another compatible MCP client.

- Azure CLI (v2.60.0+): Install and sign in with az login.

- Azure Key Vault: A key vault for secrets and certificate management.

- Azure Storage: A storage account for blob, file, queue, or table data.

- Azure Kubernetes Service: An AKS cluster for container orchestration.

- Azure Cosmos DB: A Cosmos DB account for NoSQL data.


### Related tools




Tool
Command
Purpose




extension_cli_generate
Generate az graph query commands
Primary tool - generate ARG queries from user intent


mcp_azure_mcp_subscription_list
List available subscriptions
Discover subscription scope before querying


mcp_azure_mcp_group_list
List resource groups
Narrow query scope



## When to use this skill

Use this skill when you need to:



- Work with resource inventory

- Find resources by tag

- Work with tag analysis

- Orphaned resource discovery (not for cost analysis)

- Work with unattached disks, count resources by type, and cross-subscription lookup

- And Azure Resource Graph queries


## Example prompts

Try these prompts to activate this skill:



- "List the websites in my subscription"

- "Show me the websites in my resource group"

- "List all virtual machines in my subscription"

- "Show me all VMs in resource group 'my-rg'"

- "List my Azure storage accounts"

- "List all my Azure Container Registries"

- "List the container apps in my subscription"

- "Show me the container apps in my resource group"

- "What resources do I have across all my subscriptions?"

- "Show me all my Azure resources"


## Related content


- Azure Model Context Protocol (MCP) Server overview

- Skill source code



					
		
	 
		
		
	
					
		
		
			
			## Feedback

			
				
					Was this page helpful?
				


				
					
						
							
						
						Yes
					
					
						
							
						
						No
					
					
						
							
								
							
							No
						
						
							
								Need help with this topic?
							


							
								Want to try using Ask Learn to clarify or guide you through this topic?
							


							
		
			
		
			
				
			
		
		
			
				
			
			Ask Learn
		
		
			
				
			
			Ask Learn
		
	
			
				
					
				
				 Suggest a fix? 
			
		
	
						
					
				
			
		
		
	
				
				
		
			
			## 
				Additional resources
			

			 
		
	 
		
	
		
	 
		
			
			
				- 
			
				Last updated on 
		2026-04-17
