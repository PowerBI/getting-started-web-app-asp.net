using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace PowerBIWebApp.Utils
{
    public class Settings
    {
        public static string ClientId
        {
            get { return ConfigurationManager.AppSettings["ida:ClientID"]; }
        }

        public static string Key
        {
            get { return ConfigurationManager.AppSettings["ida:Key"]; }
        }

        public static string AzureAdTenantId
        {
            get { return ConfigurationManager.AppSettings["ida:TenantId"]; }
        }

        public static string PowerBIResourceId
        {
            get { return "https://analysis.windows.net/powerbi/api"; }
        }

        public static string AzureAdGraphResourceId
        {
            get { return "https://graph.windows.net"; }
        }

        public static string AzureADAuthority
        {
            get { return string.Format("https://login.windows.net/{0}/", AzureAdTenantId); }
        }

        public static string ClaimTypeObjectIdentifier
        {
            get { return "http://schemas.microsoft.com/identity/claims/objectidentifier"; }
        }
    }
}