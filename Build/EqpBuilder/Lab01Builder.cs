using System.Security;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Builds F7, the lab 01 solution EquipmentProvisioningLab01: a "Request intake" main form for eqp_request
// whose script blocks approving an ineligible request in the form (client side only), then exports it.
// Requires the base solution (F1) in the same environment. Safe to rerun.
internal sealed class Lab01Builder(ServiceClient client, string repoRoot)
{
    public const string SolutionName = "EquipmentProvisioningLab01";
    private const string ScriptName = "eqp_/lab01/requestintake.js";
    private const string FormName = "Request intake";
    private const string Ns = "EquipmentProvisioning.Lab01";

    private static readonly string[] Columns =
        ["eqp_name", "eqp_employee", "eqp_currentdevice", "eqp_requesteddevicetype", "eqp_justification",
         "eqp_requeststatus", "eqp_approvedon", "ownerid"];

    public void Run()
    {
        SolutionHelper.EnsureSolution(client, SolutionName, "Equipment Provisioning lab 01",
            "AB-400 lab 01: the Request intake form with a client-side eligibility check.");
        var scriptId = EnsureScript();
        var formId = EnsureForm();
        client.Execute(new PublishXmlRequest
        {
            ParameterXml = "<importexportxml><entities><entity>eqp_request</entity></entities>" +
                           $"<webresources><webresource>{scriptId}</webresource></webresources></importexportxml>",
        });
        Log("publish", $"form {FormName} ({formId})");

        new SolutionExporter(client, SolutionName,
            Path.Combine(repoRoot, "Allfiles", "01-equipment-validation-plugin", $"{SolutionName}_managed.zip"),
            Path.Combine(repoRoot, "Build", "Solutions", $"{SolutionName}.zip"), null).Run();
    }

    private Guid EnsureScript()
    {
        var content = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(repoRoot, "Build", "WebResources", "lab01-requestintake.js")));
        var existing = SolutionHelper.FindOne(client, "webresource", "name", ScriptName);
        if (existing is not null)
        {
            client.Update(new Entity("webresource", existing.Id) { ["content"] = content });
            Log("update", $"web resource {ScriptName}");
            return existing.Id;
        }
        var id = SolutionHelper.CreateInSolution(client, SolutionName, new Entity("webresource")
        {
            ["name"] = ScriptName,
            ["displayname"] = "Lab 01 request intake checks",
            ["webresourcetype"] = new OptionSetValue(3), // JavaScript
            ["content"] = content,
        });
        Log("create", $"web resource {ScriptName}");
        return id;
    }

    private Guid EnsureForm()
    {
        var metadata = ((RetrieveEntityResponse)client.Execute(new RetrieveEntityRequest
        {
            LogicalName = "eqp_request",
            EntityFilters = EntityFilters.Attributes,
        })).EntityMetadata;

        var rows = new StringBuilder();
        foreach (var column in Columns)
        {
            var attribute = metadata.Attributes.Single(a => a.LogicalName == column);
            var label = SecurityElement.Escape(attribute.DisplayName.UserLocalizedLabel?.Label ?? column);
            rows.Append($"<row><cell id=\"{{{Guid.NewGuid()}}}\"><labels><label description=\"{label}\" languagecode=\"1033\" /></labels>")
                .Append($"<control id=\"{column}\" classid=\"{AppBuilder.ControlClassId(attribute)}\" datafieldname=\"{column}\" /></cell></row>");
        }

        string Handler(string function) =>
            $"<Handlers><Handler functionName=\"{Ns}.{function}\" libraryName=\"{ScriptName}\" handlerUniqueId=\"{{{Guid.NewGuid()}}}\" " +
            "enabled=\"true\" parameters=\"\" passExecutionContext=\"true\" /></Handlers>";

        var formXml =
            $"<form><tabs><tab verticallayout=\"true\" id=\"{{{Guid.NewGuid()}}}\" IsUserDefined=\"1\"><labels><label description=\"Request\" languagecode=\"1033\" /></labels>" +
            $"<columns><column width=\"100%\"><sections><section showlabel=\"false\" showbar=\"false\" IsUserDefined=\"0\" id=\"{{{Guid.NewGuid()}}}\">" +
            $"<labels><label description=\"Request\" languagecode=\"1033\" /></labels><rows>{rows}</rows></section></sections></column></columns></tab></tabs>" +
            "<events>" +
            $"<event name=\"onload\" application=\"false\" active=\"false\">{Handler("onLoad")}</event>" +
            $"<event name=\"onsave\" application=\"false\" active=\"false\">{Handler("onSave")}</event>" +
            $"<event name=\"onchange\" application=\"false\" active=\"false\" attribute=\"eqp_currentdevice\">{Handler("onDeviceChange")}</event>" +
            "</events>" +
            $"<formLibraries><Library name=\"{ScriptName}\" libraryUniqueId=\"{{{Guid.NewGuid()}}}\" /></formLibraries></form>";

        var query = new QueryExpression("systemform") { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, "eqp_request");
        query.Criteria.AddCondition("name", ConditionOperator.Equal, FormName);
        var existing = client.RetrieveMultiple(query).Entities.FirstOrDefault();
        if (existing is not null)
        {
            client.Update(new Entity("systemform", existing.Id) { ["formxml"] = formXml });
            Log("update", $"form {FormName}");
            return existing.Id;
        }
        var id = SolutionHelper.CreateInSolution(client, SolutionName, new Entity("systemform")
        {
            ["name"] = FormName,
            ["description"] = "Lab 01: request form with a client-side eligibility check on approval.",
            ["objecttypecode"] = "eqp_request",
            ["type"] = new OptionSetValue(2), // Main
            ["formxml"] = formXml,
        });
        Log("create", $"form {FormName}");
        return id;
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
