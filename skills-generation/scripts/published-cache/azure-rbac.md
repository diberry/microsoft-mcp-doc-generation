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
					


				
			
		
	
					Azure skill for role-based access control (RBAC)
					
		
			 
				
					
		
			
				
			
			Feedback
		
	
				
		  
		
	 
		
			
				
					
				
				
					
						Summarize this article for me
					
				
			
			
			
		
	 
		
			## 
				In this article
			

		
	
					Helps users find the right Azure RBAC role for an identity with least privilege access, then generate CLI commands and Bicep code to assign it. Also provides guidance on permissions required to grant roles.


Skill: azure-rbac | Source code


## What it provides

This skill gives GitHub Copilot expertise in Azure role-based access control so it can guide you through secure role assignments. Specifically, it provides:



- Least-privilege role recommendations: Searches built-in Azure roles to find the minimal role definition that matches the permissions your identity needs.

- Custom role definitions: When no built-in role fits, generates a custom role definition scoped to exactly the permissions you require.

- Role assignment commands: Produces ready-to-run Azure CLI commands and Bicep code snippets for assigning roles to managed identities, service principals, or users.

- Granting permissions guidance: Explains what permissions you need (such as Microsoft.Authorization/roleAssignments/write) to assign roles, and which built-in roles like User Access Administrator or Owner provide them.


## Prerequisites


- Azure subscription: Create a free account if you don't have one.

- AI assistant with Azure Skills: GitHub Copilot for Azure, Visual Studio Code with Azure MCP extension, Claude Code, or another compatible MCP client.

- Azure CLI (v2.60.0+): Install and sign in with az login.

- Azure role: Your account must have Microsoft.Authorization/roleAssignments/write permission, such as User Access Administrator or Owner.


## When to use this skill

Use this skill when you need to:



- Work with bicep for role assignment, least privilege role, RBAC role for, and role to read blobs

- Work with role for managed identity and custom role definition

- Assign role to identity

- Work with permissions to assign roles


## Example prompts

Try these prompts to activate this skill:



- "What Azure RBAC role should I assign to my managed identity?"

- "Which Azure role gives least privilege access to read blobs from storage?"

- "What role do I need for my identity to access Azure Key Vault secrets?"

- "Help me find the right Azure role for container registry access"

- "I need a custom role definition for my Azure storage account"

- "What Azure role should I use to give my function app access to Service Bus?"

- "Assign an Azure RBAC role to my identity for Cosmos DB read access"

- "What is the least privilege role for reading from a storage queue?"

- "I need to assign a role to my app service managed identity for database access"

- "Generate Bicep code for assigning a role to my function app"


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
