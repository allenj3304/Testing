// --------------------------------------------------------------------------------------------------------------------
// <summary>
// XRSolutions.Meridian.Teams.WebWeb.Logic.TokenUtility : TokenUtility.cs
//     Implementation file of TokenUtility class
//
// Purpose: SharePoint Token Utility
// Thanks to: Vesa Juvonen
// see cref="http://blogs.msdn.com/b/vesku/archive/2013/08/23/site-provisioning-techniques-and-remote-provisioning-in-sharepoint-2013.aspx"          
// 
// </summary>
// <copyright company="XRSolutions" file="TokenUtility.cs">
//   Copyright XRSolutions, All rights reserved
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace MySharePointAppWeb.Logic
{
    using System;
    using System.Web;

    using Microsoft.SharePoint.Client;

    public class TokenUtility
    {
        /// <summary>
        /// Resolve client context using SPHostUrl a originator 
        /// </summary>
        /// <param name="httpContext">
        /// The http Context.
        /// </param>
        /// <returns>
        /// </returns>
        public ClientContext GetClientContext(HttpContextBase httpContext)
        {
            Uri hostWeb = this.GetHostUrl(httpContext);
            return this.GetClientContext(httpContext, hostWeb);
        }

        /// <summary>
        /// To solve client context based on given URL. Solves also if request is done from on-prem or from Office365.
        /// </summary>
        /// <param name="httpContext">
        /// The http Context.
        /// </param>
        /// <param name="webUrl">
        /// </param>
        /// <returns>
        /// </returns>
        public ClientContext GetClientContext(HttpContextBase httpContext, Uri webUrl)
        {
            if (webUrl != null)
            {
                string contextTokenString = this.GetContextTokenFromRequest(httpContext);

                // Do this in ddifferrent ways for ACS and S2S auth patterns
                if (string.IsNullOrEmpty(contextTokenString))
                {
                    // if S2S or cert based
                    return TokenHelper.GetS2SClientContextWithWindowsIdentity(webUrl, httpContext.Request.LogonUserIdentity);
                }

                // if ACS or low trust based
                return TokenHelper.GetClientContextWithContextToken(webUrl.ToString(), contextTokenString, httpContext.Request.Url.Authority);
            }
            
            // you didn't give me URI, return null...
            return null;
        }

        /// <summary>
        /// To solve the host URL
        /// </summary>
        /// <returns>host web URI, if url was resolved. Null if not</returns>
        Uri GetHostUrl(HttpContextBase httpContext)
        {
            string hostUrl = string.Empty;

            if (httpContext.Request.QueryString["SPHostUrl"] != null)
            {
                hostUrl = httpContext.Request.QueryString["SPHostUrl"];
                httpContext.Session["SPHostUrl"] = hostUrl;
            }

            if (string.IsNullOrEmpty(hostUrl))
            {
                hostUrl = httpContext.Session["SPHostUrl"].ToString();
            }

            // If we got it return in URI format, if not, return null
            if (string.IsNullOrEmpty(hostUrl))
            {
                return null;
            }
            
            return new Uri(hostUrl);
        }

        public string GetContextTokenFromRequest(HttpContextBase httpContext)
        {
            string token = TokenHelper.GetContextTokenFromRequest(httpContext.Request);
            if (!string.IsNullOrEmpty(token))
            {
                httpContext.Session["SPContextToken"] = token;
            }
            else
            {
                if (httpContext.Session["SPContextToken"] != null)
                {
                    token = httpContext.Session["SPContextToken"].ToString();
                }
            }

            return token;
        }

    }
}