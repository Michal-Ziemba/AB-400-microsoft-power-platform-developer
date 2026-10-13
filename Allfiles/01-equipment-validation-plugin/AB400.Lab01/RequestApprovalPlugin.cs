using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace AB400.Lab01
{
    // Enforces R1 (replacement eligibility) when a request's status changes to Approved.
    //
    // Registration (use the Plug-in Registration Tool):
    //   Message:             Update
    //   Primary Entity:      eqp_request
    //   Event Pipeline Stage: PreOperation
    //   Execution Mode:      Synchronous
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


            // Retrieve the device being replaced


            // Apply the replacement-eligibility rule


            // Block the approval if the device isn't eligible

        }
    }
}
