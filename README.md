# Testing
Test projects for MSDN samples

# Project: MySharePointApp
A C# provider hosted SharePoint-Addin to test a Custom Action on a Document Library.  Action displays URL Token values passed to the Controller.
Exercice for the CommandUIHandler element [Microsoft Documentation](https://docs.microsoft.com/en-us/sharepoint/dev/schema/commanduihandler-element)
- Created using SharePoint App template
- Provider Hosted
- Token authentication with Azure
- ASP.Net MVC

![Action Button on a Modern Page](MySharePointApp/CustomActionCapture.PNG?raw=true "Action Button on a Modern Page")

# Project MySharePointAddin2021
A sample provider hosted SharePoint Add-in for testing

A C# provider hosted SharePoint-Addin for testing.
- Created using SharePoint App template
- Provider Hosted
- Token authentication with Azure
- ASP.Net MVC

Customized TokenHelper.cs to support OAuth for GCC High ot DOD tenant. See [Fixing on the fly oauth issue for provider hosted add-in in GCC High or DOD](https://techcommunity.microsoft.com/t5/microsoft-sharepoint-blog/fixing-on-the-fly-oauth-issue-for-provider-hosted-add-in-in-gcc/ba-p/510115)
