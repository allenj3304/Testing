// --------------------------------------------------------------------------------------------------------------------
// <summary>
//   XRSolutions.Meridian.Teams.WebWeb.Controllers.Logic.DeployManager : DeployManager.cs
//   Implementation file of DeployManager class
//   Purpose: SharePoint Site provisioning logic
//   Thanks to: Vesa Juvonen
//   see cref="http://blogs.msdn.com/b/vesku/archive/2013/08/23/site-provisioning-techniques-and-remote-provisioning-in-sharepoint-2013.aspx"
// </summary>
// <copyright company="XRSolutions" file="DeployManager.cs">
//   Copyright XRSolutions, All rights reserved
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace MySharePointAppWeb.Logic
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web;
    using System.Web.Hosting;
    using System.Xml.Linq;

    using Microsoft.Ajax.Utilities;
    using Microsoft.SharePoint.Client;
    using Microsoft.SharePoint.Client.WebParts;

    using File = System.IO.File;

    /// <summary>
    /// Actual code on manipulating the created sites based on templates
    /// </summary>
    public class DeployManager
    {
        /// <summary>
        /// Create a sub site.
        /// </summary>
        /// <param name="siteUrl">
        /// The site url.
        /// </param>
        /// <param name="templateName">
        /// The SharePoint template name.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        /// <param name="description">
        /// The description.
        /// </param>
        /// <param name="clientContext">
        /// The client context.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public string CreateSubSite(string siteUrl, string templateName, string title, string description, ClientContext clientContext)
        {
            // Currently only english, could be extended to be configurable based on language pack usage
            // Lookup the web template, need the name from the title
            WebTemplateCollection templates = clientContext.Web.GetAvailableWebTemplates(1033, false);
            clientContext.Load(templates);
            clientContext.ExecuteQuery();
            WebTemplate template = templates.FirstOrDefault(t => t.Title.Contains(templateName));
            if (template == null)
            {
                throw new ArgumentException(
                    string.Format(
                        "The project site template '{0}' was not found in the available solutions.\r\n  Check the App settings and the sites solutions for a matching template name.", 
                        templateName));
            }

            // Create web creation configuration
            WebCreationInformation information = new WebCreationInformation
                                                     {
                                                         WebTemplate = template.Name, 
                                                         Description = description, 
                                                         Title = title, 
                                                         Url = siteUrl, 
                                                         Language = 1033
                                                     };

            // Load host web and add new web to it.            
            Web web = clientContext.Web;
            Web newWeb = web.Webs.Add(information);
            clientContext.ExecuteQuery();
            clientContext.Load(newWeb);
            clientContext.ExecuteQuery();

            // All done, let's return the URL of the newly created site
            return newWeb.Url;
        }

        /// <summary>
        /// Create a site based on selected "template" with configurable options
        /// </summary>
        /// <param name="siteUrl">
        /// The site URL.
        /// </param>
        /// <param name="template">
        /// The template.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        /// <param name="description">
        /// The description.
        /// </param>
        /// <param name="clientContext">
        /// The client Context.
        /// </param>
        /// <param name="httpContext">
        /// The http Context.
        /// </param>
        /// <param name="baseConfiguration">
        /// The base Configuration.
        /// </param>
        /// <param name="isChildSite">
        /// The is Child Site.
        /// </param>
        /// <returns>
        /// URL to the new sub site.
        /// </returns>
        public string CreateSubSite(string siteUrl, string template, string title, string description, ClientContext clientContext, HttpContextBase httpContext, XDocument baseConfiguration, bool isChildSite = false)
        {
            Web newWeb;

            // Resolve the template configuration to be used for chosen template
            XElement templateConfig = this.GetTemplateConfig(template, baseConfiguration);
            string siteTemplate = this.SolveUsedTemplate(template, templateConfig);

            if (!siteUrl.IsNullOrWhiteSpace())
            {
                // Create web creation configuration
                WebCreationInformation information = new WebCreationInformation
                                                         {
                                                             WebTemplate = siteTemplate, 
                                                             Description = description, 
                                                             Title = title, 
                                                             Url = siteUrl, 
                                                             Language = 1033
                                                         };

                // Currently only english, could be extended to be configurable based on language pack usage
                // Load host web and add new web to it.            
                Web web = clientContext.Web;
                newWeb = web.Webs.Add(information);
                clientContext.ExecuteQuery();
                clientContext.Load(newWeb);
                clientContext.ExecuteQuery();
            }
            else
            {
                newWeb = clientContext.Web;
                if (!title.IsNullOrWhiteSpace())
                {
                    newWeb.Title = title;
                }

                if (!description.IsNullOrWhiteSpace())
                {
                    newWeb.Description = description;
                }

                if (!description.IsNullOrWhiteSpace() | !title.IsNullOrWhiteSpace())
                {
                    newWeb.Update();
                }

                clientContext.Load(newWeb, web => web.Url);
                clientContext.ExecuteQuery();
            }

            // Add JS and custom action to sub site, which was just created
            using (ClientContext subSiteContext = new TokenUtility().GetClientContext(httpContext, new Uri(newWeb.Url)))
            {
                this.DeployFiles(subSiteContext, templateConfig);
                this.DeployCustomActions(subSiteContext, templateConfig);
                this.DeployLists(subSiteContext, templateConfig);

                if (!isChildSite)
                {
                    this.DeploySubSites(subSiteContext, templateConfig, httpContext, baseConfiguration);
                }

                this.DeployNavigation(subSiteContext, templateConfig);
            }

            //// Apply oob them to just created web - resolve theme URL in root site in site collection
            Site site = clientContext.Site;
            clientContext.Load(site);
            Web rootWeb = site.RootWeb;
            clientContext.Load(rootWeb);
            clientContext.ExecuteQuery();

            /*
            // Apply a theme?
            if(isThemeApplied)
            {
                newWeb.ApplyTheme(
                    URLCombine(rootWeb.ServerRelativeUrl, "/_catalogs/theme/15/palette008.spcolor"),
                    URLCombine(rootWeb.ServerRelativeUrl, "/_catalogs/theme/15/fontscheme003.spfont"),
                    null,
                    true);
                clientContext.ExecuteQuery();
            }
*/

            // All done, let's return the URL of the newly created site
            return newWeb.Url;
        }

        /// <summary>
        /// Get current templates from xml configuration
        /// </summary>
        /// <param name="doc">
        /// The document
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerable"/>.
        /// </returns>
        /// <remarks>
        /// Could be extended to support filtering based on current web template
        /// </remarks>
        internal static IEnumerable<string> GetAvailableSubSiteTemplates(XDocument doc)
        {
            if (doc.Root == null)
            {
                return null;
            }

            XElement templates = doc.Root.Element("Templates");

            return templates == null
                       ? null
                       : templates.Elements().Select(element => element.Attribute("Name").Value).ToList();
        }

        /// <summary>
        /// Get the available SharePoint site collection's web templates (solutions).
        /// </summary>
        /// <param name="context">
        /// The SharePoint Host Web context.
        /// </param>
        /// <returns>
        /// The <see cref="List"/> of available Web Titles.
        /// </returns>
        internal static List<string> GetAvailableWebTemplates(ClientContext context)
        {
            WebTemplateCollection templates = context.Web.GetAvailableWebTemplates(1033, false);
            context.Load(templates);
            context.ExecuteQuery();

            IEnumerable<string> list = from t in templates.ToList() where !t.IsHidden select t.Title;

            return list.ToList();
        }

        /// <summary>
        /// Utility function for URL mapping
        /// The url combine.
        /// </summary>
        /// <param name="baseUrl">
        /// The base url.
        /// </param>
        /// <param name="relativeUrl">
        /// The relative url.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string URLCombine(string baseUrl, string relativeUrl)
        {
            if (baseUrl.Length == 0)
            {
                return relativeUrl;
            }

            if (relativeUrl.Length == 0)
            {
                return baseUrl;
            }

            return string.Format("{0}/{1}", baseUrl.TrimEnd(new[] { '/', '\\' }), relativeUrl.TrimStart(new[] { '/', '\\' }));
        }

        /// <summary>
        /// Activate feature.
        /// </summary>
        /// <param name="webUrl">
        /// The web url.
        /// </param>
        /// <param name="featureId">
        /// The feature ID.
        /// </param>
        /// <param name="fds">
        /// The Feature Definition Scope.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/> True if the feature is available.
        /// </returns>
        public static bool ActivateFeature(
            string webUrl,
            Guid featureId,
            FeatureDefinitionScope fds = FeatureDefinitionScope.Web)
        {
            using (ClientContext ctx = new ClientContext(webUrl))
            {
                return ActivateFeature(ctx, featureId, fds);
            }
        }

        /// <summary>
        /// Activate feature.
        /// </summary>
        /// <param name="ctx">
        /// The client context.
        /// </param>
        /// <param name="featureId">
        /// The feature ID use one of the static DeployManager.SiteFeature values.
        /// </param>
        /// <param name="fds">
        /// The Feature Definition Scope.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/> True if the feature is available.
        /// </returns>
        public static bool ActivateFeature(
            ClientContext ctx,
            Guid featureId,
            FeatureDefinitionScope fds = FeatureDefinitionScope.Web)
        {
            var features = ctx.Web.Features;
            ctx.Load(features);
            ctx.ExecuteQuery();

            try
            {
                features.Add(featureId, true, fds);
                ctx.ExecuteQuery();
            }
            catch (ServerException ex)
            {
                // Ignore: Feature with Id 'XXX' is not installed in this farm, and cannot be added to this scope.
                if (ex.ServerErrorTypeName != "System.InvalidOperationException")
                {
                    throw;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Add a web part to a site page.
        /// </summary>
        /// <param name="context">
        /// The SharePoint context.
        /// </param>
        /// <param name="webpartxml">
        /// The web part XML.
        /// </param>
        /// <param name="pageUrl">
        /// The destination page URL.
        /// </param>
        /// <param name="zone">
        /// The zone ID.
        /// </param>
        /// <param name="zoneIndex">
        /// The zone index.
        /// </param>
        /// <param name="replacetag">Placeholder tag to replace on publishing pages.</param>
        public static void AddWebPart(ClientContext context, string webpartxml, string pageUrl, string zone, int zoneIndex, string replacetag)
        {
            Microsoft.SharePoint.Client.File page = null;
            try
            {
                page = context.Web.GetFileByServerRelativeUrl(pageUrl);
                ListItem listItemHome = page.ListItemAllFields;
                context.Load(page);
                context.Load(listItemHome);
                context.ExecuteQuery();

                page.CheckOut();
                LimitedWebPartManager wpm = page.GetLimitedWebPartManager(PersonalizationScope.Shared);

                WebPartDefinition importedWebPart = wpm.ImportWebPart(webpartxml);
                WebPartDefinition webPart = wpm.AddWebPart(importedWebPart.WebPart, zone, zoneIndex);
                context.Load(webPart, w => w.Id);
                context.ExecuteQuery();

                // Position the part at top of the Publishing Page
                if (zone == "wpz")
                {
                    // SitePage item
                    if (listItemHome.FieldValues.ContainsKey("WikiField"))
                    {
                        // Look for Placeholder text to replace, keeps the web part in the table (no zones)
                        string pageContents = listItemHome["WikiField"] as string;
                        if (pageContents != null && pageContents.Contains(replacetag))
                        {
                            listItemHome["WikiField"] = pageContents.Replace(replacetag, GetEmbeddedWebPart(webPart.Id));
                        }
                        else
                        {
                            listItemHome["WikiField"] = string.Concat(GetEmbeddedWebPart(webPart.Id), pageContents);
                        }
                    }
                    else if (listItemHome.FieldValues.ContainsKey("PublishingPageContent"))
                    {
                        // Pages item
                        listItemHome["PublishingPageContent"] = string.Concat(GetEmbeddedWebPart(webPart.Id), "<br/>", listItemHome["PublishingPageContent"], "<br/>");
                    }

                    listItemHome.Update();
                    context.ExecuteQuery();
                }
            }
            catch (Exception e)
            {
                // Error handling
                System.Diagnostics.Trace.TraceError("Error Adding Opportunity WebPart to page " + e.Message);
            }
            finally
            {
                if (page != null)
                {
                    page.CheckIn("Added the Opportunity Details Web Part", CheckinType.MinorCheckIn);
                    ////page.Publish("Added the Opportunity Details Web Part");
                    context.ExecuteQuery();
                }
            }
        }

        /// <summary>
        /// Get embedded web part XML.
        /// </summary>
        /// <param name="webPartId">
        /// The <see cref="Guid"/> Id of web part to embed.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>Embedded web part XML.
        /// </returns>
        private static string GetEmbeddedWebPart(Guid webPartId)
        {
            // set the web part's ID as part of the ID-s of the div elements
            const string EmbeddedWebPartFormat = @"<div class=""ms-rtestate-read ms-rte-wpbox"">
                            <div class=""ms-rtestate-notify ms-rtegenerate-notify ms-rtestate-read {0}"" id=""div_{0}"">
                            </div>
                            <div id=""vid_{0}"" style=""display:none"">
                            </div>
                       </div>";

            return string.Format(EmbeddedWebPartFormat, webPartId);
        }

        /// <summary>
        /// Sub site provisioning handler
        /// </summary>
        /// <param name="clientContext">
        /// The client Context.
        /// </param>
        /// <param name="templateConfig">
        /// The template Config.
        /// </param>
        /// <param name="httpContext">
        /// The http Context.
        /// </param>
        /// <param name="baseConfiguration">
        /// The base Configuration.
        /// </param>
        private void DeploySubSites(ClientContext clientContext, XElement templateConfig, HttpContextBase httpContext, XDocument baseConfiguration)
        {
            XElement sitesToCreate = templateConfig.Element("Sites");
            if (sitesToCreate != null)
            {
                // Let's re-load this web just in case to avoid context issues
                Web web = clientContext.Web;
                clientContext.Load(web);
                clientContext.ExecuteQuery();
                
                // If we do have sub sites defined in the config, let's provision those as well
                foreach (XElement siteToCreate in sitesToCreate.Elements())
                {
                    this.CreateSubSite(siteToCreate.Attribute("Url").Value, siteToCreate.Attribute("Template").Value, siteToCreate.Attribute("Title").Value, siteToCreate.Attribute("Description").Value, clientContext, httpContext, baseConfiguration, true);
                }
            }
        }

        /// <summary>
        /// Handler for the navigation note configurations
        /// </summary>
        /// <param name="clientContext">
        /// Context to apply navigation nodes to
        /// </param>
        /// <param name="siteTemplate">
        /// XML structure for template
        /// </param>
        private void DeployNavigation(ClientContext clientContext, XElement siteTemplate)
        {
            XElement navigationNodesToCreate = siteTemplate.Element("NavigationNodes");
            if (navigationNodesToCreate != null)
            {
                Web web = clientContext.Web;
                clientContext.Load(web);
                clientContext.ExecuteQuery();
                foreach (XElement navigationNodeToCreate in navigationNodesToCreate.Elements())
                {
                    string title = navigationNodeToCreate.Attribute("Title").Value;
                    string url = navigationNodeToCreate.Attribute("Url").Value;
                    string navType = navigationNodeToCreate.Attribute("Type").Value;

                    if (url.StartsWith("/"))
                    {
                        url = URLCombine(web.Url, url);
                    }

                    // Let's create the nodes based on configuration to quick launch
                    NavigationNodeCreationInformation nodeInformation = new NavigationNodeCreationInformation
                                                                            {
                                                                                Title = title, 
                                                                                Url = url
                                                                            };

                    clientContext.Load(web.Navigation.QuickLaunch);
                    clientContext.ExecuteQuery();

                    if (navType == "TopNavBar")
                    {
                        web.Navigation.TopNavigationBar.Add(nodeInformation);
                    }
                    else
                    {
                        web.Navigation.QuickLaunch.Add(nodeInformation);
                    }

                    clientContext.ExecuteQuery();
                }
            }
        }

        /// <summary>
        /// Generic handler for list instances
        /// </summary>
        /// <param name="clientContext">
        /// Context to apply the changes in
        /// </param>
        /// <param name="siteTemplate">
        /// XML configuration for the template
        /// </param>
        private void DeployLists(ClientContext clientContext, XElement siteTemplate)
        {
            XElement liststoCreate = siteTemplate.Element("Lists");
            if (liststoCreate == null)
            {
                return;
            }

            Web web = clientContext.Web;
            clientContext.Load(web);
            clientContext.ExecuteQuery();
            foreach (XElement listToCreate in liststoCreate.Elements())
            {
                ListCreationInformation listInformation = new ListCreationInformation
                                                              {
                                                                  Description = listToCreate.Attribute("Description").Value
                                                              };
                if (!string.IsNullOrEmpty(listToCreate.Attribute("DocumentTemplate").Value))
                {
                    listInformation.DocumentTemplateType = Convert.ToInt32(listToCreate.Attribute("DocumentTemplate").Value);
                }

                if (!string.IsNullOrEmpty(listToCreate.Attribute("OnQuickLaunch").Value))
                {
                    listInformation.QuickLaunchOption =
                        Convert.ToBoolean(listToCreate.Attribute("OnQuickLaunch").Value)
                            ? QuickLaunchOptions.On
                            : QuickLaunchOptions.Off;
                }

                if (!string.IsNullOrEmpty(listToCreate.Attribute("TemplateType").Value))
                {
                    listInformation.TemplateType = Convert.ToInt32(listToCreate.Attribute("TemplateType").Value);
                }

                listInformation.Title = listToCreate.Attribute("Title").Value;
                listInformation.Url = listToCreate.Attribute("Url").Value;

                List newList = web.Lists.Add(listInformation);
                clientContext.ExecuteQuery();
                
                clientContext.Load(newList);
                clientContext.ExecuteQuery();
                this.DeployListFields(clientContext, newList, listToCreate);
            }
        }

        /// <summary>
        /// The deploy list fields.
        /// TODO: Implement Choices
        /// </summary>
        /// <param name="clientContext">
        /// The client context.
        /// </param>
        /// <param name="list">
        /// The list.
        /// </param>
        /// <param name="listToCreate">
        /// The list to create.
        /// </param>
        private void DeployListFields(ClientContext clientContext, List list, XElement listToCreate)
        {
            XElement liststoCreate = listToCreate.Element("Fields");
            if (liststoCreate == null)
            {
                return;
            }

            foreach (XElement fieldElement in liststoCreate.Elements())
            {
                string name = fieldElement.Attribute("Name").Value;
                string displayName = fieldElement.Attribute("DisplayName").Value;

                list.Fields.AddFieldAsXml(fieldElement.ToString(), true, AddFieldOptions.AddToDefaultContentType);
                list.Update();
                clientContext.ExecuteQuery();

                Field field = list.Fields.GetByInternalNameOrTitle(displayName);
                field.Title = name;

                field.Update();
                clientContext.Load(field);
                clientContext.ExecuteQuery();
            }
        }

        /// <summary>
        /// Generic handler for custom action entries
        /// </summary>
        /// <param name="clientContext">
        /// The client context
        /// </param>
        /// <param name="siteTemplate">
        /// XML structure for the template
        /// </param>
        private void DeployCustomActions(ClientContext clientContext, XElement siteTemplate)
        {
            XElement customActionsToDeploy = siteTemplate.Element("CustomActions");
            if (customActionsToDeploy != null)
            {
                foreach (XElement customAction in customActionsToDeploy.Elements())
                {
                    if (this.CustomActionAlreadyExists(clientContext, customAction.Attribute("Name").Value))
                    {
                        continue;
                    }

                    UserCustomAction action = clientContext.Web.UserCustomActions.Add();
                    action.ScriptSrc = customAction.Attribute("ScriptSrc").Value;
                    action.Location = customAction.Attribute("Location").Value;
                    action.Name = customAction.Attribute("Name").Value;
                    action.Sequence = 1000;
                    action.Update();
                    clientContext.ExecuteQuery();
                }
            }
        }

        /// <summary>
        /// Utility method to check particular custom action already exists on the web
        /// </summary>
        /// <param name="clientContext">
        /// The client context
        /// </param>
        /// <param name="name">
        /// Name of the custom action
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool CustomActionAlreadyExists(ClientContext clientContext, string name)
        {
            clientContext.Load(clientContext.Web.UserCustomActions);
            clientContext.ExecuteQuery();
            for (int i = 0; i < clientContext.Web.UserCustomActions.Count - 1; i++)
            {
                if (!string.IsNullOrEmpty(clientContext.Web.UserCustomActions[i].Name) &&
                        clientContext.Web.UserCustomActions[i].Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Generic handler for the file deployments
        /// </summary>
        /// <param name="clientContext">
        /// The client context
        /// </param>
        /// <param name="siteTemplate">
        /// XML definition for the template
        /// </param>
        private void DeployFiles(ClientContext clientContext, XElement siteTemplate)
        {
            XElement filesToLoad = siteTemplate.Element("Files");
            if (filesToLoad != null)
            {
                foreach (XElement file in filesToLoad.Elements())
                {
                    if (file.Attribute("UploadToDocumentLibray").Value == "false")
                    {
                        this.DeployFileToWebFolder(clientContext, file.Attribute("Src").Value, file.Attribute("TargetFolder").Value);
                    }

                    // TODO - handler for the document library is missing
                }
            }
        }

        /// <summary>
        /// Generic upload file to context
        /// </summary>
        /// <param name="clientContext">
        /// The client Context.
        /// </param>
        /// <param name="file">
        /// The file.
        /// </param>
        /// <param name="foldername">
        /// The foldername.
        /// </param>
        public void DeployFileToWebFolder(ClientContext clientContext, string file, string foldername)
        {
            Folder folder = this.DoesFolderExists(clientContext, foldername)
                               ? clientContext.Web.Folders.GetByUrl(foldername)
                               : clientContext.Web.Folders.Add(foldername);

            // Load Folder instance
            clientContext.Load(folder);
            clientContext.ExecuteQuery();

            this.UploadFileToContextWeb(clientContext, HostingEnvironment.MapPath(file), folder);
        }

        /// <summary>
        /// Get configuration for specific template based on name of the template
        /// </summary>
        /// <param name="chosenTemplate">
        /// </param>
        /// <param name="configuration">
        /// </param>
        /// <returns>
        /// The <see cref="XElement"/>.
        /// </returns>
        private XElement GetTemplateConfig(string chosenTemplate, XDocument configuration)
        {
            XElement templates = configuration.Root.Element("Templates");
            if (templates == null)
            {
                return null;
            }
            
            IEnumerable<XElement> template = from el in templates.Elements()
                                             where (string)el.Attribute("Name") == chosenTemplate
                                             select el;

            return template.ElementAt(0);
        }

        /// <summary>
        /// Return the root template value from the config
        /// </summary>
        /// <param name="template">
        /// </param>
        /// <param name="templateConfig">
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private string SolveUsedTemplate(string template, XElement templateConfig)
        {
            // Root template is stored in RootTemplate attribute in this level
            return templateConfig.Attribute("RootTemplate").Value;
        }

        /// <summary>
        /// Handler for teh JS injection pattern
        /// </summary>
        /// <param name="clientContext">
        /// </param>
        private void AddJSInjectionToSite(ClientContext clientContext)
        {
            // Add JS file to web remotely
            this.DeployJSInjectionFilesToContextWeb(clientContext);

            if (!this.CustomActionForJSInjectionAlreadyExists(clientContext))
            {
                UserCustomAction action = clientContext.Web.UserCustomActions.Add();
                action.ScriptSrc = "~site/Injection_JS/SubSiteRemoteProvisioningWeb_InjectedJS.js";
                action.Location = "ScriptLink";
                action.Name = "Injection_JS";
                action.Sequence = 1000;
                action.Update();
                clientContext.ExecuteQuery();
            }
        }

        /// <summary>
        /// Checker for the JS injection custom action entry
        /// </summary>
        /// <param name="clientContext">
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool CustomActionForJSInjectionAlreadyExists(ClientContext clientContext)
        {
            clientContext.Load(clientContext.Web.UserCustomActions);
            clientContext.ExecuteQuery();
            for (int i = 0; i < clientContext.Web.UserCustomActions.Count - 1; i++)
            {
                if (!string.IsNullOrEmpty(clientContext.Web.UserCustomActions[i].Name) &&
                        clientContext.Web.UserCustomActions[i].Name == "Injection_JS")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Uploading JS injection files to context web
        /// </summary>
        /// <param name="clientContext">
        /// </param>
        private void DeployJSInjectionFilesToContextWeb(ClientContext clientContext)
        {
            var folder = this.DoesFolderExists(clientContext, "Injection_JS")
                             ? clientContext.Web.Folders.GetByUrl("Injection_JS")
                             : clientContext.Web.Folders.Add("Injection_JS");

            // Load Folder instance
            clientContext.Load(folder);
            clientContext.ExecuteQuery();

            string file = HostingEnvironment.MapPath(@"~/ResourceFiles/SubSiteRemoteProvisioningWeb_InjectedJS.js");
            this.UploadFileToContextWeb(clientContext, file, folder);
        }

        /// <summary>
        /// Utility function to check if the folder name exists already in the context web
        /// </summary>
        /// <param name="clientContext">
        /// </param>
        /// <param name="targetFolderUrl">
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool DoesFolderExists(ClientContext clientContext, string targetFolderUrl)
        {
            Folder folder = clientContext.Web.GetFolderByServerRelativeUrl(targetFolderUrl);
            clientContext.Load(folder);
            bool exists = false;

            try
            {
                clientContext.ExecuteQuery();
                exists = true;
            }
            catch (Exception)
            {
            }
            
            return exists;
        }

        /// <summary>
        /// Generic uploader for the file to context web
        /// </summary>
        /// <param name="context">
        /// The context
        /// </param>
        /// <param name="fullFilePath">
        /// Full file name with path
        /// </param>
        /// <param name="folder">
        /// Target folder
        /// </param>
        private void UploadFileToContextWeb(ClientContext context, string fullFilePath, Folder folder)
        {
            try
            {
                FileCreationInformation newFile = new FileCreationInformation();
                newFile.Content = File.ReadAllBytes(fullFilePath);
                newFile.Url = folder.ServerRelativeUrl + "/" + Path.GetFileName(fullFilePath);
                newFile.Overwrite = true;
                Microsoft.SharePoint.Client.File uploadFile = folder.Files.Add(newFile);
                context.Load(uploadFile);
                context.ExecuteQuery();
            }
            catch (Exception ex)
            {
                // TODO - Proper logging on exceptions... this is not really acceptable
                string fuu = ex.ToString();
            }
        }

        /// <summary>
        /// SharePoint site features.
        /// </summary>
        public static class SiteFeatures
        {
            /// <summary>
            /// SharePoint Server Publishing.
            /// Create a Web page library as well as supporting libraries to create and publish pages based on page layouts.
            /// Scope: Web
            /// </summary>
            public static readonly Guid Publishing = new Guid("94c94ca6-b32f-4da9-a9e3-1f3d343d7ecb");

            /// <summary>
            /// Enable App Side Loading.
            /// Enable side loading of Apps for Office and SharePoint
            /// Scope: Site
            /// </summary>
            public static readonly Guid EnableAppSideLoading = new Guid("ae3a1339-61f5-4f8f-81a7-abd2da956a7d");

            /// <summary>
            /// The developer feature.
            /// Scope: Site
            /// </summary>
            public static readonly Guid DeveloperFeature = new Guid("e374875e-06b6-11e0-b0fa-57f5dfd72085");
        }
    }
}   