using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Builds the Equipment Provisioning model-driven app for F1: main forms and default views with the
// columns the labs use, the Reset lab data script and its command on the Requests and Employees grids,
// the site map, the app itself, and its security-role associations. Safe to rerun: forms and views are
// rewritten from the definitions below on every run.
internal sealed partial class AppBuilder(ServiceClient client, string webResourceDir)
{
    private const string AppUniqueName = "eqp_EquipmentProvisioning";
    private const string AppName = "Equipment Provisioning";
    private const string ScriptName = "eqp_/scripts/resetlabdata.js";
    private const string ResetFunction = "EquipmentProvisioning.resetLabData";
    private const string DefaultAppIcon = "953b9fac-1e5e-e611-80d6-00155ded156f";

    private static readonly (string Table, string[] FormColumns, string[] ViewColumns)[] Tables =
    [
        ("eqp_request",
            ["eqp_name", "eqp_employee", "eqp_currentdevice", "eqp_requesteddevicetype", "eqp_justification", "eqp_requeststatus",
             "eqp_approvedon", "eqp_availability", "eqp_availabilitycheckedon", "eqp_processingnote", "ownerid"],
            ["eqp_name", "eqp_employee", "eqp_currentdevice", "eqp_requeststatus", "eqp_availability", "eqp_approvedon"]),
        ("eqp_employee",
            ["eqp_name", "eqp_employeenumber", "eqp_email", "eqp_department", "eqp_user", "ownerid"],
            ["eqp_name", "eqp_employeenumber", "eqp_department", "eqp_user"]),
        ("eqp_device",
            ["eqp_name", "eqp_devicetype", "eqp_model", "eqp_devicestatus", "eqp_assignedto", "eqp_issuedon",
             "eqp_damagedbeyondrepair", "eqp_conditionrating", "ownerid"],
            ["eqp_name", "eqp_devicetype", "eqp_devicestatus", "eqp_assignedto", "eqp_issuedon", "eqp_damagedbeyondrepair"]),
        ("eqp_allowance",
            ["eqp_name", "eqp_employee", "eqp_periodstart", "eqp_periodend", "eqp_devicesallowed", "eqp_devicesused",
             "eqp_remaining", "eqp_lastrecalculatedon", "ownerid"],
            ["eqp_name", "eqp_employee", "eqp_devicesallowed", "eqp_devicesused", "eqp_remaining"]),
    ];

    public void Run()
    {
        var components = new EntityReferenceCollection();
        var unpinned = new EntityReferenceCollection();
        foreach (var (table, formColumns, viewColumns) in Tables)
        {
            var metadata = RetrieveTable(table);
            components.Add(new EntityReference("entity", metadata.MetadataId!.Value));
            var formId = UpdateMainForm(metadata, formColumns);
            // Requests aren't pinned to one form, so lab solutions (lab 01's Request intake form) appear in
            // the app while they're installed.
            if (table == "eqp_request")
            {
                unpinned.Add(new EntityReference("systemform", formId));
            }
            else
            {
                components.Add(new EntityReference("systemform", formId));
            }
            components.Add(new EntityReference("savedquery", UpdateDefaultView(metadata, viewColumns)));
        }

        var scriptId = EnsureScript();
        var siteMapId = EnsureSiteMap();
        components.Add(new EntityReference("sitemap", siteMapId));
        var appId = EnsureApp();

        client.Execute(new AddAppComponentsRequest { AppId = appId, Components = components });
        Log("set", $"app components ({components.Count})");
        try
        {
            client.Execute(new RemoveAppComponentsRequest { AppId = appId, Components = unpinned });
            Log("set", "Requests not pinned to a single form");
        }
        catch (System.ServiceModel.FaultException<OrganizationServiceFault>)
        {
            // Nothing to remove: the form wasn't pinned.
        }

        EnsureResetCommand(appId, RetrieveTable("eqp_request"), scriptId);
        EnsureResetCommand(appId, RetrieveTable("eqp_employee"), scriptId);
        AssociateRoles(appId, "Equipment Coordinator", "Equipment Requester");

        var validation = (ValidateAppResponse)client.Execute(new ValidateAppRequest { AppModuleId = appId });
        foreach (var issue in validation.AppValidationResponse.ValidationIssueList)
        {
            Log(issue.ErrorType.ToString().ToLowerInvariant(),
                $"{issue.Message} [code {issue.CRMErrorCode}, component type {issue.ComponentType}, {issue.DisplayName ?? issue.ParentEntityName}]");
        }

        var tables = string.Join("", Tables.Select(t => $"<entity>{t.Table}</entity>"));
        client.Execute(new PublishXmlRequest
        {
            ParameterXml = $"<importexportxml><entities>{tables}</entities><webresources><webresource>{scriptId}</webresource></webresources>" +
                           $"<sitemaps><sitemap>{siteMapId}</sitemap></sitemaps><appmodules><appmodule>{appId}</appmodule></appmodules></importexportxml>",
        });
        Log("publish", $"app {AppName}");
    }

    private EntityMetadata RetrieveTable(string table) =>
        ((RetrieveEntityResponse)client.Execute(new RetrieveEntityRequest { LogicalName = table, EntityFilters = EntityFilters.Attributes })).EntityMetadata;

    private Guid UpdateMainForm(EntityMetadata metadata, string[] columns)
    {
        var query = new QueryExpression("systemform") { ColumnSet = new ColumnSet("formid"), TopCount = 1 };
        query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, metadata.LogicalName);
        query.Criteria.AddCondition("type", ConditionOperator.Equal, 2); // Main
        query.Criteria.AddCondition("name", ConditionOperator.Equal, "Information"); // the generated main form, not lab-specific ones
        var form = client.RetrieveMultiple(query).Entities.Single();

        var rows = new StringBuilder();
        foreach (var column in columns)
        {
            var attribute = metadata.Attributes.Single(a => a.LogicalName == column);
            var label = SecurityElement.Escape(attribute.DisplayName.UserLocalizedLabel?.Label ?? column);
            rows.Append($"<row><cell id=\"{{{Guid.NewGuid()}}}\"><labels><label description=\"{label}\" languagecode=\"1033\" /></labels>")
                .Append($"<control id=\"{column}\" classid=\"{ControlClassId(attribute)}\" datafieldname=\"{column}\" /></cell></row>");
        }
        var formXml =
            $"<form><tabs><tab verticallayout=\"true\" id=\"{{{Guid.NewGuid()}}}\" IsUserDefined=\"1\"><labels><label description=\"General\" languagecode=\"1033\" /></labels>" +
            $"<columns><column width=\"100%\"><sections><section showlabel=\"false\" showbar=\"false\" IsUserDefined=\"0\" id=\"{{{Guid.NewGuid()}}}\">" +
            $"<labels><label description=\"General\" languagecode=\"1033\" /></labels><rows>{rows}</rows></section></sections></column></columns></tab></tabs></form>";

        client.Update(new Entity("systemform", form.Id) { ["formxml"] = formXml });
        Log("set", $"main form {metadata.LogicalName} ({columns.Length} columns)");
        return form.Id;
    }

    // Standard model-driven control class IDs by column type.
    internal static string ControlClassId(AttributeMetadata attribute) => attribute switch
    {
        LookupAttributeMetadata => "{270BD3DB-D9AF-4782-9025-509E298DEC0A}",
        PicklistAttributeMetadata => "{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}",
        BooleanAttributeMetadata => "{67FAC785-CD58-4f9f-ABB3-4B7DDC6ED5ED}",
        DateTimeAttributeMetadata => "{5B773807-9FB2-42db-97C3-7A91EFF8ADFF}",
        IntegerAttributeMetadata => "{C6D124CA-7EDA-4a60-AEA9-7FB8D318B68F}",
        MemoAttributeMetadata => "{E0DECE4B-6FC8-4a8f-A065-082708572369}",
        StringAttributeMetadata { FormatName.Value: "Email" } => "{ADA2203E-B4CD-49be-9DDF-234642B43B52}",
        _ => "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}",
    };

    private Guid UpdateDefaultView(EntityMetadata metadata, string[] columns)
    {
        var query = new QueryExpression("savedquery") { ColumnSet = new ColumnSet("layoutxml"), TopCount = 1 };
        query.Criteria.AddCondition("returnedtypecode", ConditionOperator.Equal, metadata.LogicalName);
        query.Criteria.AddCondition("querytype", ConditionOperator.Equal, 0);
        query.Criteria.AddCondition("isdefault", ConditionOperator.Equal, true);
        var view = client.RetrieveMultiple(query).Entities.Single();
        var objectTypeCode = ObjectAttribute().Match(view.GetAttributeValue<string>("layoutxml")).Groups[1].Value;

        var cells = string.Join("", columns.Select(c => $"<cell name=\"{c}\" width=\"{(c == "eqp_name" ? 150 : 140)}\" />"));
        var layoutXml = $"<grid name=\"resultset\" object=\"{objectTypeCode}\" jump=\"eqp_name\" select=\"1\" icon=\"1\" preview=\"1\">" +
                        $"<row name=\"result\" id=\"{metadata.PrimaryIdAttribute}\">{cells}</row></grid>";
        var attributes = string.Join("", columns.Prepend(metadata.PrimaryIdAttribute).Select(c => $"<attribute name=\"{c}\" />"));
        var fetchXml = $"<fetch version=\"1.0\" mapping=\"logical\"><entity name=\"{metadata.LogicalName}\">{attributes}" +
                       "<order attribute=\"eqp_name\" descending=\"false\" />" +
                       "<filter type=\"and\"><condition attribute=\"statecode\" operator=\"eq\" value=\"0\" /></filter></entity></fetch>";

        client.Update(new Entity("savedquery", view.Id) { ["layoutxml"] = layoutXml, ["fetchxml"] = fetchXml });
        Log("set", $"default view {metadata.LogicalName} ({columns.Length} columns)");
        return view.Id;
    }

    [GeneratedRegex("object=\"(\\d+)\"")]
    private static partial Regex ObjectAttribute();

    private Guid EnsureScript()
    {
        var content = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(webResourceDir, "resetlabdata.js")));
        var existing = FindOne("webresource", "name", ScriptName);
        if (existing is not null)
        {
            client.Update(new Entity("webresource", existing.Id) { ["content"] = content });
            Log("update", $"web resource {ScriptName}");
            return existing.Id;
        }
        var id = CreateInSolution(new Entity("webresource")
        {
            ["name"] = ScriptName,
            ["displayname"] = "Reset lab data (command script)",
            ["webresourcetype"] = new OptionSetValue(3), // JavaScript
            ["content"] = content,
        });
        Log("create", $"web resource {ScriptName}");
        return id;
    }

    private Guid EnsureSiteMap()
    {
        var subAreas = string.Join("", Tables.Select(t => $"<SubArea Id=\"{t.Table}\" Entity=\"{t.Table}\" />"));
        var xml = "<SiteMap IntroducedVersion=\"7.0.0.0\"><Area Id=\"eqp_area\" ShowGroups=\"true\"><Titles><Title LCID=\"1033\" Title=\"Equipment\" /></Titles>" +
                  $"<Group Id=\"eqp_group\"><Titles><Title LCID=\"1033\" Title=\"Equipment provisioning\" /></Titles>{subAreas}</Group></Area></SiteMap>";
        var existing = FindOne("sitemap", "sitemapnameunique", AppUniqueName);
        if (existing is not null)
        {
            client.Update(new Entity("sitemap", existing.Id) { ["sitemapxml"] = xml });
            Log("update", $"site map {AppUniqueName}");
            return existing.Id;
        }
        var id = CreateInSolution(new Entity("sitemap")
        {
            ["sitemapname"] = AppName,
            ["sitemapnameunique"] = AppUniqueName,
            ["sitemapxml"] = xml,
        });
        Log("create", $"site map {AppUniqueName}");
        return id;
    }

    private Guid EnsureApp()
    {
        var existing = FindOne("appmodule", "uniquename", AppUniqueName);
        if (existing is not null)
        {
            client.Update(new Entity("appmodule", existing.Id) { ["clienttype"] = UnifiedInterface });
            Log("exists", $"app {AppUniqueName}");
            return existing.Id;
        }
        var id = CreateInSolution(new Entity("appmodule")
        {
            ["name"] = AppName,
            ["uniquename"] = AppUniqueName,
            ["description"] = "AB-400 labs: employees, devices, replacement requests and allowances.",
            ["webresourceid"] = new Guid(DefaultAppIcon),
            ["clienttype"] = UnifiedInterface,
        });
        Log("create", $"app {AppUniqueName}");
        return id;
    }

    // Without this, the app is treated as a legacy web client app and shows a warning banner.
    private const int UnifiedInterface = 4;

    private void EnsureResetCommand(Guid appId, EntityMetadata table, Guid scriptId)
    {
        var uniqueName = $"eqp_ResetLabData_{table.LogicalName}_grid";
        if (FindOne("appaction", "uniquename", uniqueName) is not null)
        {
            Log("exists", $"command {uniqueName}");
            return;
        }
        CreateInSolution(new Entity("appaction")
        {
            ["uniquename"] = uniqueName,
            ["name"] = $"Reset lab data ({table.LogicalName} grid)",
            ["appmoduleid"] = new EntityReference("appmodule", appId),
            ["context"] = new OptionSetValue(1), // Entity
            ["contextentity"] = new EntityReference("entity", table.MetadataId!.Value),
            ["contextvalue"] = table.LogicalName,
            ["location"] = new OptionSetValue(1), // Main grid
            ["type"] = new OptionSetValue(0), // Standard button
            ["origin"] = new OptionSetValue(0),
            ["buttonlabeltext"] = "Reset lab data",
            ["buttontooltiptitle"] = "Reset lab data",
            ["buttontooltipdescription"] = "Creates or restores the lab's sample rows. Doesn't delete anything.",
            ["sequence"] = 100m,
            ["onclickeventtype"] = new OptionSetValue(2), // JavaScript
            ["onclickeventjavascriptwebresourceid"] = new EntityReference("webresource", scriptId),
            ["onclickeventjavascriptfunctionname"] = ResetFunction,
            ["visibilitytype"] = new OptionSetValue(0), // always visible
        });
        Log("create", $"command {uniqueName}");
    }

    private void AssociateRoles(Guid appId, params string[] roleNames)
    {
        foreach (var name in roleNames)
        {
            var role = FindOne("role", "name", name) ?? throw new InvalidOperationException($"Role '{name}' not found. Run the roles command first.");
            try
            {
                client.Associate("appmodule", appId, new Relationship("appmoduleroles_association"), [new EntityReference("role", role.Id)]);
                Log("create", $"app access for {name}");
            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault> ex) when (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                Log("exists", $"app access for {name}");
            }
        }
    }

    private Guid CreateInSolution(Entity entity)
    {
        var request = new CreateRequest { Target = entity };
        request.Parameters["SolutionUniqueName"] = SchemaBuilder.SolutionName;
        return ((CreateResponse)client.Execute(request)).id;
    }

    private Entity? FindOne(string table, string column, string value)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition(column, ConditionOperator.Equal, value);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
