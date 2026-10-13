using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Imports a solution zip (by default the managed learner download) into the connected environment, the
// same way a learner's import does, and reports the installed version. Used for the clean-environment test.
internal sealed class SolutionImporter(ServiceClient client, string zipPath)
{
    public int Run()
    {
        var path = Path.GetFullPath(zipPath);
        Log("import", $"{Path.GetFileName(path)} ({new FileInfo(path).Length / 1024} KB)");
        var started = DateTime.UtcNow;
        client.Execute(new ImportSolutionRequest
        {
            CustomizationFile = File.ReadAllBytes(path),
            PublishWorkflows = true,
            OverwriteUnmanagedCustomizations = false,
        });
        Log("done", $"imported in {(DateTime.UtcNow - started).TotalSeconds:F0} s");

        var query = new QueryExpression("solution") { ColumnSet = new ColumnSet("version", "ismanaged"), TopCount = 1 };
        var uniqueName = SolutionUniqueName(path);
        query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
        var solution = client.RetrieveMultiple(query).Entities.SingleOrDefault();
        if (solution is null)
        {
            Log("error", $"{uniqueName} isn't installed after the import");
            return 1;
        }
        Log("check", $"{uniqueName} {solution.GetAttributeValue<string>("version")}, managed: {solution.GetAttributeValue<bool>("ismanaged")}");
        return 0;
    }

    private static string SolutionUniqueName(string zipPath)
    {
        using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);
        using var stream = zip.GetEntry("solution.xml")!.Open();
        var xml = System.Xml.Linq.XDocument.Load(stream);
        return xml.Descendants("UniqueName").First().Value;
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
