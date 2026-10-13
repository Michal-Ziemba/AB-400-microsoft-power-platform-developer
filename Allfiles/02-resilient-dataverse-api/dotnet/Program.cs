// Resilient Dataverse API exercise - Dataverse SDK for .NET.
//
// Uses two client surfaces against the same environment:
//  - A raw Web API client (MSAL + HttpClient) for create, retrieve, and update.
//  - The Organization service (ServiceClient) for a FetchXML query.
//
// Both use Microsoft's published sample AppId, which needs no app registration,
// client secret, or admin consent step. See the exercise instructions for details.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

// Microsoft's published sample AppId and redirect URI, for development and
// prototyping purposes only (see Microsoft Learn: "Use connection strings in
// XRM tooling to connect to Microsoft Dataverse").
const string SampleAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
const string SampleAppRedirectUri = "app://58145b91-0c36-4500-8554-080854f2ac97";

// Get configuration settings
string configJson = File.ReadAllText("appsettings.json");
string environmentUrl = JsonDocument.Parse(configJson)
    .RootElement.GetProperty("DataverseEnvironmentUrl")
    .GetString()!
    .TrimEnd('/');

Console.WriteLine("== Sign in to the Web API ==");
using HttpClient webApiClient = await GetWebApiClientAsync(environmentUrl);
await ConfirmWebApiSignInAsync(webApiClient);

Console.WriteLine();
Console.WriteLine("== Sign in to the Organization service ==");
using ServiceClient serviceClient = GetOrganizationServiceClient(environmentUrl);
ConfirmOrganizationServiceSignIn(serviceClient);

Console.WriteLine();
Console.WriteLine("== Work with the Dataverse Web API ==");

// Create a request through the Web API


// Retrieve the request through the Web API


// Update the request through the Web API


Console.WriteLine();
Console.WriteLine("== Query with FetchXML through the Organization service ==");

// Query devices and requests with FetchXML


// Acquires a Web API access token interactively and configures an HttpClient to call it.
static async Task<HttpClient> GetWebApiClientAsync(string environmentUrl)
{
    IPublicClientApplication app = PublicClientApplicationBuilder.Create(SampleAppId)
        .WithAuthority(AadAuthorityAudience.AzureAdMultipleOrgs)
        .WithRedirectUri("http://localhost")
        .Build();

    string[] scopes = { $"{environmentUrl}/user_impersonation" };
    AuthenticationResult token = await app.AcquireTokenInteractive(scopes).ExecuteAsync();

    var client = new HttpClient
    {
        BaseAddress = new Uri($"{environmentUrl}/api/data/v9.2/"),
        Timeout = TimeSpan.FromMinutes(2)
    };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
    client.DefaultRequestHeaders.Add("OData-Version", "4.0");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return client;
}

// Calls WhoAmI through the raw Web API to confirm the sign-in succeeded.
static async Task ConfirmWebApiSignInAsync(HttpClient client)
{
    HttpResponseMessage response = await client.GetAsync("WhoAmI");
    response.EnsureSuccessStatusCode();
    string json = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Web API sign-in succeeded. {json}");
}

// Connects to the Organization service with an interactive OAuth sign-in.
static ServiceClient GetOrganizationServiceClient(string environmentUrl)
{
    string connectionString =
        $"AuthType=OAuth;" +
        $"Url={environmentUrl};" +
        $"AppId={SampleAppId};" +
        $"RedirectUri={SampleAppRedirectUri};" +
        $"LoginPrompt=Auto;" +
        $"RequireNewInstance=True";

    return new ServiceClient(connectionString);
}

// Calls WhoAmI through the Organization service to confirm the sign-in succeeded.
static void ConfirmOrganizationServiceSignIn(ServiceClient serviceClient)
{
    if (!serviceClient.IsReady)
    {
        throw new InvalidOperationException($"Couldn't connect to the Organization service: {serviceClient.LastError}");
    }

    var response = (WhoAmIResponse)serviceClient.Execute(new WhoAmIRequest());
    Console.WriteLine($"Organization service sign-in succeeded. User ID: {response.UserId}");
}
