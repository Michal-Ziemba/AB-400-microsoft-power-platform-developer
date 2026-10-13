using System.Net.Http;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Technical checks for F1 (lab-plan\lab-plan-summary.md, build step 8). Not a learner walkthrough.
// Leaves the environment as it found it, apart from resetting the seed rows.
internal sealed class Verifier(ServiceClient client)
{
    private int _failures;

    public int Run()
    {
        var me = ((WhoAmIResponse)client.Execute(new WhoAmIRequest())).UserId;

        // F3: Reset lab data runs twice in a row with the same result.
        var first = Reset();
        var countsAfterFirst = SeedCounts();
        var second = Reset();
        var countsAfterSecond = SeedCounts();
        Check("Reset lab data returns a summary", !string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second), second);
        Check("A second reset writes nothing (so it doesn't fire other labs' flows or plug-ins)",
            second.Contains(" 0 created, 0 restored, 24 already correct"), second);
        Check("Seed rows: 5 employees, 9 devices, 6 requests, 4 allowances",
            countsAfterFirst == (5, 9, 6, 4), $"{countsAfterFirst}");
        Check("Running Reset lab data twice creates no duplicates", countsAfterSecond == countsAfterFirst, $"{countsAfterSecond}");

        var emp1 = RetrieveByName("eqp_employee", "eqp_employeenumber", "EMP-001", "eqp_user");
        Check("EMP-001 is linked to the caller", emp1?.GetAttributeValue<EntityReference>("eqp_user")?.Id == me, "");

        var req4 = RetrieveByName("eqp_request", "eqp_name", "REQ-SEED-004", "eqp_requeststatus");
        Check("REQ-SEED-004 is Approved", req4?.GetAttributeValue<OptionSetValue>("eqp_requeststatus")?.Value == 100000001, "");

        var stale = RetrieveByName("eqp_allowance", "eqp_name", "EMP-002-CURRENT", "eqp_devicesused", "eqp_remaining");
        Check("EMP-002-CURRENT is the planted stale row (used 2, remaining 0)",
            stale?.GetAttributeValue<int>("eqp_devicesused") == 2 && stale.GetAttributeValue<int>("eqp_remaining") == 0, "");

        // F2: the eligibility API gives the expected answer for every seed device (foundation §2 and §3).
        var expected = new Dictionary<string, bool>
        {
            ["LAP-1001"] = false, // 8 months, not damaged
            ["PHN-1002"] = true,  // 26 months
            ["LAP-1003"] = true,  // 8 months, damaged beyond repair
            ["LAP-1004"] = true,  // 30 months
            ["MON-1005"] = true,  // exactly 24 months (boundary)
            ["TAB-1006"] = false, // 23 months
            ["LAP-2001"] = true,  // no issue date
        };
        foreach (var (assetTag, eligible) in expected)
        {
            var device = RetrieveByName("eqp_device", "eqp_name", assetTag);
            var response = client.Execute(new OrganizationRequest("eqp_IsEligibleForReplacement") { ["DeviceId"] = device!.Id });
            var actual = (bool)response["IsEligible"];
            Check($"Eligibility of {assetTag} is {eligible}", actual == eligible, $"returned {actual}");
        }

        // F4: the availability stand-in follows eqp_AvailabilityMode.
        var request3 = RetrieveByName("eqp_request", "eqp_name", "REQ-SEED-003")!;
        var definition = RetrieveByName("environmentvariabledefinition", "schemaname", "eqp_AvailabilityMode")!;
        var priorValue = FindCurrentValue(definition.Id);
        try
        {
            SetMode(definition.Id, null);
            Check("Availability with the default mode (Available) is true", Availability(request3.Id) == true, "");
            SetMode(definition.Id, "Unavailable");
            Check("Availability in Unavailable mode is false", Availability(request3.Id) == false, "");
            SetMode(definition.Id, "Error");
            var (status, body) = AvailabilityOverWebApi(request3.Id);
            Check("Error mode fails with HTTP 503 through the Web API", status == 503, $"HTTP {status}: {Truncate(body)}");
        }
        finally
        {
            SetMode(definition.Id, priorValue);
        }

        Console.WriteLine(_failures == 0 ? "All checks passed." : $"{_failures} check(s) failed.");
        return _failures == 0 ? 0 : 1;
    }

    private string Reset() =>
        (string)client.Execute(new OrganizationRequest("eqp_ResetLabData"))["Summary"];

    private (int, int, int, int) SeedCounts() => (
        Count("eqp_employee", "eqp_employeenumber", "EMP-00"),
        Count("eqp_device", "eqp_name", "LAP-", "PHN-", "MON-", "TAB-"),
        Count("eqp_request", "eqp_name", "REQ-SEED-"),
        Count("eqp_allowance", "eqp_name", "EMP-00"));

    private int Count(string table, string column, params string[] prefixes)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(false) };
        var filter = new FilterExpression(LogicalOperator.Or);
        foreach (var prefix in prefixes)
        {
            filter.AddCondition(column, ConditionOperator.BeginsWith, prefix);
        }
        query.Criteria.AddFilter(filter);
        return client.RetrieveMultiple(query).Entities.Count;
    }

    private Entity? RetrieveByName(string table, string column, string value, params string[] columns)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(columns), TopCount = 1 };
        query.Criteria.AddCondition(column, ConditionOperator.Equal, value);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    private bool? Availability(Guid requestId) =>
        (bool)client.Execute(new OrganizationRequest("eqp_CheckAvailability") { ["RequestId"] = requestId })["IsAvailable"];

    private (int Status, string Body) AvailabilityOverWebApi(Guid requestId)
    {
        // Call the Web API directly, the way a cloud flow's HTTP action would, to see the real status code.
        var baseUrl = client.ConnectedOrgUriActual.GetLeftPart(UriPartial.Authority);
        using var http = new HttpClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/data/v9.2/eqp_CheckAvailability")
        {
            Content = new StringContent($"{{\"RequestId\":\"{requestId}\"}}", Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Program.AzureCliToken(baseUrl));
        using var response = http.Send(message);
        return ((int)response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
    }

    private string? FindCurrentValue(Guid definitionId)
    {
        var value = FindValueRecord(definitionId);
        return value?.GetAttributeValue<string>("value");
    }

    private Entity? FindValueRecord(Guid definitionId)
    {
        var query = new QueryExpression("environmentvariablevalue") { ColumnSet = new ColumnSet("value"), TopCount = 1 };
        query.Criteria.AddCondition("environmentvariabledefinitionid", ConditionOperator.Equal, definitionId);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    // Sets the current value, or removes it (null) so the default applies.
    private void SetMode(Guid definitionId, string? value)
    {
        var existing = FindValueRecord(definitionId);
        if (value is null)
        {
            if (existing is not null)
            {
                client.Delete("environmentvariablevalue", existing.Id);
            }
            return;
        }
        if (existing is null)
        {
            client.Create(new Entity("environmentvariablevalue")
            {
                ["environmentvariabledefinitionid"] = new EntityReference("environmentvariabledefinition", definitionId),
                ["value"] = value,
                ["schemaname"] = $"eqp_AvailabilityMode_{Guid.NewGuid():N}"[..40],
            });
        }
        else
        {
            client.Update(new Entity("environmentvariablevalue", existing.Id) { ["value"] = value });
        }
    }

    private void Check(string name, bool passed, string detail)
    {
        if (!passed)
        {
            _failures++;
        }
        Console.WriteLine($"[{(passed ? "PASS" : "FAIL"),-4}] {name}{(string.IsNullOrEmpty(detail) ? "" : $" ({detail})")}");
    }

    private static string Truncate(string text) => text.Length > 160 ? text[..160] + "…" : text;
}
