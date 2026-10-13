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


    }
}
