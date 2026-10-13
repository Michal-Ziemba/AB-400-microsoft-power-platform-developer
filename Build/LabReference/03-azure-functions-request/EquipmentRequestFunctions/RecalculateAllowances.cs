using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EquipmentRequestFunctions;

/// <summary>
/// A scheduled function that recalculates every allowance's used and remaining device counts from the
/// requests that are actually Fulfilled. This function is complete: it ships ready to deploy and run, so
/// this exercise focuses its hands-on time on the event-driven function above.
/// </summary>
public class RecalculateAllowances
{
    private readonly ILogger<RecalculateAllowances> _logger;

    public RecalculateAllowances(ILogger<RecalculateAllowances> logger)
    {
        _logger = logger;
    }

    // Runs once a day at 02:00 UTC. You can also run it on demand from the Azure portal's test pane
    // instead of waiting for the schedule.
    [Function("RecalculateAllowances")]
    public void Run([TimerTrigger("0 0 2 * * *")] TimerInfo timer)
    {
        var environmentUrl = Environment.GetEnvironmentVariable("DATAVERSE_ENVIRONMENT_URL");
        if (string.IsNullOrWhiteSpace(environmentUrl))
        {
            _logger.LogError("The DATAVERSE_ENVIRONMENT_URL application setting isn't configured.");
            return;
        }

        using var serviceClient = DataverseConnection.CreateServiceClient(environmentUrl);

        var allowanceQuery = new QueryExpression("eqp_allowance")
        {
            ColumnSet = new ColumnSet("eqp_employee", "eqp_devicesallowed")
        };
        var allowances = serviceClient.RetrieveMultiple(allowanceQuery).Entities;

        foreach (var allowance in allowances)
        {
            var employee = allowance.GetAttributeValue<EntityReference>("eqp_employee");
            if (employee is null)
            {
                continue;
            }

            var fulfilledQuery = new QueryExpression("eqp_request")
            {
                ColumnSet = new ColumnSet(),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("eqp_employee", ConditionOperator.Equal, employee.Id),
                        new ConditionExpression("eqp_requeststatus", ConditionOperator.Equal, 100000004) // Fulfilled
                    }
                }
            };
            var usedCount = serviceClient.RetrieveMultiple(fulfilledQuery).Entities.Count;
            var allowedCount = allowance.GetAttributeValue<int>("eqp_devicesallowed");

            var update = new Entity("eqp_allowance", allowance.Id)
            {
                ["eqp_devicesused"] = usedCount,
                ["eqp_remaining"] = allowedCount - usedCount,
                ["eqp_lastrecalculatedon"] = DateTime.UtcNow
            };
            serviceClient.Update(update);

            _logger.LogInformation(
                "Recalculated allowance {AllowanceId}: {Used} used, {Remaining} remaining.",
                allowance.Id, usedCount, allowedCount - usedCount);
        }
    }
}
