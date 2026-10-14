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
const string SampleAppRedirectUri = "http://localhost";

// Get configuration settings
string configJson = File.ReadAllText("appsettings.json");
string environmentUrl = JsonDocument.Parse(configJson)
    .RootElement.GetProperty("DataverseEnvironmentUrl")
    .GetString()!
    .TrimEnd('/');

var tokenProvider = CreateTokenProvider(environmentUrl);

Console.WriteLine("== Sign in to the Web API ==");
await tokenProvider(CancellationToken.None);
using HttpClient webApiClient = GetWebApiClient(environmentUrl, tokenProvider);
await ConfirmWebApiSignInAsync(webApiClient);

Console.WriteLine();
Console.WriteLine("== Sign in to the Organization service ==");
using ServiceClient serviceClient = GetOrganizationServiceClient(environmentUrl, tokenProvider);
ConfirmOrganizationServiceSignIn(serviceClient);

Console.WriteLine();
Console.WriteLine("== Work with the Dataverse Web API ==");

// Create a request through the Web API


// Retrieve the request through the Web API


// Update the request through the Web API


Console.WriteLine();
Console.WriteLine("== Query with FetchXML through the Organization service ==");

// Query devices and requests with FetchXML


// Both API clients use the same MSAL account and in-memory token cache.
static Func<CancellationToken, Task<string>> CreateTokenProvider(string environmentUrl)
{
    IPublicClientApplication app = PublicClientApplicationBuilder.Create(SampleAppId)
        .WithAuthority(AadAuthorityAudience.AzureAdMultipleOrgs)
        .WithRedirectUri(SampleAppRedirectUri)
        .Build();

    string[] scopes = { $"{environmentUrl}/user_impersonation" };
    IAccount? account = null;

    return async cancellationToken =>
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            if (account is not null)
            {
                try
                {
                    var cached = await app.AcquireTokenSilent(scopes, account)
                        .ExecuteAsync(timeout.Token).ConfigureAwait(false);
                    return cached.AccessToken;
                }
                catch (MsalUiRequiredException)
                {
                    // The session needs fresh consent or authentication.
                }
            }

            Console.WriteLine("Complete sign-in in your browser using your Dataverse account (within five minutes).");
            var result = await app.AcquireTokenInteractive(scopes)
                .WithUseEmbeddedWebView(false)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync(timeout.Token).ConfigureAwait(false);
            account = result.Account;
            return result.AccessToken;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Token acquisition didn't complete within five minutes. Run the app again and complete browser sign-in.", ex);
        }
    };
}

static HttpClient GetWebApiClient(string environmentUrl, Func<CancellationToken, Task<string>> tokenProvider)
{
    var client = new HttpClient(new DataverseTokenHandler(tokenProvider))
    {
        BaseAddress = new Uri($"{environmentUrl}/api/data/v9.2/"),
        Timeout = TimeSpan.FromMinutes(2)
    };
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

static ServiceClient GetOrganizationServiceClient(string environmentUrl, Func<CancellationToken, Task<string>> tokenProvider)
{
    return new ServiceClient(new Uri(environmentUrl), _ => tokenProvider(CancellationToken.None));
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

sealed class DataverseTokenHandler(Func<CancellationToken, Task<string>> tokenProvider)
    : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
