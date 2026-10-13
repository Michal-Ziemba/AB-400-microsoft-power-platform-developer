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
        using var serviceClient = DataverseConnection.CreateServiceClient(environmentUrl);

        // Find the request by its request number
        var keys = new KeyAttributeCollection { { "eqp_name", requestNumber } };
        var retrieveRequest = new RetrieveRequest
        {
            Target = new EntityReference("eqp_request", keys),
            ColumnSet = new ColumnSet("eqp_requeststatus", "eqp_availability")
        };
        var request = ((RetrieveResponse)serviceClient.Execute(retrieveRequest)).Entity;

        // Simulate the slow external availability service
        var isAvailable = await SlowAvailabilityService.CheckAsync(requestNumber);

        // Update the request's status and availability columns
        var update = new Entity("eqp_request", request.Id);
        if (isAvailable)
        {
            update["eqp_requeststatus"] = new OptionSetValue(100000003); // In fulfillment
            update["eqp_availability"] = new OptionSetValue(100000001); // Available
            update["eqp_processingnote"] = $"Availability confirmed by the Azure Function on {DateTime.UtcNow:u}.";
        }
        else
        {
            update["eqp_availability"] = new OptionSetValue(100000002); // Unavailable
            update["eqp_processingnote"] = $"The requested device type isn't currently available (checked {DateTime.UtcNow:u}).";
        }
        update["eqp_availabilitycheckedon"] = DateTime.UtcNow;
        serviceClient.Update(update);

        _logger.LogInformation("Finished processing request {RequestNumber}.", requestNumber);
        return new OkObjectResult($"Finished checking availability for {requestNumber}.");
    }
}
