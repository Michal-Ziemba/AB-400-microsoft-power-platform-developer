using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace AB400.Lab01
{
    // Completed reference for Lab 01. Enforces R1 (replacement eligibility, foundation
    // spec §3) when a request's status changes to Approved.
    //
    // Registration (Plug-in Registration Tool):
    //   Message:              Update
    //   Primary Entity:       eqp_request
    //   Event Pipeline Stage: PreOperation
    //   Execution Mode:       Synchronous
    //   Filtering Attributes: eqp_requeststatus
    //   Pre-image ("PreImage"): eqp_requeststatus, eqp_currentdevice
    public class RequestApprovalPlugin : IPlugin
    {
        private const int StatusApproved = 100000001;

        public void Execute(IServiceProvider serviceProvider)
        {
            // Get the execution context and organization service
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            // Confirm the request is being approved (read the Target and the pre-image)
            var target = (Entity)context.InputParameters["Target"];
            if (!target.Attributes.Contains("eqp_requeststatus"))
            {
                // The filtering attribute guarantees this never happens in practice, but
                // guard against being invoked for an unrelated update.
                return;
            }

            var newStatus = ((OptionSetValue)target["eqp_requeststatus"]).Value;
            if (newStatus != StatusApproved)
            {
                // Some other status transition (for example, to Rejected). R1 only applies to approval.
                return;
            }

            var preImage = context.PreEntityImages["PreImage"];
            var previousStatus = ((OptionSetValue)preImage["eqp_requeststatus"]).Value;
            if (previousStatus == StatusApproved)
            {
                // The request was already Approved; this save didn't change the status,
                // so there's no new approval transition to validate.
                return;
            }

            // Retrieve the device being replaced
            var deviceRef = preImage.GetAttributeValue<EntityReference>("eqp_currentdevice");

            Entity device;
            try
            {
                device = service.Retrieve("eqp_device", deviceRef.Id, new ColumnSet("eqp_issuedon", "eqp_damagedbeyondrepair"));
            }
            catch (FaultException<OrganizationServiceFault>)
            {
                throw new InvalidPluginExecutionException("The device being replaced couldn't be found.");
            }

            var issuedOn = device.GetAttributeValue<DateTime?>("eqp_issuedon");
            var damagedBeyondRepair = device.GetAttributeValue<bool>("eqp_damagedbeyondrepair");

            // Apply the replacement-eligibility rule
            var isEligible = damagedBeyondRepair
                || !issuedOn.HasValue
                || issuedOn.Value.Date <= DateTime.UtcNow.Date.AddMonths(-24);

            // Block the approval if the device isn't eligible
            if (!isEligible)
            {
                var eligibleFrom = issuedOn.Value.Date.AddMonths(24);
                throw new InvalidPluginExecutionException(
                    $"This device was issued on {issuedOn.Value:yyyy-MM-dd}. It can be replaced from {eligibleFrom:yyyy-MM-dd}, or earlier if it's flagged as damaged beyond repair.");
            }
        }
    }
}
