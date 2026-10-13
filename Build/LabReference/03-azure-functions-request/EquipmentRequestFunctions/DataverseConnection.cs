using Azure.Core;
using Azure.Identity;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace EquipmentRequestFunctions;

/// <summary>
/// Creates the shared Dataverse connection used by both functions in this project. Both the HTTP-triggered
/// function and the supplied timer-triggered function call <see cref="CreateServiceClient"/>, so completing
/// this one method wires up Dataverse access for both of them.
/// </summary>
public static class DataverseConnection
{
    public static ServiceClient CreateServiceClient(string environmentUrl)
    {
        var instanceUri = new Uri(environmentUrl);

        // Acquire tokens from the function app's managed identity
        var credential = new DefaultAzureCredential();

        return new ServiceClient(instanceUri, async (string resource) =>
        {
            // ServiceClient passes the full Organization service URL here (including the
            // XRMServices path and an SDKClientVersion query string), not a bare origin, so
            // building a scope directly from it produces an invalid "resource/.default" scope
            // string. Extract just the scheme and host before appending the default scope.
            var audience = new Uri(resource).GetLeftPart(UriPartial.Authority);
            var tokenRequestContext = new TokenRequestContext(new[] { $"{audience}/.default" });
            var token = await credential.GetTokenAsync(tokenRequestContext);
            return token.Token;
        });
    }
}
