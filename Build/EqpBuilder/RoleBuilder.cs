using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Creates the F6 security roles from the foundation spec §4 and replaces their privileges each run,
// so the roles always match this definition. The roles are additive: users also need Basic User
// (or another role that lets them open apps), and application users need Basic User too.
internal sealed class RoleBuilder(ServiceClient client)
{
    private const int RoleComponentType = 20;

    private enum Op { Create, Read, Write, Append, AppendTo }

    public void Run()
    {
        var u = PrivilegeDepth.Basic;  // user level
        var o = PrivilegeDepth.Global; // organization level

        // Requester: works with their own requests; reads the reference data. Append/AppendTo let them set
        // the request's employee and device lookups.
        EnsureRole("Equipment Requester",
            "AB-400 labs: submit and track your own replacement requests. Add to Basic User.",
            [
                ("eqp_request", Op.Create, u), ("eqp_request", Op.Read, u), ("eqp_request", Op.Write, u), ("eqp_request", Op.Append, u),
                ("eqp_employee", Op.Read, o), ("eqp_employee", Op.AppendTo, o),
                ("eqp_device", Op.Read, o), ("eqp_device", Op.AppendTo, o),
                ("eqp_allowance", Op.Read, o),
            ]);

        // Coordinator: full create, read, write and lookup rights on all four tables (no delete).
        var coordinator = new List<(string, Op, PrivilegeDepth)>();
        foreach (var table in new[] { "eqp_employee", "eqp_device", "eqp_request", "eqp_allowance" })
        {
            foreach (var op in new[] { Op.Create, Op.Read, Op.Write, Op.Append, Op.AppendTo })
            {
                coordinator.Add((table, op, o));
            }
        }
        EnsureRole("Equipment Coordinator",
            "AB-400 labs: manage employees, devices, requests and allowances, and run Reset lab data. Add to Basic User.",
            coordinator);

        // Integration: for application users (managed identity in lab 03, service principal in lab 08).
        EnsureRole("Equipment Integration",
            "AB-400 labs: for application users that process requests and recalculate allowances. Add to Basic User.",
            [
                ("eqp_employee", Op.Read, o), ("eqp_device", Op.Read, o),
                ("eqp_request", Op.Read, o), ("eqp_request", Op.Write, o),
                ("eqp_allowance", Op.Read, o), ("eqp_allowance", Op.Write, o),
                ("environmentvariabledefinition", Op.Read, o), // values inherit the definition's privileges
            ]);
    }

    private void EnsureRole(string name, string description, IEnumerable<(string Table, Op Op, PrivilegeDepth Depth)> grants)
    {
        var rootBusinessUnit = RootBusinessUnitId();
        var query = new QueryExpression("role") { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
        query.Criteria.AddCondition("businessunitid", ConditionOperator.Equal, rootBusinessUnit);
        var existing = client.RetrieveMultiple(query).Entities.FirstOrDefault();

        Guid roleId;
        if (existing is null)
        {
            roleId = client.Create(new Entity("role")
            {
                ["name"] = name,
                ["description"] = description,
                ["businessunitid"] = new EntityReference("businessunit", rootBusinessUnit),
            });
            Log("create", $"role {name}");
        }
        else
        {
            roleId = existing.Id;
            client.Update(new Entity("role", roleId) { ["description"] = description });
            Log("exists", $"role {name}");
        }

        var privileges = grants
            .Select(g => new RolePrivilege { PrivilegeId = PrivilegeId(g.Table, g.Op), Depth = g.Depth })
            .ToArray();
        client.Execute(new ReplacePrivilegesRoleRequest { RoleId = roleId, Privileges = privileges });
        Log("set", $"role {name}: {privileges.Length} privileges");

        client.Execute(new AddSolutionComponentRequest
        {
            ComponentType = RoleComponentType,
            ComponentId = roleId,
            SolutionUniqueName = SchemaBuilder.SolutionName,
        });
    }

    private readonly Dictionary<string, SecurityPrivilegeMetadata[]> _privilegeCache = [];

    private Guid PrivilegeId(string table, Op op)
    {
        if (!_privilegeCache.TryGetValue(table, out var privileges))
        {
            var response = (RetrieveEntityResponse)client.Execute(new RetrieveEntityRequest
            {
                LogicalName = table,
                EntityFilters = EntityFilters.Privileges,
            });
            privileges = response.EntityMetadata.Privileges;
            _privilegeCache[table] = privileges;
        }
        var type = op switch
        {
            Op.Create => PrivilegeType.Create,
            Op.Read => PrivilegeType.Read,
            Op.Write => PrivilegeType.Write,
            Op.Append => PrivilegeType.Append,
            Op.AppendTo => PrivilegeType.AppendTo,
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
        return privileges.Single(p => p.PrivilegeType == type).PrivilegeId;
    }

    private Guid RootBusinessUnitId()
    {
        var query = new QueryExpression("businessunit") { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition("parentbusinessunitid", ConditionOperator.Null);
        return client.RetrieveMultiple(query).Entities.Single().Id;
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
