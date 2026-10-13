using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

internal static class SolutionHelper
{
    // Creates a solution owned by the Equipment Provisioning Labs publisher if it doesn't exist yet.
    public static Guid EnsureSolution(ServiceClient client, string uniqueName, string friendlyName, string description)
    {
        var existing = FindOne(client, "solution", "uniquename", uniqueName);
        if (existing is not null)
        {
            Log("exists", $"solution {uniqueName}");
            return existing.Id;
        }
        var publisher = FindOne(client, "publisher", "uniquename", SchemaBuilder.PublisherName)
            ?? throw new InvalidOperationException("Publisher not found. Run the schema command first.");
        var id = client.Create(new Entity("solution")
        {
            ["uniquename"] = uniqueName,
            ["friendlyname"] = friendlyName,
            ["publisherid"] = new EntityReference("publisher", publisher.Id),
            ["version"] = "1.0.0.0",
            ["description"] = description,
        });
        Log("create", $"solution {uniqueName}");
        return id;
    }

    public static Guid CreateInSolution(ServiceClient client, string solutionName, Entity entity)
    {
        var request = new CreateRequest { Target = entity };
        request.Parameters["SolutionUniqueName"] = solutionName;
        return ((CreateResponse)client.Execute(request)).id;
    }

    public static Entity? FindOne(ServiceClient client, string table, string column, string value)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition(column, ConditionOperator.Equal, value);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
