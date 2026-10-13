using Microsoft.PowerPlatform.Dataverse.Client;

namespace EqpBuilder;

// Builds the AB-400 base solution (F1) in a Dataverse environment.
// Usage: dotnet run -- <command> --url <environment URL> --user <sign-in name>
// Commands:
//   schema    publisher, solution, choices, tables, columns, relationships, keys, environment variables
//   fixtures  registers Build/Fixtures (build it in Release first) and the custom APIs F2, F3, F4 (--assembly to override the path)
//   verify    technical checks: Reset lab data twice, eligibility per seed device, availability modes
//   roles     security roles F6 (Equipment Requester, Coordinator, Integration); privileges replaced each run
//   app       Equipment Provisioning app: forms, views, Reset lab data command, site map, role access (run roles first)
//   export    managed zip to Allfiles/shared and unmanaged zip to Build/Solutions (--version to set the version first)
//   import    imports a solution zip, by default the managed learner download (--zip to override)
//   lab01     F7: the lab 01 solution (Request intake form with a client-side check), exported to Allfiles/01-…
//   lab08     F8: the lab 08 solution (naive flow, child-flow scaffold, connection references), unmanaged export to
//             Allfiles/08-…; --connection <name> binds a Dataverse connection and switches the flows on for testing
//   lab01verify  live-registers the compiled lab 01 plug-in (Build/LabReference/01-.../bin/Debug/net48/AB400.Lab01.dll)
//                against a throwaway employee/device/request trio, confirms the bypass is blocked and a legitimate
//                approval still succeeds, then unregisters and deletes everything it created
//   findorphans  diagnostic: lists (optionally --delete's) rows in --table whose --column begins with --prefix
internal static class Program
{
    private static int Main(string[] args)
    {
        var command = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "schema";
        var url = GetOption(args, "--url") ?? Environment.GetEnvironmentVariable("EQP_URL");
        var user = GetOption(args, "--user") ?? Environment.GetEnvironmentVariable("EQP_USER");
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(user))
        {
            Console.Error.WriteLine("Provide --url and --user (or the EQP_URL and EQP_USER environment variables).");
            return 2;
        }

        // Creating tables and keys can take minutes per request.
        ServiceClient.MaxConnectionTimeout = TimeSpan.FromMinutes(10);
        using var client = Connect(url, user);
        if (!client.IsReady)
        {
            Console.Error.WriteLine($"Connection failed: {client.LastError}");
            return 1;
        }
        Console.WriteLine($"Connected to {client.ConnectedOrgFriendlyName} as {user}");

        switch (command)
        {
            case "schema":
                new SchemaBuilder(client).Run();
                return 0;
            case "fixtures":
                var assembly = GetOption(args, "--assembly")
                    ?? Path.Combine("..", "Fixtures", "bin", "Release", "net462", "EquipmentProvisioning.Fixtures.dll");
                new FixtureRegistrar(client, assembly).Run();
                return 0;
            case "verify":
                return new Verifier(client).Run();
            case "roles":
                new RoleBuilder(client).Run();
                return 0;
            case "app":
                new AppBuilder(client, GetOption(args, "--webresources") ?? Path.Combine("..", "WebResources")).Run();
                return 0;
            case "export":
                var repo = GetOption(args, "--repo") ?? Path.Combine("..", "..");
                new SolutionExporter(client, SchemaBuilder.SolutionName,
                    Path.Combine(repo, "Allfiles", "shared", $"{SchemaBuilder.SolutionName}_managed.zip"),
                    Path.Combine(repo, "Build", "Solutions", $"{SchemaBuilder.SolutionName}.zip"),
                    GetOption(args, "--version")).Run();
                return 0;
            case "lab01":
                new Lab01Builder(client, GetOption(args, "--repo") ?? Path.Combine("..", "..")).Run();
                return 0;
            case "lab08":
                new Lab08Builder(client, GetOption(args, "--repo") ?? Path.Combine("..", ".."), GetOption(args, "--connection")).Run();
                return 0;
            case "lab01verify":
                var lab01Assembly = GetOption(args, "--assembly")
                    ?? Path.Combine("..", "..", "Build", "LabReference", "01-equipment-validation-plugin", "AB400.Lab01", "bin", "Debug", "net48", "AB400.Lab01.dll");
                return new Lab01PluginVerifier(client, lab01Assembly).Run();
            case "findorphans":
                var deleteFlag = Array.IndexOf(args, "--delete") >= 0;
                var query = new Microsoft.Xrm.Sdk.Query.QueryExpression(GetOption(args, "--table")!) { ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet(true) };
                query.Criteria.AddCondition(GetOption(args, "--column")!, Microsoft.Xrm.Sdk.Query.ConditionOperator.BeginsWith, GetOption(args, "--prefix")!);
                foreach (var e in client.RetrieveMultiple(query).Entities)
                {
                    Console.WriteLine($"{e.Id}  {string.Join(", ", e.Attributes.Select(a => $"{a.Key}={a.Value}"))}");
                    if (deleteFlag)
                    {
                        client.Delete(e.LogicalName, e.Id);
                        Console.WriteLine($"  deleted {e.Id}");
                    }
                }
                return 0;
            case "import":
                return new SolutionImporter(client, GetOption(args, "--zip")
                    ?? Path.Combine("..", "..", "Allfiles", "shared", $"{SchemaBuilder.SolutionName}_managed.zip")).Run();
            default:
                Console.Error.WriteLine($"Unknown command '{command}'.");
                return 2;
        }
    }

    private static ServiceClient Connect(string url, string user)
    {
        // Default: tokens from the Azure CLI, using a separate CLI profile signed in to the lab tenant
        // (az login --use-device-code), so the developer's own Azure CLI sign-in isn't touched.
        // Set EQP_AUTH=interactive to use a browser sign-in through the SDK instead.
        if (!string.Equals(Environment.GetEnvironmentVariable("EQP_AUTH"), "interactive", StringComparison.OrdinalIgnoreCase))
        {
            return new ServiceClient(new Uri(url), _ => Task.FromResult(AzureCliToken(url)), useUniqueInstance: true);
        }

        // Interactive sign-in with Microsoft's published sample app registration; the token is cached
        // outside the repo so later runs don't prompt again.
        var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AB400", "EqpBuilder");
        Directory.CreateDirectory(cacheDir);
        var connectionString =
            $"AuthType=OAuth;Url={url};Username={user};" +
            "AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=http://localhost;" +
            $"LoginPrompt=Auto;TokenCacheStorePath={Path.Combine(cacheDir, "token.cache")}";
        return new ServiceClient(connectionString);
    }

    public static string AzureCliConfigDir
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("EQP_AZURE_CONFIG_DIR");
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AB400", "azcli")
                : Path.GetFullPath(configured);
        }
    }

    internal static string AzureCliToken(string url)
    {
        var resource = new Uri(url).GetLeftPart(UriPartial.Authority);
        var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe",
            $"/c az account get-access-token --resource {resource} --query accessToken -o tsv")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["AZURE_CONFIG_DIR"] = AzureCliConfigDir;
        using var process = System.Diagnostics.Process.Start(psi)!;
        var token = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                $"Couldn't get a token from the Azure CLI profile in {AzureCliConfigDir}. Sign in first with " +
                $"'az login --use-device-code --allow-no-subscriptions --tenant <lab tenant>' and AZURE_CONFIG_DIR set to that folder. {error}");
        }
        return token;
    }

    private static string? GetOption(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
