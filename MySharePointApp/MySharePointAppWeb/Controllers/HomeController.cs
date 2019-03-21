using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MySharePointAppWeb.Controllers
{
    using System.Diagnostics;

    using MySharePointAppWeb.Logic;
    using MySharePointAppWeb.Models;

    public class HomeController : Controller
    {
        [SharePointContextFilter]
        public ActionResult Index()
        {
            var sharePointContext = SharePointContextProvider.Current.GetSharePointContext(HttpContext);

            using (var clientContext = sharePointContext.CreateUserClientContextForSPHost())
            {
                if (clientContext != null)
                {
                    var currentUser = clientContext.Web.CurrentUser;

                    clientContext.Load(currentUser, user => user.Title);

                    clientContext.ExecuteQuery();

                    this.ViewBag.UserName = currentUser.Title;

                    this.ViewBag.SPHostUrl = sharePointContext.SPHostUrl;

                    // Save the token for Token Utility
                    var token = new TokenUtility().GetContextTokenFromRequest(this.HttpContext);
                }
            }

            return this.View();
        }

        public ActionResult About()
        {
            this.ViewBag.Message = "Your application description page.";

            return this.View();
        }

        public ActionResult Contact()
        {
            this.ViewBag.Message = "Your contact page.";

            return this.View();
        }

        /// <summary>
        /// The custom action info.
        /// Test project for Modern Page web part command handler attribute values
        /// <seealso cref="https://social.msdn.microsoft.com/Forums/windowshardware/en-US/58e36bdd-0af7-4ca0-9fb1-98f945dbf60c/url-token-for-custom-action-on-ribbon-does-not-work-from-library-webpart-in-modern-ui"/>
        /// </summary>
        /// <returns>
        /// The <see cref="ActionResult"/>.
        /// </returns>
        public ActionResult CustomActionInfo()
        {
            string selectedListId = SharePointContext.GetSpKey(this.HttpContext.Request, SharePointContext.SPListIdKey);
            string listId = SharePointContext.GetSpKey(this.HttpContext.Request, "ListId");
            string selectedItemId = SharePointContext.GetSpKey(this.HttpContext.Request, SharePointContext.SPListItemIdKey);
            string listUrlDir = SharePointContext.GetSpKey(this.HttpContext.Request, "ListUrlDir");
            string source = SharePointContext.GetSpKey(this.HttpContext.Request, "Source");

            string selectedListTitle = this.GetListTitle(selectedListId);
            string listTitle = this.GetListTitle(listId);
            ListInfoModel model = new ListInfoModel
                                      {
                                          ListId = listId,
                                          ListTitle = listTitle,
                                          SelectedItemId = selectedItemId,
                                          SelectedListTitle = selectedListTitle,
                                          SelectedListId = selectedListId,
                                          ListUrlDir = listUrlDir,
                                          Source = source
                                      };

            return this.View(model);
        }

        /// <summary>
        /// Get a list's title.
        /// </summary>
        /// <param name="listId">
        /// The list id.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private string GetListTitle(string listId)
        {
            SharePointContext context = SharePointContextProvider.Current.GetSharePointContext(this.HttpContext);
            using (var clientContext = context.CreateUserClientContextForSPHost())
            {
                try
                {
                    Guid listGId;
                    if (Guid.TryParse(listId, out listGId) && clientContext != null)
                    {
                        List list = clientContext.Web.Lists.GetById(listGId);

                        clientContext.Load(list);
                        clientContext.ExecuteQuery();

                        return list.Title;
                    }
                }
                catch (Exception ex)
                {
                    this.ViewBag.Error = ex.Message;

                    Trace.TraceError(ex.Message);
                }

                return "Unknown";
            }
        }
    }
}