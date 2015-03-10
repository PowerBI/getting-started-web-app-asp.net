The Power BI web sample shows you how to

-	[Register a Power BI ASP.NET web app in Azure AD](#Register)
-	[Create a Power BI web app](#Create)
		- [Configure web authentication](#Configure)
		- [Create a Power BI model](#Create)
		- [Create a Power BI view](#view)
		- [Create a Power BI controller](#controller)

<a name="Register"></a>
##Register a Power BI web app in Azure AD
Follow the steps in [Register a web app](Register+an+app.md). In step 10, copy and paste the **Client ID** and **Key** into web.config **appSettings**. In addition, enter your **TenantId** such as {your tenant id}. onmicrosoft.com.
<a name="Create"></a>
##Create a Power BI web app
You can integrate the Power BI REST API into a web app using various programming languages. The Power BI web sample shows how to use ASP.NET to create a Power BI web app. The sample uses an ASP.NET Model-View-Controller (MVC). The MVC architectural pattern separates an application into three main components: the model, the view, and the controller. To learn more about ASP.NET MVC apps, see [ASP.NET MVC Overview](https://msdn.microsoft.com/en-us/library/dd381412(v=vs.108).aspx).

Power BI REST APIs require Azure Active Directory for authentication. Power BI REST API calls are made on behalf of an authenticated user by passing a token in the “Authorization” header that is acquired through Azure Active Directory.

OAuth 2.0 is a commonly used, open standard for authorization. It provides client applications secure delegated access to server resources on behalf of a resource owner without sharing credentials. If you have worked with Twitter, LinkedIN or other web app APIs, OAuth may already be familiar to you. If you’d like to know more about this type of authentication, please review the OAuth article on MSDN and Power BI Authentication docs.

To learn more about how authentication works, see [Authenticate with Power BI](Authenticate+with+Power+BI.md).

The Power BI web app sample shows you how to

- [Configure web authentication](#Configure)
- [Create a Power BI model](#model)
- [Create a Power BI view](#view)
- [Create a Power BI controller](#controller)
 
<a name="Configure"></a>
##Configure web authentication
To configure authentication, configure web.config and implement a ConfigureAuth() method. 
<a name="webconfig"></a>
###Configure web.config	
    <!--Configure Power BI -->
    <add key="ida:TenantId" value="{your tenant id}.onmicrosoft.com" />
    <add key="ida:ClientID" value="{client id from Azure AD configuration page}" />
    <add key="ida:Key" value="{key from Azure AD configuration page}" />
<a name="ConfigAuth"></a>
###ConfigAuth() method

A ConfigAuth()
- Sets the authentication type to CookieAuthentication.
- Creates an OpenIdConnectAuthenticationOptions that
	- Sets ClientID to the client id from your Azure AD app. To get an Azure app client id, see [enter link description here](Register+an+app.md).
	- Sets Authority to a valid Azure AD authority as https://login.windows.net/{tenantID}/". For example, https://login.windows.net/{your tenant id}.onmicrosoft.com.
	- Creates an OpenIdConnectAuthenticationNotifications that is invoked after security token validation if an authorization code is present in the protocol message.
		- Creates a new ClientCredential passing ClientID and Key. You get the ClientID and Key from your Azure AD app configuration page. See [enter link description here](Register+an+app.md)
		- Creates an AuthenticationContext from a valid Azure AD authority.
		- Calls authContext.AcquireTokenByAuthorizationCode() to cache an Azure AD authorization code.

    
```
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            ...
        }
    }


    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app)
        {
            // Configure the authentication type and settings
            // Set the authentication type to CookieAuthentication
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

                        // Use the OpenID Connect code to obtain access token and refresh token
                        //  save those in a persistent store.
                        AuthenticationContext authContext = new                   
                           AuthenticationContext(Settings.AzureADAuthority);

                        // Obtain access token from the AzureAD graph
                        Uri redirectUri = new  
                           Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Path));

                        //Pass the OpenID Connect code passed from Azure AD on successful auth 
                        authContext.AcquireTokenByAuthorizationCode
                            (context.Code, redirectUri, creds, Settings.AzureAdGraphResourceId);

                        // Successful auth
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
```
<a name="model"></a>
##Create a Power BI model
A model implements application logic. For a Power BI web app, the model implements token logic and HttpClient request and response logic. The complete Power BI model is listed below.
<a name="resource"></a>
###Get a resource specific access token for Power BI
1. Create an AuthenticationContext from a valid Azure AD authority as https://login.windows.net/{tenantID}/". For example, https://login.windows.net/{your tenant id}.onmicrosoft.com.
2. Create a new ClientCredential passing ClientID and Key. You get the ClientID and Key from your Azure AD app configuration page.
3. Get access token for Power BI on behalf of a user


```
        private static async Task<string> getAccessToken()
        {
            // Create auth context (note: token is not cached)
            AuthenticationContext authContext = new AuthenticationContext(Settings.AzureADAuthority);

            // Create client credential
            var clientCredential = new ClientCredential(Settings.ClientId, Settings.Key);
            
            // Get user object id
            var userObjectId = ClaimsPrincipal.Current.FindFirst(Settings.ClaimTypeObjectIdentifier).Value;

            // Get access token for Power BI on behalf of a user
            return authContext.AcquireToken(Settings.PowerBIResourceId, clientCredential, new UserAssertion         
            (userObjectId, UserIdentifierType.UniqueId.ToString())).AccessToken;
        }
```
<a name="datasets"></a>
###Get all datasets


```
        public static async Task<List<PowerBIDataset>> GetDatasets()
        {
            List<PowerBIDataset> datasets = new List<PowerBIDataset>();
            var token = await getAccessToken();
            
            using (var client = new HttpClient{ BaseAddress = baseAddress })
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json; odata=verbose");

                using (var response = await client.GetAsync(String.Format("{0}/datasets", baseAddress)))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                    datasets = oResponse.SelectToken("datasets").ToObject<List<PowerBIDataset>>();
                }
            }

            return datasets;
        }

```
<a name="dataset"></a>
###Create a dataset


        
```
        public static async Task<Guid> CreateDataset(PowerBIDataset dataset)
        {
            var token = await getAccessToken();
            
            using (var client = new HttpClient{ BaseAddress = baseAddress })
            {
                var content = new StringContent(JsonConvert.SerializeObject(dataset)
                .Replace("\"id\":\"00000000-0000-0000-0000-000000000000\",", ""), System.Text.Encoding.Default, 
                "application/json");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                using (var response = await client.PostAsync(String.Format("{0}/datasets", baseAddress), content))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                    dataset.id = new Guid(oResponse.SelectToken("id").ToString());
                }
            }

            return dataset.id;
        }
```
<a name="DefineDataset"></a>
###Define a Power BI dataset


```
    public class PowerBIDataset
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public List<PowerBITable> tables { get; set; }
    }

    public class PowerBITable
    {
        public string name { get; set; }
        public List<PowerBIColumn> columns { get; set; }
    }


    public class PowerBIColumn
    {
        public string name { get; set; }
        public string dataType { get; set; }
    }

```
<a name="CompleteModel"></a>
###Complete Power BI Model


```
namespace PowerBIWebApp.Models
{
    public class PowerBIModel
    {
        static Uri baseAddress = new Uri("https://api.powerbi.com/beta/myorg/");

        /// <summary>
        /// Get resource specific access token for Power BI ("https://analysis.windows.net/powerbi/api")
        /// </summary>
        /// <returns>Access Token string</returns>
        private static async Task<string> getAccessToken()
        {
            // Create auth context (note: token is not cached)
            AuthenticationContext authContext = new 
              AuthenticationContext(Settings.AzureADAuthority);

            // Create client credential
            var clientCredential = new ClientCredential(Settings.ClientId, Settings.Key);
            
            // Get user object id
            var userObjectId = 
             ClaimsPrincipal.Current.FindFirst(Settings.ClaimTypeObjectIdentifier).Value;

            // Get access token for Power BI
            // Call Power BI APIs from Web API on behalf of a user
            return authContext.AcquireToken(Settings.PowerBIResourceId, clientCredential,   
                new UserAssertion(userObjectId,    
                UserIdentifierType.UniqueId.ToString())).AccessToken;
            }
        /// <summary>
        /// Get all datasets for a user
        /// </summary>
        /// <returns>List of PowerBIDataset</returns>
        public static async Task<List<PowerBIDataset>> GetDatasets()
        {
            List<PowerBIDataset> datasets = new List<PowerBIDataset>();
            var token = await getAccessToken();
            
            using (var client = new HttpClient{ BaseAddress = baseAddress })
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json; odata=verbose");

                using (var response = await client.GetAsync(String.Format("{0}/datasets", baseAddress)))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                    datasets = oResponse.SelectToken("datasets").ToObject<List<PowerBIDataset>>();
                }
            }

            return datasets;
        }

        /// <summary>
        /// Get a specific dataset based on id
        /// </summary>
        /// <param name="id">Guid id of dataset</param>
        /// <returns>PowerBIDataset</returns>
        public static async Task<PowerBIDataset> GetDataset(Guid id)
        {
            PowerBIDataset dataset = null;
            var token = await getAccessToken();
           
            using (var client = new HttpClient { BaseAddress = baseAddress })
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json; odata=verbose");

                using (var response = await client.GetAsync(String.Format("{0}/datasets/{1}", 
                baseAddress, id.ToString())))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                }
            }

            return dataset;
        }

        /// <summary>
        /// Create a dataset, including tables/columns
        /// </summary>
        /// <param name="dataset">PowerBIDataset</param>
        /// <returns>Guid id of the new dataset</returns>
        public static async Task<Guid> CreateDataset(PowerBIDataset dataset)
        {
            var token = await getAccessToken();
            
            using (var client = new HttpClient{ BaseAddress = baseAddress })
            {
                var content = new StringContent(JsonConvert.SerializeObject(dataset)
                .Replace("\"id\":\"00000000-0000-0000-0000-000000000000\",", ""), 
                System.Text.Encoding.Default, "application/json");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                using (var response = await client.PostAsync(String.Format("{0}/datasets", baseAddress), content))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject oResponse = JObject.Parse(responseString);
                    dataset.id = new Guid(oResponse.SelectToken("id").ToString());
                }
            }

            return dataset.id;
        }

        /// <summary>
        /// Clear all data our of a given table of a dataset
        /// </summary>
        /// <param name="dataset">Guid dataset igd</param>
        /// <param name="table">string table name</param>
        /// <returns>bool indicating success</returns>
        public static async Task<bool> ClearTable(Guid dataset, string table)
        {
            bool success = false;
            var token = await getAccessToken();
           
            using (var client = new HttpClient { BaseAddress = baseAddress })
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                using (var response = await client.DeleteAsync(
                String.Format("{0}/datasets/{1}/tables/{2}/rows", baseAddress, dataset.ToString(), table)))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    success = true;
                }
            }

            return success;
        }

        /// <summary>
        /// Add rows to a given table and dataset in Power BI
        /// </summary>
        /// <param name="dataset">PowerBIDataset</param>
        /// <param name="table">PowerBITable</param>
        /// <param name="rows">List<Dictionary<string, object>></param>
        /// <returns></returns>
        public static async Task<bool> AddTableRows(Guid dataset, string table, 
            List<Dictionary<string, object>> rows)
        {
            bool success = false;
            var token = await getAccessToken();
            
            using (var client = new HttpClient { BaseAddress = baseAddress })
            {
                //build the json post by looping through the rows and columns for each row
                string json = "{\"rows\": [";
                foreach (var row in rows)
                {
                    //process each column on the row
                    json += "{";
                    foreach (var key in row.Keys)
                    {
                        json += String.Format("\"{0}\":\"{1}\",", key, row[key].ToString());
                    }
                    json = json.Substring(0, json.Length - 1) + "},";
                }

                json = json.Substring(0, json.Length - 1) + "]}";
                var content = new StringContent(json, System.Text.Encoding.Default, "application/json");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                using (var response = await client.PostAsync(
                String.Format("{0}/datasets/{1}/tables/{2}/rows", baseAddress, dataset.ToString(), table), content))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    success = true;
                }
            }

            return success;
        }
    }

    public class PowerBIDataset
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public List<PowerBITable> tables { get; set; }
    }

    public class PowerBIColumn
    {
        public string name { get; set; }
        public string dataType { get; set; }
    }

    public class PowerBITable
    {
        public string name { get; set; }
        public List<PowerBIColumn> columns { get; set; }
    }

    public class PowerBITableRef
    {
        public Guid datasetId { get; set; }
        public string tableName { get; set; }
    }

    public class PowerBITableRows : PowerBITableRef
    {
        public List<Dictionary<string, object>> rows;
    }
}

```
<a name="view"></a>
##Create a Power BI view
A view displays the application's user interface (UI). Typically, this UI is created from the model data. The Power BI web sample implements a simple web page view using JQuery to 

- [Get Datasets](#viewDatasets)
- [Create Dataset](#viewCreate)
- [Add Rows](#viewAdd)
- [Clear Rows](#viewClear)

<a name="viewDatasets"></a>
###Get Datasets


```
        $("#btnGet").click(function () {
            $.ajax({
                url: "/api/PowerBI/GetDatasets",
                type: "GET",
                success: function (data) {

                    $("#alert").html("Get datasets");
                    $("#alert").show();

                    //Loop through data to add items
                    $(data).each(function (i, e) {
                    if (e.name != null)
                        $("#divDataset").append($("<div>", { text: e.name, value: e.id }));
                        });
                    },
                error: function (er) {
                    $("#alert").html("Error retrieving Power BI datasets");
                    $("#alert").show();
                }
            });
        });
```

<a name="viewCreate"></a>
###Create dataset


```
    $("#btnCreate").click(function () {
        var data = {
            name: "TestDataset", tables:
                [{
                    name: "TestTable",
                    columns:
                        [{ name: "Id", dataType: "Int64" },
                        { name: "Description", dataType: "string" },
                        { name: "Created", dataType: "DateTime" }]
                }]
        };
        $.ajax({
            url: "/api/PowerBI/CreateDataset",
            type: "POST",
            data: JSON.stringify(data),
            contentType: "application/json",
            success: function (data) {
                dsID = data;
                $("#alert").html("Create dataset");
                $("#alert").show();
            },
            error: function (er) {
                $("#alert").html("Error retrieving Power BI datasets");
                $("#alert").show();
            }
        });
    });
```


<a name="viewAdd"></a>
###Add table


```
    $("#btnAdd").click(function () {
        var data = {
            datasetId: dsID, tableName: "TestTable",
            rows: [{ "Id": 1, "Description": "Richard", "Created": "1/1/2015" },
                { "Id": 1, "Description": "Richard", "Created": "1/1/2015" }]
        };
        $.ajax({
            url: "/api/PowerBI/AddTableRows",
            type: "POST",
            data: JSON.stringify(data),
            contentType: "application/json",
            success: function (data) {
                $("#alert").html("Table added");
                $("#alert").show();
            },
            error: function (er) {
                $("#alert").html("Error retrieving Power BI datasets");
                $("#alert").show();
            }
        });
    });
```


<a name="viewClear"></a>
###Clear table

```
    $("#btnClear").click(function () {
        var data = { datasetId: dsID, tableName: "TestTable" };
        $.ajax({
            url: "/api/PowerBI/ClearTable",
            type: "POST",
            data: JSON.stringify(data),
            contentType: "application/json",
            success: function (data) {
                $("#alert").html("Table cleared");
                $("#alert").show();
            },
            error: function (er) {
                $("#alert").html("Error retrieving Power BI datasets");
                $("#alert").show();
            }
        });
    });
});
```
<a name="controller"></a>
##Create a Power BI controller
A controller handles user interaction, works with the model, and ultimately selects a view to render the UI. In an MVC application, the view only displays information; the controller handles and responds to user input and interaction. 


```
namespace PowerBIWebApp.Controllers
{
    public class PowerBIController : ApiController
    {
        [HttpGet]
        public async Task<List<PowerBIDataset>> GetDatasets()
        {
            return await PowerBIModel.GetDatasets();
        }

        [HttpGet]
        public async Task<PowerBIDataset> GetDataset(Guid id)
        {
            return await PowerBIModel.GetDataset(id);
        }

        [HttpPost]
        public async Task<Guid> CreateDataset(PowerBIDataset dataset)
        {
            return await PowerBIModel.CreateDataset(dataset);
        }

        [HttpPost]
        public async Task<bool> ClearTable(PowerBITableRef tableRef)
        {
            return await PowerBIModel.ClearTable(tableRef.datasetId, tableRef.tableName);
        }

        [HttpPost]
        public async Task<bool> AddTableRows(PowerBITableRows rows)
        {
            return await PowerBIModel.AddTableRows(rows.datasetId, rows.tableName, rows.rows);
        }
    }
}

```
