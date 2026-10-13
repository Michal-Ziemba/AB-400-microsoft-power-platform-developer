---
lab:
    title: 'Perform resilient Dataverse API operations'
    description: 'Use the Dataverse Web API, the Organization service, and the Dataverse SDKs for .NET and Python to create, retrieve, update, and query equipment-provisioning data, then add a bounded retry for a simulated transient error.'
    level: 300
    duration: 30
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Perform resilient Dataverse API operations

The equipment-provisioning solution you're extending exposes its data through more than one Dataverse API surface, and part of a developer's job is choosing the right one for the operation at hand: a host (Web API or Organization service), a language (.NET or Python), an authentication method, and an operation that may or may not survive a busy server. In this exercise you work with three of those surfaces against the same Dataverse environment, and you make one of them resilient to a transient failure instead of just hoping it doesn't happen.

In this exercise, you will:

- Create, retrieve, and update an equipment request through the raw Dataverse Web API
- Query device, employee, and request history across three tables with a FetchXML query through the Organization service
- Observe how the Dataverse SDK for .NET automatically retries a throttled request
- Add a bounded, `Retry-After`-aware retry loop to a Python script, and prove it recovers from a simulated transient error

This exercise should take approximately **30** minutes to complete.

You need the .NET SDK and Python 3.10 or later installed, and network access to install packages from PyPI (`pip install`) for the Python part of this exercise.

## Confirm the base solution and reset your lab data

Every Dataverse exercise in this course starts the same way: confirm the shared base solution is present, then reset the seed data to its starting values.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`.

1. Confirm the environment selector shows the environment you want to use for these exercises.

1. Select **Solutions**, and confirm that **Equipment Provisioning (base)** is listed. If it isn't, follow the import steps in [the lab setup](./00-setup.md) before you continue.

1. Open the **Equipment Provisioning** app, go to **Requests**, and on the command bar select **Reset lab data**.

1. Wait for the reset to finish, review the summary message, then select **Refresh**.

    <!-- SCREENSHOT: the "Lab data reset" confirmation dialog after selecting Reset lab data on the Requests view. -->

You should see a summary confirming 5 employees, 9 devices, 6 requests, and 4 allowances were checked, and that your own account is linked to employee `EMP-001`.

## Configure environment-neutral authentication for the .NET and Python clients

The starter projects for this exercise are in `Allfiles/02-resilient-dataverse-api/dotnet` and `Allfiles/02-resilient-dataverse-api/python`, in the lab files you cloned in the setup. Both projects sign in interactively, so neither one needs an app registration, a client secret, or an admin consent step. The .NET project uses the same sample application ID Microsoft publishes for development and prototyping in its own Dataverse SDK for .NET quickstart. The Python project uses a browser-based sign-in through Azure Identity.

1. Open `Allfiles/02-resilient-dataverse-api/dotnet/appsettings.json` and replace the placeholder value with your Dataverse environment URL, for example `https://yourorg.crm.dynamics.com`. Save the file.

    If you didn't clone the repository in the lab setup, download [appsettings.json](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/02-resilient-dataverse-api/dotnet/appsettings.json){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/02-resilient-dataverse-api/dotnet/appsettings.json`.

1. In a terminal, open the `Allfiles/02-resilient-dataverse-api/dotnet` folder and run the project:

    ```bash
    dotnet run
    ```

1. Sign in with your credentials when prompted. You're prompted twice in this first run: once for the Web API client, and once for the Organization service client.

1. Confirm the output includes two success lines similar to:

    ```output
    Web API sign-in succeeded. {"@odata.context":"...","BusinessUnitId":"...","UserId":"...","OrganizationId":"..."}
    Organization service sign-in succeeded. User ID: 00aa00aa-bb11-cc22-dd33-44ee44ee44ee
    ```

1. Open `Allfiles/02-resilient-dataverse-api/python/config.json` and set the same environment URL. Save the file.

1. In a terminal, open the `Allfiles/02-resilient-dataverse-api/python` folder, create and activate a virtual environment, then install the SDK:

    ```bash
    python -m venv .venv
    .venv\Scripts\activate
    pip install -r requirements.txt
    ```

    On macOS or Linux, activate the virtual environment with `source .venv/bin/activate` instead.

1. Run the script:

    ```bash
    python query_devices.py
    ```

1. Sign in with your credentials when the browser opens. Confirm the output shows a line similar to the following, with no errors:

    ```output
    Python sign-in succeeded. Found employee: Avery Chen.
    ```

## Create, retrieve, and update a request through the Dataverse Web API

The Dataverse Web API is an OData v4 REST endpoint: every operation is a plain HTTP call. You already have an authenticated `webApiClient` from the previous task, so the three calls in this task are just HTTP requests with a JSON body.

1. In `Program.cs`, find the comment **Create a request through the Web API** and add the following code:

    ```csharp
    // Create a request through the Web API
    var newRequest = new Dictionary<string, object>
    {
        ["eqp_requesteddevicetype"] = 100000000, // Laptop
        ["eqp_justification"] = "My laptop is more than 24 months old. I'd like a replacement.",
        ["eqp_Employee@odata.bind"] = "/eqp_employees(eqp_employeenumber='EMP-001')",
        ["eqp_CurrentDevice@odata.bind"] = "/eqp_devices(eqp_name='LAP-1001')"
    };

    using StringContent createContent = new(JsonSerializer.Serialize(newRequest), Encoding.UTF8, "application/json");
    HttpResponseMessage createResponse = await webApiClient.PostAsync("eqp_requests", createContent);
    createResponse.EnsureSuccessStatusCode();

    string entityUri = createResponse.Headers.GetValues("OData-EntityId").First();
    Guid requestId = Guid.Parse(entityUri.Split('(', ')')[1]);
    Console.WriteLine($"Created request {requestId}");
    ```

    The `@odata.bind` properties use alternate keys (`eqp_employeenumber` and the device's asset tag, `eqp_name`) instead of GUIDs, so you don't need a lookup call first. `EMP-001` is your own employee row, and `LAP-1001` is the laptop it's already assigned, so this request reads naturally as "I'd like to replace my own laptop."

    > **Note**: The `@odata.bind` property name must match the lookup column's schema name casing exactly — `eqp_Employee` and `eqp_CurrentDevice`, not the all-lowercase logical name you see everywhere else in this exercise. The Web API treats these names case-sensitively, and a lowercase `eqp_employee@odata.bind` fails with an "undeclared property" error. You can always find a lookup's exact schema-name casing in the maker portal's table column list.

1. Run the project again:

    ```bash
    dotnet run
    ```

1. Confirm the console shows a line similar to `Created request 3fa85f64-5717-4562-b3fc-2c963f66afa6`.

1. Find the comment **Retrieve the request through the Web API** and add the following code:

    ```csharp
    // Retrieve the request through the Web API
    HttpResponseMessage retrieveResponse = await webApiClient.GetAsync(
        $"eqp_requests({requestId})?$select=eqp_name,eqp_requeststatus,eqp_justification");
    retrieveResponse.EnsureSuccessStatusCode();
    Console.WriteLine($"Retrieved request: {await retrieveResponse.Content.ReadAsStringAsync()}");
    ```

1. Run the project again. Confirm the retrieved JSON includes `"eqp_requeststatus":100000000`, the default value for a new request (Submitted).

1. Find the comment **Update the request through the Web API** and add the following code:

    ```csharp
    // Update the request through the Web API
    var update = new Dictionary<string, object>
    {
        ["eqp_requeststatus"] = 100000005, // Needs attention
        ["eqp_processingnote"] = "Flagged for manual review by the resilient Dataverse API exercise."
    };

    using StringContent updateContent = new(JsonSerializer.Serialize(update), Encoding.UTF8, "application/json");
    HttpResponseMessage updateResponse = await webApiClient.PatchAsync($"eqp_requests({requestId})", updateContent);
    updateResponse.EnsureSuccessStatusCode();

    HttpResponseMessage confirmResponse = await webApiClient.GetAsync(
        $"eqp_requests({requestId})?$select=eqp_name,eqp_requeststatus");
    confirmResponse.EnsureSuccessStatusCode();
    Console.WriteLine($"After update: {await confirmResponse.Content.ReadAsStringAsync()}");
    ```

    The update sets the status to **Needs attention**, never **Approved**. Approving a request is governed by an eligibility rule enforced elsewhere in this course, so this exercise deliberately avoids it.

1. Run the project one more time. Confirm the final JSON now includes `"eqp_requeststatus":100000005`.

## Query device and request history with FetchXML through the Organization service

A single Web API call is a good fit for working with one table at a time. Finding every device issued in a particular window, together with the employee it's assigned to and the request history against it, means joining three tables at once — something FetchXML expresses directly with `link-entity` elements, where the Web API would need multiple `$expand` calls across two different relationships.

1. Find the comment **Query devices and requests with FetchXML** and add the following code:

    ```csharp
    // Query devices and requests with FetchXML
    string fetchXml = """
        <fetch>
          <entity name="eqp_device">
            <attribute name="eqp_name" />
            <attribute name="eqp_devicetype" />
            <attribute name="eqp_issuedon" />
            <order attribute="eqp_issuedon" />
            <filter type="and">
              <condition attribute="eqp_issuedon" operator="last-x-months" value="25" />
              <condition attribute="eqp_issuedon" operator="olderthan-x-months" value="22" />
            </filter>
            <link-entity name="eqp_employee" from="eqp_employeeid" to="eqp_assignedto" alias="employee" link-type="inner">
              <attribute name="eqp_name" />
              <attribute name="eqp_employeenumber" />
            </link-entity>
            <link-entity name="eqp_request" from="eqp_currentdevice" to="eqp_deviceid" alias="request" link-type="outer">
              <attribute name="eqp_name" />
              <attribute name="eqp_requeststatus" />
            </link-entity>
          </entity>
        </fetch>
        """;

    EntityCollection results = serviceClient.RetrieveMultiple(new FetchExpression(fetchXml));

    Console.WriteLine($"Found {results.Entities.Count} device(s) issued 23 to 25 months ago:");
    foreach (Entity device in results.Entities)
    {
        string employeeName = device.GetAttributeValue<AliasedValue>("employee.eqp_name")?.Value?.ToString() ?? "(none)";
        string requestNumber = device.GetAttributeValue<AliasedValue>("request.eqp_name")?.Value?.ToString() ?? "(no request)";
        Console.WriteLine($"- {device["eqp_name"]} issued {device["eqp_issuedon"]}, assigned to {employeeName}, request {requestNumber}");
    }
    ```

    The two `condition` elements together select devices issued between roughly 22 and 25 months ago. The two `link-entity` elements bring in the assigned employee (an inner join, since every issued device has one) and any request raised against that device (an outer join, since not every device has one).

1. Run the project again:

    ```bash
    dotnet run
    ```

1. Confirm the output lists two devices, `MON-1005` and `TAB-1006`, both assigned to employee Taylor Brooks, each with its own request number. These two devices sit on either side of the 24-month eligibility boundary used elsewhere in this course: `MON-1005` was issued exactly 24 months ago, and `TAB-1006` only 23.

> **Note**: The Dataverse SDK for .NET's `ServiceClient` already retries a request automatically if Dataverse responds with a `Retry-After` header, so no retry code is needed here. That's also why the next task writes its retry logic in Python instead: the Dataverse SDK for Python doesn't retry `HttpError` responses for you, so your application has to.

## Run the same kind of query with the Dataverse SDK for Python, then add a bounded retry

The Python script queries issued devices the same way, through the SDK's query builder, but a supplied helper, `flaky_query.py`, simulates one transient Dataverse error before the query succeeds — so you can prove a retry works without waiting for real throttling in a shared tenant.

1. In `query_devices.py`, find the comment **Run the query without a retry** and add the following code:

    ```python
    # Run the query without a retry
    devices = flaky_query.query_issued_devices(client)
    for device in devices:
        print(f"- {device['eqp_name']} ({device['eqp_devicetype']}) issued {device['eqp_issuedon']}")
    ```

1. Run the script:

    ```bash
    python query_devices.py
    ```

1. Confirm it signs in successfully, then crashes with an unhandled `HttpError`, ending with a message similar to:

    ```output
    PowerPlatform.Dataverse.core.errors.HttpError: The availability service is temporarily unavailable. Try again later.
    ```

    This is the simulated transient error from `flaky_query.py`, not real Dataverse throttling. The query itself never ran.

1. Replace the code you just added with the following, which wraps the same call in a bounded retry:

    ```python
    # Run the query with a bounded retry
    max_attempts = 3
    devices = None
    for attempt in range(1, max_attempts + 1):
        try:
            devices = flaky_query.query_issued_devices(client)
            break
        except HttpError as e:
            if e.is_transient and attempt < max_attempts:
                wait_seconds = e.details.get("retry_after", 2)
                print(
                    f"Attempt {attempt} hit a transient error "
                    f"(HTTP {e.status_code}). Retrying in {wait_seconds}s..."
                )
                time.sleep(wait_seconds)
            else:
                raise

    print(f"Found {len(devices)} issued device(s):")
    for device in devices:
        print(f"- {device['eqp_name']} ({device['eqp_devicetype']}) issued {device['eqp_issuedon']}")
    ```

    The loop only retries when `e.is_transient` is `True`, waits for the number of seconds Dataverse's `Retry-After` value asked for (`e.details.get("retry_after")`), and gives up after `max_attempts` tries instead of retrying forever.

1. Run the script again:

    ```bash
    python query_devices.py
    ```

1. Confirm the output now shows the same error being caught, a short wait, and then success:

    ```output
    Python sign-in succeeded. Found employee: Avery Chen.
    Attempt 1 hit a transient error (HTTP 429). Retrying in 2s...
    Found 6 issued device(s):
    - LAP-1004 (100000000) issued ...
    - PHN-1002 (100000002) issued ...
    - MON-1005 (100000001) issued ...
    - TAB-1006 (100000003) issued ...
    - LAP-1001 (100000000) issued ...
    - LAP-1003 (100000000) issued ...
    ```

    > **Note**: `eqp_devicetype` is a choice column, so the query returns its underlying option value (for example `100000000` for Laptop), not the label. To see the label instead, chain `.include_formatted_values()` onto the query and read `device['eqp_devicetype@OData.Community.Display.V1.FormattedValue']`.

    This is the payoff: the same call that crashed the script a few minutes ago now fails once, waits exactly as long as Dataverse's `Retry-After` value told it to, and succeeds on its own, without you touching the keyboard in between.

1. When you've finished testing, deactivate the Python virtual environment:

    ```bash
    deactivate
    ```

## Clean up

Nothing in this exercise needs to be turned off or uninstalled: there's no plug-in step, no flow, and no Azure resource. The only thing you created is a request row, and nothing else in this course reads it, so it's safe to leave in place.

If you'd like to tidy it up anyway:

1. In the **Equipment Provisioning** app, go to **Requests**.

1. Select the request you created through the Web API (its request number, such as `REQ-00007`, appeared in the JSON you retrieved earlier).

1. On the command bar, select **Delete**, and confirm.

## Summary

In this exercise, you worked across three Dataverse API surfaces against the same equipment-provisioning data. You created, retrieved, and updated a request through the raw Dataverse Web API using alternate-key `@odata.bind` references. You wrote a FetchXML query through the Organization service that joined three tables at once, something the Web API alone couldn't express as simply, and saw that the Dataverse SDK for .NET already retries a throttled request for you. Finally, you wrote a bounded, `Retry-After`-aware retry loop against the Dataverse SDK for Python and proved it recovers from a transient error that would otherwise have crashed the script.

You have successfully completed this exercise.
