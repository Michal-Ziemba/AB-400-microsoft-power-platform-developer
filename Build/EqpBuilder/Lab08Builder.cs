using System.Text.Json.Nodes;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Builds F8, the lab 08 solution EquipmentProvisioningLab08 (unmanaged, because learners edit the flows):
//   - "Lab 08 approval flow": the deliberately naive parent flow. Its trigger fires on every change to a
//     request, the availability call has no error handling, and the fulfilment steps sit inline.
//   - "Lab 08 fulfilment child flow": the child-flow scaffold with a correct manual trigger, ready for
//     Run a Child Flow.
//   - Two Dataverse connection references, so only the parent's connection moves to the service principal.
// Flows are created off; they're switched on only when a connection is bound (--connection <name>).
internal sealed class Lab08Builder(ServiceClient client, string repoRoot, string? connectionName)
{
    public const string SolutionName = "EquipmentProvisioningLab08";
    private const string ConnectorId = "/providers/Microsoft.PowerApps/apis/shared_commondataserviceforapps";
    private const string ParentReference = "eqp_lab08dataverse_parent";
    private const string ChildReference = "eqp_lab08dataverse_child";
    private const string ParentFlow = "Lab 08 approval flow";
    private const string ChildFlow = "Lab 08 fulfilment child flow";

    private const int Approved = 100000001, InFulfillment = 100000003;
    private const int NotChecked = 100000000, Available = 100000001, Unavailable = 100000002;

    public void Run()
    {
        SolutionHelper.EnsureSolution(client, SolutionName, "Equipment Provisioning lab 08",
            "AB-400 lab 08: the naive approval flow, the fulfilment child-flow scaffold and their connection references. Unmanaged: learners edit the flows.");
        var parentRef = EnsureConnectionReference(ParentReference, "Lab 08 Dataverse (approval flow)");
        var childRef = EnsureConnectionReference(ChildReference, "Lab 08 Dataverse (child flow)");

        var childId = EnsureFlow(ChildFlow, "Lab 08: fulfilment steps, called from the approval flow with Run a Child Flow.",
            ClientData(ChildReference, ChildDefinition()));
        var parentId = EnsureFlow(ParentFlow, "Lab 08: naive approval flow. Fires on every change, has no error handling, and runs fulfilment inline.",
            ClientData(ParentReference, ParentDefinition()));

        if (!string.IsNullOrEmpty(connectionName))
        {
            Bind(parentRef, connectionName);
            Bind(childRef, connectionName);
            SetState(childId, ChildFlow, on: true);
            SetState(parentId, ParentFlow, on: true);
        }

        new SolutionExporter(client, SolutionName,
            Path.Combine(repoRoot, "Allfiles", "08-resilient-cloud-flow", $"{SolutionName}.zip"),
            Path.Combine(repoRoot, "Build", "Solutions", $"{SolutionName}.zip"), null).RunUnmanagedOnly();
    }

    private Guid EnsureConnectionReference(string logicalName, string displayName)
    {
        var existing = SolutionHelper.FindOne(client, "connectionreference", "connectionreferencelogicalname", logicalName);
        if (existing is not null)
        {
            Log("exists", $"connection reference {logicalName}");
            return existing.Id;
        }
        var id = SolutionHelper.CreateInSolution(client, SolutionName, new Entity("connectionreference")
        {
            ["connectionreferencelogicalname"] = logicalName,
            ["connectionreferencedisplayname"] = displayName,
            ["connectorid"] = ConnectorId,
            ["description"] = "Microsoft Dataverse connection used by lab 08.",
        });
        Log("create", $"connection reference {logicalName}");
        return id;
    }

    private Guid EnsureFlow(string name, string description, string clientData)
    {
        var query = new QueryExpression("workflow") { ColumnSet = new ColumnSet("statecode"), TopCount = 1 };
        query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
        query.Criteria.AddCondition("category", ConditionOperator.Equal, 5);
        var existing = client.RetrieveMultiple(query).Entities.FirstOrDefault();
        if (existing is not null)
        {
            // A flow must be off to change its definition.
            if (existing.GetAttributeValue<OptionSetValue>("statecode")?.Value == 1)
            {
                SetState(existing.Id, name, on: false);
            }
            client.Update(new Entity("workflow", existing.Id) { ["clientdata"] = clientData, ["description"] = description });
            Log("update", $"flow {name}");
            return existing.Id;
        }
        var id = SolutionHelper.CreateInSolution(client, SolutionName, new Entity("workflow")
        {
            ["category"] = new OptionSetValue(5), // Modern flow
            ["type"] = new OptionSetValue(1), // Definition
            ["primaryentity"] = "none",
            ["name"] = name,
            ["description"] = description,
            ["clientdata"] = clientData,
        });
        Log("create", $"flow {name}");
        return id;
    }

    private void Bind(Guid referenceId, string connection)
    {
        client.Update(new Entity("connectionreference", referenceId) { ["connectionid"] = connection });
        Log("set", $"connection reference bound to {connection}");
    }

    private void SetState(Guid flowId, string name, bool on)
    {
        client.Update(new Entity("workflow", flowId)
        {
            ["statecode"] = new OptionSetValue(on ? 1 : 0),
            ["statuscode"] = new OptionSetValue(on ? 2 : 1),
        });
        Log("set", $"flow {name} {(on ? "on" : "off")}");
    }

    private static string ClientData(string referenceName, JsonObject definition) => new JsonObject
    {
        ["properties"] = new JsonObject
        {
            ["connectionReferences"] = new JsonObject
            {
                ["shared_commondataserviceforapps"] = new JsonObject
                {
                    ["runtimeSource"] = "embedded",
                    ["connection"] = new JsonObject { ["connectionReferenceLogicalName"] = referenceName },
                    ["api"] = new JsonObject { ["name"] = "shared_commondataserviceforapps" },
                },
            },
            ["definition"] = definition,
        },
        ["schemaVersion"] = "1.0.0.0",
    }.ToJsonString();

    private static JsonObject Definition(JsonObject triggers, JsonObject actions) => new()
    {
        ["$schema"] = "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
        ["contentVersion"] = "1.0.0.0",
        ["parameters"] = new JsonObject
        {
            ["$connections"] = new JsonObject { ["defaultValue"] = new JsonObject(), ["type"] = "Object" },
            ["$authentication"] = new JsonObject { ["defaultValue"] = new JsonObject(), ["type"] = "SecureObject" },
        },
        ["triggers"] = triggers,
        ["actions"] = actions,
    };

    private static JsonObject Dataverse(string operationId, JsonObject parameters, JsonObject? runAfter = null) => new()
    {
        ["runAfter"] = runAfter ?? new JsonObject(),
        ["type"] = "OpenApiConnection",
        ["inputs"] = new JsonObject
        {
            ["host"] = new JsonObject
            {
                ["apiId"] = ConnectorId,
                ["connectionName"] = "shared_commondataserviceforapps",
                ["operationId"] = operationId,
            },
            ["parameters"] = parameters,
            ["authentication"] = "@parameters('$authentication')",
        },
    };

    private static JsonObject After(string action) => new() { [action] = new JsonArray("Succeeded") };

    private static JsonObject WithoutRetries(JsonObject action)
    {
        ((JsonObject)action["inputs"]!)["retryPolicy"] = new JsonObject { ["type"] = "none" };
        return action;
    }

    // The naive flow, with the three defects the lab fixes: no trigger filter (it runs for every change and
    // relies on a condition to skip), no error handling around the availability call, and fulfilment inline.
    private static JsonObject ParentDefinition()
    {
        var requestId = "@triggerOutputs()?['body/eqp_requestid']";
        var triggers = new JsonObject
        {
            ["When_a_request_is_modified"] = new JsonObject
            {
                ["type"] = "OpenApiConnectionWebhook",
                ["inputs"] = new JsonObject
                {
                    ["host"] = new JsonObject
                    {
                        ["apiId"] = ConnectorId,
                        ["connectionName"] = "shared_commondataserviceforapps",
                        ["operationId"] = "SubscribeWebhookTrigger",
                    },
                    ["parameters"] = new JsonObject
                    {
                        ["subscriptionRequest/message"] = 3, // Modified
                        ["subscriptionRequest/entityname"] = "eqp_request",
                        ["subscriptionRequest/scope"] = 4, // Organization
                    },
                    ["authentication"] = "@parameters('$authentication')",
                },
            },
        };
        var actions = new JsonObject
        {
            ["Is_newly_approved"] = new JsonObject
            {
                ["runAfter"] = new JsonObject(),
                ["type"] = "If",
                ["expression"] = new JsonObject
                {
                    ["and"] = new JsonArray(
                        new JsonObject { ["equals"] = new JsonArray("@triggerOutputs()?['body/eqp_requeststatus']", Approved) },
                        new JsonObject { ["equals"] = new JsonArray("@triggerOutputs()?['body/eqp_availability']", NotChecked) }),
                },
                ["actions"] = new JsonObject
                {
                    // Retries are off in the naive flow, so a failure shows straight away; learners add a
                    // short, bounded retry policy in the lab.
                    ["Check_availability"] = WithoutRetries(Dataverse("PerformUnboundAction", new JsonObject
                    {
                        ["actionName"] = "eqp_CheckAvailability",
                        ["item/RequestId"] = requestId,
                    })),
                    ["Record_availability"] = Dataverse("UpdateRecord", new JsonObject
                    {
                        ["entityName"] = "eqp_requests",
                        ["recordId"] = requestId,
                        ["item/eqp_availability"] = $"@if(equals(outputs('Check_availability')?['body/IsAvailable'], true), {Available}, {Unavailable})",
                        ["item/eqp_availabilitycheckedon"] = "@utcNow()",
                    }, After("Check_availability")),
                    ["Start_fulfilment"] = Dataverse("UpdateRecord", new JsonObject
                    {
                        ["entityName"] = "eqp_requests",
                        ["recordId"] = requestId,
                        ["item/eqp_requeststatus"] = InFulfillment,
                        ["item/eqp_processingnote"] = "@concat('Fulfilment started on ', utcNow(), '.')",
                    }, After("Record_availability")),
                },
                ["else"] = new JsonObject { ["actions"] = new JsonObject() },
            },
        };
        return Definition(triggers, actions);
    }

    // The child-flow scaffold: a manual trigger with a Request ID input, the fulfilment steps, and a response.
    private static JsonObject ChildDefinition()
    {
        var triggers = new JsonObject
        {
            ["manual"] = new JsonObject
            {
                ["type"] = "Request",
                ["kind"] = "Button",
                ["inputs"] = new JsonObject
                {
                    ["schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["text"] = new JsonObject
                            {
                                ["title"] = "Request ID",
                                ["type"] = "string",
                                ["x-ms-dynamically-added"] = true,
                                ["description"] = "The ID of the request to fulfil",
                                ["x-ms-content-hint"] = "TEXT",
                            },
                        },
                        ["required"] = new JsonArray("text"),
                    },
                },
            },
        };
        var actions = new JsonObject
        {
            ["Start_fulfilment"] = Dataverse("UpdateRecord", new JsonObject
            {
                ["entityName"] = "eqp_requests",
                ["recordId"] = "@triggerBody()['text']",
                ["item/eqp_requeststatus"] = InFulfillment,
                ["item/eqp_processingnote"] = "@concat('Fulfilment started on ', utcNow(), ' by the child flow.')",
            }),
            ["Respond_to_the_parent_flow"] = new JsonObject
            {
                ["runAfter"] = After("Start_fulfilment"),
                ["type"] = "Response",
                ["kind"] = "PowerApp",
                ["inputs"] = new JsonObject
                {
                    ["statusCode"] = 200,
                    ["body"] = new JsonObject { ["result"] = "Fulfilment started" },
                    ["schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["result"] = new JsonObject { ["title"] = "Result", ["x-ms-dynamically-added"] = true, ["type"] = "string" },
                        },
                    },
                },
            },
        };
        return Definition(triggers, actions);
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
