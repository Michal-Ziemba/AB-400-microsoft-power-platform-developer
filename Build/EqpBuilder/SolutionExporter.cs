using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Exports a solution as managed (the learner download under Allfiles) and unmanaged (for source control;
// unpack it with 'pac solution unpack'). Optionally sets the version first.
internal sealed class SolutionExporter(ServiceClient client, string solutionName, string managedPath, string unmanagedPath, string? version)
{
    private readonly HashSet<Guid> _excludedBaseForms = [];

    public void Run()
    {
        if (solutionName == SchemaBuilder.SolutionName)
        {
            PrepareBaseExport();
        }
        SetVersion();
        client.Execute(new PublishAllXmlRequest());
        Export(managed: true, managedPath);
        Export(managed: false, unmanagedPath);
    }

    private void PrepareBaseExport()
    {
        var solution = SolutionHelper.FindOne(client, "solution", "uniquename", solutionName)
            ?? throw new InvalidOperationException($"Solution '{solutionName}' not found.");
        var metadata = ((RetrieveEntityResponse)client.Execute(new RetrieveEntityRequest
        {
            LogicalName = "eqp_request",
            EntityFilters = EntityFilters.Entity,
        })).EntityMetadata;
        var tableId = metadata.MetadataId
            ?? throw new InvalidOperationException("Request table metadata ID is missing.");

        var lab01 = SolutionHelper.FindOne(client, "solution", "uniquename", Lab01Builder.SolutionName);
        if (lab01 is not null)
        {
            var labForms = new QueryExpression("solutioncomponent") { ColumnSet = new ColumnSet("objectid") };
            labForms.Criteria.AddCondition("solutionid", ConditionOperator.Equal, lab01.Id);
            labForms.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 60);
            foreach (var form in client.RetrieveMultiple(labForms).Entities)
            {
                _excludedBaseForms.Add(form.GetAttributeValue<Guid>("objectid"));
            }
        }

        var rootQuery = new QueryExpression("solutioncomponent") { ColumnSet = new ColumnSet("rootcomponentbehavior") };
        rootQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solution.Id);
        rootQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1);
        rootQuery.Criteria.AddCondition("objectid", ConditionOperator.Equal, tableId);
        var root = client.RetrieveMultiple(rootQuery).Entities.SingleOrDefault();
        if (root?.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")?.Value != 0)
        {
            if (root is not null)
            {
                client.Execute(new RemoveSolutionComponentRequest
                {
                    ComponentId = tableId,
                    ComponentType = 1,
                    SolutionUniqueName = solutionName,
                });
            }
            // A clean install needs the full definition of this custom table.
            client.Execute(new AddSolutionComponentRequest
            {
                ComponentId = tableId,
                ComponentType = 1,
                SolutionUniqueName = solutionName,
                AddRequiredComponents = false,
                DoNotIncludeSubcomponents = false,
            });
        }
    }

    private byte[] RemoveLabForms(byte[] bytes, bool managed)
    {
        if (_excludedBaseForms.Count == 0)
        {
            return bytes;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"ab400-base-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "input.zip");
            var output = Path.Combine(directory, "output.zip");
            var source = Path.Combine(directory, "source");
            var packageType = managed ? "Managed" : "Unmanaged";
            File.WriteAllBytes(input, bytes);
            RunPac("solution", "unpack", "--zipfile", input, "--folder", source, "--packagetype", packageType);
            var removed = new HashSet<Guid>();
            foreach (var file in Directory.EnumerateFiles(Path.Combine(source, "Entities"), "*.xml", SearchOption.AllDirectories))
            {
                var stem = Path.GetFileNameWithoutExtension(file).Split('_')[0];
                if (!Guid.TryParse(stem, out var id) || !_excludedBaseForms.Contains(id))
                {
                    continue;
                }
                var form = XDocument.Load(file);
                if (!Guid.TryParse(form.Descendants("formid").SingleOrDefault()?.Value, out var formId) || formId != id)
                {
                    throw new InvalidOperationException($"Expected form source for {id}, found '{file}'.");
                }
                File.Delete(file);
                removed.Add(id);
            }
            var manifestPath = Path.Combine(source, "Other", "Solution.xml");
            var manifest = XDocument.Load(manifestPath);
            foreach (var dependency in manifest.Descendants("MissingDependency").ToArray())
            {
                if (Guid.TryParse(dependency.Element("Dependent")?.Attribute("id")?.Value, out var id) && removed.Contains(id))
                {
                    dependency.Remove();
                }
            }
            foreach (var component in manifest.Descendants("RootComponent").ToArray())
            {
                if (component.Attribute("type")?.Value == "60"
                    && Guid.TryParse(component.Attribute("id")?.Value, out var id) && removed.Contains(id))
                {
                    component.Remove();
                }
            }
            manifest.Save(manifestPath);
            RunPac("solution", "pack", "--zipfile", output, "--folder", source, "--packagetype", packageType);
            Log("filter", $"excluded {removed.Count} lab 01 form(s) from the {(managed ? "managed" : "unmanaged")} base package");
            return File.ReadAllBytes(output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void RunPac(params string[] args)
    {
        var executable = Environment.GetEnvironmentVariable("EQP_PAC_EXE") ?? "pac";
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException($"Couldn't start '{executable}'.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var stdout = output.GetAwaiter().GetResult();
        var stderr = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Power Platform CLI failed ({process.ExitCode}): {stdout}{stderr}");
        }
    }

    // For solutions learners edit (lab 08): the learner download is the unmanaged export.
    public void RunUnmanagedOnly()
    {
        SetVersion();
        client.Execute(new PublishAllXmlRequest());
        Export(managed: false, managedPath);
        File.Copy(managedPath, unmanagedPath, overwrite: true);
        Log("copy", $"{managedPath} -> {unmanagedPath}");
    }

    private void SetVersion()
    {
        if (!string.IsNullOrEmpty(version))
        {
            var query = new QueryExpression("solution") { ColumnSet = new ColumnSet(false), TopCount = 1 };
            query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, solutionName);
            var solution = client.RetrieveMultiple(query).Entities.Single();
            client.Update(new Entity("solution", solution.Id) { ["version"] = version });
            Log("set", $"{solutionName} version {version}");
        }
    }

    private void Export(bool managed, string path)
    {
        var response = (ExportSolutionResponse)client.Execute(new ExportSolutionRequest
        {
            SolutionName = solutionName,
            Managed = managed,
        });
        var bytes = solutionName == SchemaBuilder.SolutionName
            ? RemoveLabForms(response.ExportSolutionFile, managed)
            : response.ExportSolutionFile;
        if (solutionName == SchemaBuilder.SolutionName)
        {
            using var stream = new MemoryStream(bytes);
            using var zip = new ZipArchive(stream);
            var manifestEntry = zip.GetEntry("solution.xml")
                ?? throw new InvalidOperationException("Exported solution manifest is missing.");
            using var manifestStream = manifestEntry.Open();
            var manifest = XDocument.Load(manifestStream);
            var external = manifest.Descendants("MissingDependency")
                .Select(dependency => dependency.Element("Required")?.Attribute("schemaName")?.Value)
                .Where(name => name?.StartsWith("eqp_", StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();
            if (external.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Base solution depends on components outside its package: {string.Join(", ", external)}. Export not saved.");
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, bytes);
        Log("export", $"{solutionName} {(managed ? "managed" : "unmanaged")} -> {path} ({bytes.Length / 1024} KB)");
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
