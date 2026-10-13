using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Registers the fixture assembly (Build/Fixtures) and the three custom APIs from the foundation spec §4:
// F2 eqp_IsEligibleForReplacement, F3 eqp_ResetLabData and F4 eqp_CheckAvailability.
// Creates what's missing and refreshes the assembly content, so it can be rerun after every code change.
internal sealed class FixtureRegistrar(ServiceClient client, string assemblyPath)
{
    private const string Ns = "EquipmentProvisioning.Fixtures";

    // Custom API enumerations
    private const int GlobalBinding = 0;
    private const int StepTypeNone = 0, StepTypeAsyncOnly = 1;
    private const int TypeBoolean = 0, TypeString = 10, TypeGuid = 12;

    public void Run()
    {
        var assemblyId = EnsureAssembly();
        var eligibleType = EnsurePluginType(assemblyId, $"{Ns}.IsEligibleForReplacement", "Is eligible for replacement");
        var resetType = EnsurePluginType(assemblyId, $"{Ns}.ResetLabData", "Reset lab data");
        var availabilityType = EnsurePluginType(assemblyId, $"{Ns}.CheckAvailability", "Check availability");

        // Async Only so the API can be cataloged as a business event in lab 01 (foundation J-3).
        var eligible = EnsureCustomApi("eqp_IsEligibleForReplacement", "Is eligible for replacement",
            "Checks the replacement eligibility rule for one device: eligible if damaged beyond repair, if it has no issue date, or if it was issued at least 24 months ago.",
            StepTypeAsyncOnly, eligibleType);
        EnsureRequestParameter(eligible, "eqp_IsEligibleForReplacement", "DeviceId", "Device ID", "The device being replaced.", TypeGuid);
        EnsureResponseProperty(eligible, "eqp_IsEligibleForReplacement", "IsEligible", "Is eligible", "True if the device can be replaced now.", TypeBoolean);

        var reset = EnsureCustomApi("eqp_ResetLabData", "Reset lab data",
            "Creates or restores the lab seed rows with dates relative to today, and links EMP-001 to the caller. Never deletes rows.",
            StepTypeNone, resetType);
        EnsureResponseProperty(reset, "eqp_ResetLabData", "Summary", "Summary", "What was reset.", TypeString);

        var availability = EnsureCustomApi("eqp_CheckAvailability", "Check availability",
            "Stand-in for an external availability service. Controlled by the eqp_AvailabilityMode environment variable: Available, Unavailable or Error (HTTP 503).",
            StepTypeNone, availabilityType);
        EnsureRequestParameter(availability, "eqp_CheckAvailability", "RequestId", "Request ID", "The replacement request to check.", TypeGuid);
        EnsureResponseProperty(availability, "eqp_CheckAvailability", "IsAvailable", "Is available", "True if a replacement device is available.", TypeBoolean);

        Log("done", "fixtures registered");
    }

    private Guid EnsureAssembly()
    {
        var path = Path.GetFullPath(assemblyPath);
        var name = System.Reflection.AssemblyName.GetAssemblyName(path);
        var content = Convert.ToBase64String(File.ReadAllBytes(path));
        var existing = FindOne("pluginassembly", "name", name.Name!);
        if (existing is not null)
        {
            client.Update(new Entity("pluginassembly", existing.Id) { ["content"] = content, ["version"] = name.Version!.ToString() });
            Log("update", $"assembly {name.Name} {name.Version} (content refreshed)");
            return existing.Id;
        }
        var id = CreateInSolution(new Entity("pluginassembly")
        {
            ["name"] = name.Name,
            ["content"] = content,
            ["isolationmode"] = new OptionSetValue(2), // Sandbox
            ["sourcetype"] = new OptionSetValue(0), // Database
            ["culture"] = "neutral",
            ["version"] = name.Version!.ToString(),
            ["publickeytoken"] = Convert.ToHexString(name.GetPublicKeyToken()!).ToLowerInvariant(),
        });
        Log("create", $"assembly {name.Name} {name.Version}");
        return id;
    }

    private Guid EnsurePluginType(Guid assemblyId, string typeName, string friendlyName)
    {
        var existing = FindOne("plugintype", "typename", typeName);
        if (existing is not null)
        {
            Log("exists", $"plug-in type {typeName}");
            return existing.Id;
        }
        var id = client.Create(new Entity("plugintype")
        {
            ["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId),
            ["typename"] = typeName,
            ["name"] = typeName,
            ["friendlyname"] = friendlyName,
        });
        Log("create", $"plug-in type {typeName}");
        return id;
    }

    private Guid EnsureCustomApi(string uniqueName, string displayName, string description, int stepType, Guid pluginTypeId)
    {
        var existing = FindOne("customapi", "uniquename", uniqueName);
        if (existing is not null)
        {
            // Binding, function/action and step type can't change after creation; keep the rest in sync.
            client.Update(new Entity("customapi", existing.Id)
            {
                ["displayname"] = displayName,
                ["description"] = description,
                ["plugintypeid"] = new EntityReference("plugintype", pluginTypeId),
            });
            Log("exists", $"custom API {uniqueName}");
            return existing.Id;
        }
        var id = CreateInSolution(new Entity("customapi")
        {
            ["uniquename"] = uniqueName,
            ["name"] = uniqueName,
            ["displayname"] = displayName,
            ["description"] = description,
            ["bindingtype"] = new OptionSetValue(GlobalBinding),
            ["isfunction"] = false,
            ["isprivate"] = false,
            ["allowedcustomprocessingsteptype"] = new OptionSetValue(stepType),
            ["plugintypeid"] = new EntityReference("plugintype", pluginTypeId),
        });
        Log("create", $"custom API {uniqueName}");
        return id;
    }

    private void EnsureRequestParameter(Guid apiId, string apiName, string uniqueName, string displayName, string description, int type)
    {
        if (FindChild("customapirequestparameter", apiId, uniqueName) is not null)
        {
            Log("exists", $"parameter {apiName}.{uniqueName}");
            return;
        }
        CreateInSolution(new Entity("customapirequestparameter")
        {
            ["customapiid"] = new EntityReference("customapi", apiId),
            ["uniquename"] = uniqueName,
            ["name"] = $"{apiName}.{uniqueName}",
            ["displayname"] = displayName,
            ["description"] = description,
            ["type"] = new OptionSetValue(type),
            ["isoptional"] = false,
        });
        Log("create", $"parameter {apiName}.{uniqueName}");
    }

    private void EnsureResponseProperty(Guid apiId, string apiName, string uniqueName, string displayName, string description, int type)
    {
        if (FindChild("customapiresponseproperty", apiId, uniqueName) is not null)
        {
            Log("exists", $"response {apiName}.{uniqueName}");
            return;
        }
        CreateInSolution(new Entity("customapiresponseproperty")
        {
            ["customapiid"] = new EntityReference("customapi", apiId),
            ["uniquename"] = uniqueName,
            ["name"] = $"{apiName}.{uniqueName}",
            ["displayname"] = displayName,
            ["description"] = description,
            ["type"] = new OptionSetValue(type),
        });
        Log("create", $"response {apiName}.{uniqueName}");
    }

    private Guid CreateInSolution(Entity entity)
    {
        var request = new Microsoft.Xrm.Sdk.Messages.CreateRequest { Target = entity };
        request.Parameters["SolutionUniqueName"] = SchemaBuilder.SolutionName;
        return ((Microsoft.Xrm.Sdk.Messages.CreateResponse)client.Execute(request)).id;
    }

    private Entity? FindOne(string table, string column, string value)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition(column, ConditionOperator.Equal, value);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    private Entity? FindChild(string table, Guid apiId, string uniqueName)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition("customapiid", ConditionOperator.Equal, apiId);
        query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
