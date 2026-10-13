using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EquipmentProvisioning.Fixtures
{
    // Main operation of the eqp_CheckAvailability custom API (F4), a stand-in for an external
    // availability service. Input: RequestId (Guid). Output: IsAvailable (Boolean).
    // The eqp_AvailabilityMode environment variable controls the result:
    // Available (default) returns true, Unavailable returns false, Error fails with HTTP 503 so a
    // cloud flow's retry policy treats it as a transient failure.
    public sealed class CheckAvailability : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            if (!context.InputParameters.TryGetValue("RequestId", out var raw) || !(raw is Guid requestId) || requestId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("RequestId is required.");
            }

            try
            {
                service.Retrieve("eqp_request", requestId, new ColumnSet("eqp_requesteddevicetype"));
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                throw new InvalidPluginExecutionException($"Request {requestId} wasn't found, or you don't have permission to read it.");
            }

            var mode = EnvironmentVariables.GetString(service, "eqp_AvailabilityMode") ?? "Available";
            switch (mode.Trim().ToLowerInvariant())
            {
                case "error":
                    throw new InvalidPluginExecutionException(
                        "The availability service is temporarily unavailable. Try again later.",
                        PluginHttpStatusCode.ServiceUnavailable);
                case "unavailable":
                    context.OutputParameters["IsAvailable"] = false;
                    break;
                default:
                    context.OutputParameters["IsAvailable"] = true;
                    break;
            }
        }
    }
}
