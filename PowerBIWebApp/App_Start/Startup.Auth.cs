// Copyright Microsoft

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using System.Configuration;
using System.Threading.Tasks;
using PowerBIWebApp.Utils;

namespace PowerBIWebApp
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            // Configure the authentication type and settings
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);
            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            // Configure the OWIN OpenId Connect options
            app.UseOpenIdConnectAuthentication(new OpenIdConnectAuthenticationOptions
            {
                ClientId = Settings.ClientId,
                Authority = Settings.AzureADAuthority,
                Notifications = new OpenIdConnectAuthenticationNotifications()
                {
                    // When an auth code is received
                    AuthorizationCodeReceived = (context) =>
                    {
                        // Create the app credentials and get reference to the user
                        ClientCredential creds = new ClientCredential(Settings.ClientId, Settings.Key);
                       
                        // Use the OpenID Connect code to obtain access token & refresh token...
                        //  save those in a persistent store...
                        AuthenticationContext authContext = new AuthenticationContext(Settings.AzureADAuthority);

                        // Obtain access token from the AzureAD graph
                        Uri redirectUri = new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Path));

                        //Pass the OpenID Connect code passed from Azure AD on successful auth (context.Code)
                        authContext.AcquireTokenByAuthorizationCode(context.Code, redirectUri, creds, Settings.AzureAdGraphResourceId);

                        // Return a successful auth
                        return Task.FromResult(0);
                    },
                    AuthenticationFailed = (context) =>
                    {
                        context.HandleResponse();
                        return Task.FromResult(0);
                    }
                },
                TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = false
                }
            });
        }
    }
}
