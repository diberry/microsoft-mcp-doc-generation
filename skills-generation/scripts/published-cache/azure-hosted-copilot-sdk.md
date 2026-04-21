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
					


				
			
		
	
					Azure skill for Hosted Copilot SDK
					
		
			 
				
					
		
			
				
			
			Feedback
		
	
				
		  
		
	 
		
			
				
					
				
				
					
						Summarize this article for me
					
				
			
			
			
		
	 
		
			## 
				In this article
			

		
	
					Build and deploy GitHub Copilot SDK apps to Azure.


Skill: azure-hosted-copilot-sdk | Source code


## What it provides

This skill provides GitHub Copilot with specialized knowledge. Build and deploy GitHub Copilot SDK apps to Azure.



Note


This skill automatically activates when your codebase contains @github/copilot-sdk in package.json or CopilotClient in source files. When detected, this skill becomes the entry point for deploy, modify, and add-feature workflows instead of the general-purpose azure-prepare skill.



## Prerequisites


- Azure subscription: Create a free account if you don't have one.

- AI assistant with Azure Skills: GitHub Copilot for Azure, Visual Studio Code with Azure MCP extension, Claude Code, or another compatible MCP client.

- Azure CLI (v2.60.0+): Install and sign in with az login.


## When to use this skill

Use this skill when you need to:



- Build custom copilot applications on Azure infrastructure.

- Create and configure copilot applications.

- Use the GitHub Copilot SDK to scaffold and build copilot-powered applications.

- Deploy your copilot application to Azure infrastructure.

- Host applications on Azure using Azure OpenAI models or bring your own model (BYOM).

- Integrate your own large language model (LLM) with the Copilot SDK.

- Configure Azure OpenAI models, managed identity authentication, and the Copilot SDK service.

- Build chat applications using the Copilot SDK service template and the CopilotClient library.

- Implement session management and message handling using the GitHub Models API.


## Example prompts

Try these prompts to activate this skill:



- "Build a Copilot SDK app and deploy it"

- "Create a new copilot SDK service"

- "Scaffold a copilot-powered app on Azure"

- "Build with the GitHub Copilot SDK and host it"

- "Build a Copilot SDK app with my own Azure model"

- "Create a copilot app using my Azure OpenAI model"

- "Set up a copilot service with BYOM and DefaultAzureCredential"

- "Build a copilot app that uses a self-hosted model on Azure"

- "Deploy a copilot SDK app with my own endpoint"

- "Create a copilot app and bring your own model from Azure OpenAI"


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
