using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MySharePointAddin2021Web.Controllers
{
    public class HomeController : Controller
    {
        [SharePointContextFilter]
        public ActionResult Index()
        {
            SharePointContext context = SharePointContextProvider.Current.GetSharePointContext(HttpContext);

            using (ClientContext clientContext = context.CreateUserClientContextForSPHost())
            {
                if (clientContext != null)
                {
                    User currentUser = clientContext.Web.CurrentUser;

                    clientContext.Load(currentUser, user => user.Title);

                    clientContext.ExecuteQuery();

                    this.ViewBag.UserName = currentUser.Title;
                }
            }

            return this.View();
        }

        /// <summary>
        /// The about.
        /// </summary>
        /// <returns>
        /// The <see cref="ActionResult"/>.
        /// </returns>
        public ActionResult About()
        {
            this.ViewBag.Message = "Your application description page.";

            this.ViewBag.Version = this.GetType().Assembly.GetName().Version;
            return this.View();
        }

        /// <summary>
        /// The contact.
        /// </summary>
        /// <returns>
        /// The <see cref="ActionResult"/>.
        /// </returns>
        public ActionResult Contact()
        {
            this.ViewBag.Message = "Your contact page.";

            return this.View();
        }
    }
}
