using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// One-off live check for Instructions/Labs/01-equipment-validation-plugin.md (not part of the shipped solution).
// Registers the real, learner-written plug-in assembly against a throwaway employee/device/request trio,
// confirms the bypass the lab describes is now blocked (and a legitimate approval still succeeds), then
// unregisters everything and deletes the temporary rows. Leaves Dev One exactly as it found it.
internal sealed class Lab01PluginVerifier(ServiceClient client, string assemblyPath)
{
    private int _failures;
    private readonly List<(string Table, Guid Id)> _created = [];

    public int Run()
    {
        Console.WriteLine("Registering the compiled lab 01 plug-in...");
        var assemblyId = CreateAssembly();
        var pluginTypeId = CreatePluginType(assemblyId);
        var stepId = CreateStep(pluginTypeId);
        var imageId = CreateImage(stepId);

        try
        {
            Console.WriteLine("Creating a throwaway employee, two devices, and two requests...");
            var (employeeId, ineligibleDeviceId, eligibleDeviceId, ineligibleRequestId, eligibleRequestId) = CreateTestData();

            // Mirrors the lab's own bypass: a direct Update of eqp_requeststatus, no form involved.
            var blocked = TryApprove(ineligibleRequestId, out var faultMessage);
            Check("The plug-in blocks approving a 1-month-old, undamaged device", !blocked, faultMessage);
            Check("The fault message names the issue date and an eligible-from date",
                faultMessage.Contains("was issued on") && faultMessage.Contains("can be replaced from"), faultMessage);

            var allowed = TryApprove(eligibleRequestId, out var okMessage);
            Check("The plug-in still allows approving a 30-month-old device", allowed, okMessage);
        }
        finally
        {
            Console.WriteLine("Deleting the temporary rows...");
            // Reverse order: requests reference devices and the employee.
            foreach (var (table, id) in Enumerable.Reverse(_created))
            {
                try { client.Delete(table, id); } catch (Exception ex) { Console.WriteLine($"  (cleanup) couldn't delete {table} {id}: {ex.Message}"); }
            }

            Console.WriteLine("Unregistering the plug-in...");
            client.Delete("sdkmessageprocessingstepimage", imageId);
            client.Delete("sdkmessageprocessingstep", stepId);
            client.Delete("plugintype", pluginTypeId);
            client.Delete("pluginassembly", assemblyId);
        }

        Console.WriteLine(_failures == 0 ? "All lab 01 plug-in checks passed." : $"{_failures} check(s) failed.");
        return _failures == 0 ? 0 : 1;
    }


    private Guid CreateAssembly()
    {
        var path = Path.GetFullPath(assemblyPath);
        var name = System.Reflection.AssemblyName.GetAssemblyName(path);
        var content = Convert.ToBase64String(File.ReadAllBytes(path));
        return client.Create(new Entity("pluginassembly")
        {
            ["name"] = name.Name,
            ["content"] = content,
            ["isolationmode"] = new OptionSetValue(2), // Sandbox
            ["sourcetype"] = new OptionSetValue(0), // Database
            ["culture"] = "neutral",
            ["version"] = name.Version!.ToString(),
            ["publickeytoken"] = Convert.ToHexString(name.GetPublicKeyToken()!).ToLowerInvariant(),
        });
    }

    private Guid CreatePluginType(Guid assemblyId) => client.Create(new Entity("plugintype")
    {
        ["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId),
        ["typename"] = "AB400.Lab01.RequestApprovalPlugin",
        ["name"] = "AB400.Lab01.RequestApprovalPlugin",
        ["friendlyname"] = $"AB400.Lab01.RequestApprovalPlugin.{Guid.NewGuid():N}",
    });

    private Guid CreateStep(Guid pluginTypeId)
    {
        var messageId = FindOne("sdkmessage", "name", "Update")!.Id;
        var filterQuery = new QueryExpression("sdkmessagefilter") { ColumnSet = new ColumnSet(false), TopCount = 1 };
        filterQuery.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, messageId);
        filterQuery.Criteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, "eqp_request");
        var filterId = client.RetrieveMultiple(filterQuery).Entities.First().Id;

        return client.Create(new Entity("sdkmessageprocessingstep")
        {
            ["name"] = "Lab 01 verify: enforce replacement eligibility on approval",
            ["sdkmessageid"] = new EntityReference("sdkmessage", messageId),
            ["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId),
            ["plugintypeid"] = new EntityReference("plugintype", pluginTypeId),
            ["stage"] = new OptionSetValue(20), // Pre-operation
            ["mode"] = new OptionSetValue(0), // Synchronous
            ["supporteddeployment"] = new OptionSetValue(0), // Server only
            ["invocationsource"] = new OptionSetValue(0), // Parent
            ["rank"] = 1,
            ["filteringattributes"] = "eqp_requeststatus",
            ["configuration"] = "",
        });
    }

    private Guid CreateImage(Guid stepId) => client.Create(new Entity("sdkmessageprocessingstepimage")
    {
        ["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId),
        ["entityalias"] = "PreImage",
        ["imagetype"] = new OptionSetValue(0), // Pre-image
        ["messagepropertyname"] = "Target",
        ["attributes"] = "eqp_requeststatus,eqp_currentdevice",
        ["name"] = "PreImage",
    });

    private (Guid employeeId, Guid ineligibleDeviceId, Guid eligibleDeviceId, Guid ineligibleRequestId, Guid eligibleRequestId) CreateTestData()
    {
        var employeeId = Track("eqp_employee", client.Create(new Entity("eqp_employee")
        {
            ["eqp_name"] = "Lab01Verify temp employee",
            ["eqp_employeenumber"] = $"TMP-{Guid.NewGuid():N}"[..12],
        }));

        // eqp_device's asset-tag primary attribute is capped at 20 characters.
        var ineligibleDeviceId = Track("eqp_device", client.Create(new Entity("eqp_device")
        {
            ["eqp_name"] = "L1V-ineligible",
            ["eqp_issuedon"] = DateTime.UtcNow.Date.AddMonths(-1),
            ["eqp_damagedbeyondrepair"] = false,
        }));
        var eligibleDeviceId = Track("eqp_device", client.Create(new Entity("eqp_device")
        {
            ["eqp_name"] = "L1V-eligible",
            ["eqp_issuedon"] = DateTime.UtcNow.Date.AddMonths(-30),
            ["eqp_damagedbeyondrepair"] = false,
        }));

        // eqp_name on eqp_request is auto-numbered (REQ-{SEQNUM:5}); leave it unset so Dataverse assigns it.
        var ineligibleRequestId = Track("eqp_request", client.Create(new Entity("eqp_request")
        {
            ["eqp_employee"] = new EntityReference("eqp_employee", employeeId),
            ["eqp_currentdevice"] = new EntityReference("eqp_device", ineligibleDeviceId),
            ["eqp_requeststatus"] = new OptionSetValue(100000000), // Submitted
        }));
        var eligibleRequestId = Track("eqp_request", client.Create(new Entity("eqp_request")
        {
            ["eqp_employee"] = new EntityReference("eqp_employee", employeeId),
            ["eqp_currentdevice"] = new EntityReference("eqp_device", eligibleDeviceId),
            ["eqp_requeststatus"] = new OptionSetValue(100000000), // Submitted
        }));

        return (employeeId, ineligibleDeviceId, eligibleDeviceId, ineligibleRequestId, eligibleRequestId);
    }

    private Guid Track(string table, Guid id)
    {
        _created.Add((table, id));
        return id;
    }


    private bool TryApprove(Guid requestId, out string message)
    {
        try
        {
            client.Update(new Entity("eqp_request", requestId) { ["eqp_requeststatus"] = new OptionSetValue(100000001) }); // Approved
            message = "updated without error";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private Entity? FindOne(string table, string column, string value)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition(column, ConditionOperator.Equal, value);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    private void Check(string description, bool passed, string detail)
    {
        if (passed)
        {
            Console.WriteLine($"[ pass ] {description}");
        }
        else
        {
            _failures++;
            Console.WriteLine($"[ FAIL ] {description} — {detail}");
        }
    }
}
