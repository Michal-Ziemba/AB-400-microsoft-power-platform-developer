using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EquipmentProvisioning.Fixtures
{
    // Main operation of the eqp_IsEligibleForReplacement custom API (F2).
    // Input: DeviceId (Guid). Output: IsEligible (Boolean). Runs as the caller, so Dataverse security applies.
    public sealed class IsEligibleForReplacement : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            if (!context.InputParameters.TryGetValue("DeviceId", out var raw) || !(raw is Guid deviceId) || deviceId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("DeviceId is required.");
            }

            Entity device;
            try
            {
                device = service.Retrieve("eqp_device", deviceId, new ColumnSet("eqp_issuedon", "eqp_damagedbeyondrepair"));
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                throw new InvalidPluginExecutionException($"Device {deviceId} wasn't found, or you don't have permission to read it.");
            }

            context.OutputParameters["IsEligible"] = EligibilityRule.IsEligible(
                device.GetAttributeValue<DateTime?>("eqp_issuedon"),
                device.GetAttributeValue<bool>("eqp_damagedbeyondrepair"),
                DateTime.UtcNow);
        }
    }
}
