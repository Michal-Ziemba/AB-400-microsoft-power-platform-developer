using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace EquipmentRequestFunctions;

/// <summary>
/// An HTTP-triggered function that stands in for an event-driven caller (such as a business event
/// subscriber). It checks the slow external availability service for one request and, once the result
/// comes back, updates the request in Dataverse so fulfilment can continue.
/// </summary>
public class CheckRequestAvailability
{
    private readonly ILogger<CheckRequestAvailability> _logger;

    public CheckRequestAvailability(ILogger<CheckRequestAvailability> logger)
    {
        _logger = logger;
    }

    [Function("CheckRequestAvailability")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        var requestNumber = req.Query["requestNumber"].ToString();

        if (string.IsNullOrWhiteSpace(requestNumber))
        {
            return new BadRequestObjectResult(
                "Provide a requestNumber query string value, for example ?requestNumber=REQ-SEED-004.");
        }

        var environmentUrl = Environment.GetEnvironmentVariable("DATAVERSE_ENVIRONMENT_URL");
        if (string.IsNullOrWhiteSpace(environmentUrl))
        {
            return new ObjectResult("The DATAVERSE_ENVIRONMENT_URL application setting isn't configured.")
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }

        _logger.LogInformation("Checking availability for request {RequestNumber}.", requestNumber);

        // Connect to Dataverse by using the function app's managed identity


        // Find the request by its request number


        // Simulate the slow external availability service


        // Update the request's status and availability columns


        _logger.LogInformation("Finished processing request {RequestNumber}.", requestNumber);
        return new OkObjectResult($"Finished checking availability for {requestNumber}.");
    }
}
