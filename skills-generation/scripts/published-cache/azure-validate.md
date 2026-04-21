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
					


				
			
		
	
					Azure skill for validate
					
		
			 
				
					
		
			
				
			
			Feedback
		
	
				
		  
		
	 
		
			
				
					
				
				
					
						Summarize this article for me
					
				
			
			
			
		
	 
		
			## 
				In this article
			

		
	
					Pre-deployment validation for Azure readiness. Run deep checks on configuration, infrastructure (Bicep or Terraform), role-based access control (RBAC) role assignments, managed identity permissions, and prerequisites before deploying.


Skill: azure-validate | Source code


## What it provides

This skill provides GitHub Copilot with specialized knowledge. Pre-deployment validation for Azure readiness. Run deep checks on configuration, infrastructure (Bicep or Terraform), RBAC role assignments, managed identity permissions, and prerequisites before deploying.


## Prerequisites


- Azure subscription: Create a free account if you don't have one.

- AI assistant with Azure Skills: GitHub Copilot for Azure, Visual Studio Code with Azure MCP extension, Claude Code, or another compatible MCP client.

- Azure CLI (v2.60.0+): Install and sign in with az login.


## When to use this skill

Use this skill when you need to:



- Validate my app in Azure

- Check deployment readiness in Azure

- Run preflight checks in Azure

- Verify configuration in Azure

- Check if ready to deploy

- Validate azure.yaml

- Validate Bicep in Azure

- Test before deploying in Azure

- Troubleshoot deployment errors in Azure

- Validate Azure Functions


## Example prompts

Try these prompts to activate this skill:



- "Check if my app is ready to deploy to Azure"

- "Validate my azure.yaml configuration"

- "Run preflight checks before Azure deployment"

- "Troubleshoot deployment errors"

- "Verify my infrastructure configuration before deploying"

- "Is my app ready for Azure deployment?"

- "Validate my Bicep configuration"

- "Validate my Bicep template before deploying to Azure"

- "Check my deployment permissions before running azd up"

- "Verify my Bicep files are valid before provisioning"


## Deployment workflow

This skill is the second step in the deployment workflow:



- azure-prepare — generates infrastructure files and .azure/deployment-plan.md

- azure-validate (this skill) — validates the deployment plan and infrastructure before deploying

- azure-deploy — executes the deployment


## Related content


- Azure skill for prepare

- Azure skill for deploy

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
