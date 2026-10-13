---
lab:
    title: 'Process an equipment request with Azure Functions'
    description: 'Deploy an Azure Functions app that uses a managed identity to authenticate to Microsoft Dataverse, then complete an HTTP-triggered function that processes an equipment request asynchronously.'
    level: 400
    duration: 30
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Process an equipment request with Azure Functions

The equipment provisioning system has to check a slow external availability service before it can start fulfilling an approved request. A synchronous Dataverse plug-in would have to block the caller until that external service finally replies. In this exercise, you move that work out of Dataverse entirely and into an Azure Function: an HTTP-triggered function that an event-driven caller can invoke, which checks availability, updates the request, and returns, all without holding open a transaction inside Dataverse. The function authenticates to Dataverse using its own system-assigned managed identity, not a client secret or a signed-in user.

You also deploy, but don't write, a second function that runs on a schedule instead of in response to an event: a nightly job that recalculates how many devices each employee has used against their allowance. Running it once lets you see the difference between the two trigger types this exercise covers: one that reacts to a request, and one that runs on a timer.

In this exercise, you will:

- Deploy a storage account, a Flex Consumption hosting plan, and a function app with a system-assigned managed identity, from a supplied Bicep template
- Create a Dataverse application user for that managed identity and assign it the **Equipment Integration** security role
- Complete an HTTP-triggered function that connects to Dataverse with the managed identity and updates a request's status and availability
- Deploy the function app and trigger it against a real request to watch its status change
- Optionally, run a supplied timer-triggered function once to correct a stale allowance

This exercise should take approximately **30** minutes to complete.

## Prerequisites

To complete this exercise, you need:

- An [Azure subscription](https://azure.microsoft.com/free/){:target="_blank"} at `https://azure.microsoft.com/free/`, with permission to create a resource group and deploy function app resources into it
- A Power Platform developer environment with the **Equipment Provisioning (base)** solution installed (confirmed in the first task below)
- [Visual Studio Code](https://code.visualstudio.com/){:target="_blank"} at `https://code.visualstudio.com/`
- The [.NET SDK](https://dotnet.microsoft.com/download){:target="_blank"} at `https://dotnet.microsoft.com/download`, version 8 or later
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local){:target="_blank"} at `https://learn.microsoft.com/azure/azure-functions/functions-run-local`, version 4
- The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli){:target="_blank"} at `https://learn.microsoft.com/cli/azure/install-azure-cli`

## Confirm the base solution is installed and reset the lab data

Every exercise that uses Dataverse starts the same way: confirm the shared base solution is present, then reset the seed data so the records this exercise uses are in their starting state.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, and select your developer environment.
1. Select **Solutions**, and confirm that **Equipment Provisioning (base)** is listed. If it isn't, follow the import steps in the [setup instructions](./00-setup.md), then come back to this exercise.
1. Open the **Equipment Provisioning** app.
1. In the site map, under **Equipment provisioning**, select **Requests**.
1. On the command bar, select **Reset lab data**. Wait for the progress indicator, then review the summary in the **Lab data reset** dialog.
1. Select **Refresh**.

You should see a dialog summarizing how many employees, devices, requests, and allowances were reset. You'll use request `REQ-SEED-004` and allowance `EMP-002-CURRENT` later in this exercise, so this step guarantees both start from a known state.

## Review the starter function project and deployment template

Before you write any code, look at what's already there. This exercise uses the lab files you cloned during setup, in the `Allfiles/03-azure-functions-request` folder. If you didn't clone the repository, clone it from [GitHub](https://github.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer.git){:target="_blank"} at `https://github.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer.git`.

1. Open the `Allfiles/03-azure-functions-request` folder in Visual Studio Code.
1. Open `EquipmentRequestFunctions/CheckRequestAvailability.cs`. This is the HTTP-triggered function you'll complete. It takes a request number, and has four comment placeholders marking where you'll add code later in this exercise.
1. Open `EquipmentRequestFunctions/SlowAvailabilityService.cs`. This small helper simulates the organization's real external availability service with a delay, which is the reason this work doesn't belong in a synchronous plug-in.
1. Open `EquipmentRequestFunctions/RecalculateAllowances.cs`. This timer-triggered function is already complete. It recalculates every allowance's used and remaining counts from the requests that are actually Fulfilled. You'll deploy and run it once, but you won't change its code.
1. Open `deploy/main.bicep`. Review the resources it provisions: a storage account, a Flex Consumption hosting plan, and a function app with a system-assigned managed identity already enabled. Every resource name is generated from the resource group, so the template produces the same names every time you deploy it into the same resource group.

    If you only need this file, download [main.bicep](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/03-azure-functions-request/deploy/main.bicep){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/03-azure-functions-request/deploy/main.bicep`.

You should now be able to tell the two functions apart: `CheckRequestAvailability` is the one you'll complete, and `RecalculateAllowances` is the one you'll only deploy and run.

## Deploy the Azure resources for the function app

Now provision the storage account, hosting plan, and function app that your code will run in.

1. In a terminal, sign in to Azure:

    ```azurecli
    az login
    ```

1. Create a new resource group named `rg-ab400-lab03`, or select an existing one:

    ```azurecli
    az group create --name rg-ab400-lab03 --location <region>
    ```

    Replace `<region>` with a region close to you, such as `eastus2`.

1. Change to the `deploy` folder, and deploy the template:

    ```azurecli
    cd Allfiles/03-azure-functions-request/deploy
    az deployment group create --resource-group rg-ab400-lab03 --template-file main.bicep --query properties.outputs
    ```

1. From the command's output, note the `functionAppName` value (for example, `func-eqp-abc1234567`). You'll use it in every remaining task.

You should see a JSON object with `functionAppName`, `functionAppDefaultHostName`, `functionAppPrincipalId`, and `storageAccountName` values. Function app host names must be unique across every Azure subscription in the world, so on the rare chance this name is already taken, create a new resource group with a different name and run the deployment again.

## Create a Dataverse application user for the managed identity

An Azure resource's managed identity can't sign in to Dataverse until it exists there as an application user with a security role. Power Platform allows only one application user per Microsoft Entra application per environment, so this step is safe to repeat.

1. Find the managed identity's application ID, using the `functionAppPrincipalId` value from the previous task:

    ```azurecli
    az ad sp show --id <functionAppPrincipalId> --query appId --output tsv
    ```

1. Sign in to the [Power Platform admin center](https://admin.powerplatform.microsoft.com){:target="_blank"} at `https://admin.powerplatform.microsoft.com`.
1. Select **Environments**, then select your developer environment.
1. Select **Settings** > **Users + permissions** > **Application users**.
1. Select **+ New app user**, or select the existing application user for this managed identity if one is already listed.
1. Select **+ Add an app**. Search for the application ID you noted in step 1, select it, and select **Add**.

    > **Note**: For a managed identity, search by its application ID, not its name. Managed identities don't appear in the application list by name.

1. Under **Business unit**, select a business unit.
1. Enter an email address, for example `azure-functions-lab03@example.com`.
1. Select the edit icon next to security roles, and add **Equipment Integration** and **Basic User**. Select **Save**, then confirm the role assignment.
1. Select **Create**.

You should see the new application user listed with the **Equipment Integration** role. If you selected an existing application user instead, confirm it already has that role.

## Complete the Dataverse connection and status update logic

Now wire the function app's managed identity into a Dataverse connection, and use that connection to update a request.

1. In `DataverseConnection.cs`, find the comment **Acquire tokens from the function app's managed identity**, and add the following code:

    ```csharp
    var credential = new DefaultAzureCredential();

    return new ServiceClient(instanceUri, async (string resource) =>
    {
        // ServiceClient passes the full Organization service URL here (the XRMServices path
        // and an SDKClientVersion query string), not a bare origin, so build the scope from
        // just the scheme and host.
        var audience = new Uri(resource).GetLeftPart(UriPartial.Authority);
        var tokenRequestContext = new TokenRequestContext(new[] { $"{audience}/.default" });
        var token = await credential.GetTokenAsync(tokenRequestContext);
        return token.Token;
    });
    ```

    This passes a token provider function to the Dataverse `ServiceClient`, instead of a connection string. Every time the client needs a token, it asks the function app's managed identity for one. The `resource` value `ServiceClient` passes to this callback is the full Organization service URL, complete with path and query string — appending `/.default` to it directly produces an invalid OAuth scope, so the callback first reduces it to just the scheme and host.

1. In `CheckRequestAvailability.cs`, find the comment **Connect to Dataverse by using the function app's managed identity**, and add the following code:

    ```csharp
    using var serviceClient = DataverseConnection.CreateServiceClient(environmentUrl);
    ```

    This creates the connection using the method you just completed, authenticating with the function app's managed identity.

1. Find the comment **Find the request by its request number**, and add the following code:

    ```csharp
    var keys = new KeyAttributeCollection { { "eqp_name", requestNumber } };
    var retrieveRequest = new RetrieveRequest
    {
        Target = new EntityReference("eqp_request", keys),
        ColumnSet = new ColumnSet("eqp_requeststatus", "eqp_availability")
    };
    var request = ((RetrieveResponse)serviceClient.Execute(retrieveRequest)).Entity;
    ```

    This looks up the request by its request number (`REQ-SEED-004`, for example) instead of its internal ID, using the alternate key Dataverse already defines on that column.

1. Find the comment **Simulate the slow external availability service**, and add the following code:

    ```csharp
    var isAvailable = await SlowAvailabilityService.CheckAsync(requestNumber);
    ```

    This calls the supplied helper, which pauses for a few seconds before reporting whether the device is available, standing in for the real external service.

1. Find the comment **Update the request's status and availability columns**, and add the following code:

    ```csharp
    var update = new Entity("eqp_request", request.Id);
    if (isAvailable)
    {
        update["eqp_requeststatus"] = new OptionSetValue(100000003); // In fulfillment
        update["eqp_availability"] = new OptionSetValue(100000001); // Available
        update["eqp_processingnote"] = $"Availability confirmed by the Azure Function on {DateTime.UtcNow:u}.";
    }
    else
    {
        update["eqp_availability"] = new OptionSetValue(100000002); // Unavailable
        update["eqp_processingnote"] = $"The requested device type isn't currently available (checked {DateTime.UtcNow:u}).";
    }
    update["eqp_availabilitycheckedon"] = DateTime.UtcNow;
    serviceClient.Update(update);
    ```

    If the device is available, this moves the request to **In fulfillment**; otherwise, it records **Unavailable** and leaves the request **Approved** for a coordinator to follow up. Either way, it stamps when the check happened and why.

    As you add code, be sure to maintain the correct indentation. Use the existing comments as a guide, entering the new code at the same level of indentation.

    > **Tip**: If you're not sure what to write for one of these steps, ask GitHub Copilot to explain the surrounding code or suggest the next line. The comment above each placeholder is written to be a useful prompt on its own.

1. Save all your changed files, and build the project to confirm it compiles:

    ```bash
    dotnet build
    ```

You should see `Build succeeded` with no errors.

## Deploy and configure the function app

With the code complete, deploy it to the function app you provisioned earlier, then tell it which Dataverse environment to connect to.

1. From the `EquipmentRequestFunctions` folder, publish the project:

    ```bash
    func azure functionapp publish <functionAppName> --dotnet-isolated
    ```

    Replace `<functionAppName>` with the value you noted earlier. The `--dotnet-isolated` flag identifies the project's runtime without needing a local settings file. Publishing can take a minute or two. When it finishes, the output lists both functions and, for `CheckRequestAvailability`, an invoke URL similar to `https://<functionAppName>.azurewebsites.net/api/checkrequestavailability`.

1. Set the function app's Dataverse connection setting:

    ```azurecli
    az functionapp config appsettings set --name <functionAppName> --resource-group rg-ab400-lab03 --settings DATAVERSE_ENVIRONMENT_URL=<environment-url>
    ```

    Replace `<environment-url>` with your Dataverse environment's URL, for example `https://yourorg.crm.dynamics.com`. You can find it on the environment's details page in the Power Platform admin center.

1. Verify the stored Dataverse URL:

    ```azurecli
    az functionapp config appsettings list --name <functionAppName> --resource-group rg-ab400-lab03 --query "[?name=='DATAVERSE_ENVIRONMENT_URL'].value" --output tsv
    ```

    The setting command might return `null` instead of echoing the value. This read-only query should print the environment URL you supplied.

## Trigger the function and verify the result

This is the moment the whole exercise has been building toward: invoke the deployed function against a real request, and watch Dataverse change in response.

1. Open the following URL in a browser, or call it with a tool such as curl, replacing `<functionAppName>` with your function app's name:

    ```text
    https://<functionAppName>.azurewebsites.net/api/checkrequestavailability?requestNumber=REQ-SEED-004
    ```

1. Wait a few seconds. The function deliberately pauses to simulate the slow external service before it responds.
1. Confirm you see a response similar to `Finished checking availability for REQ-SEED-004.`
1. Back in the **Equipment Provisioning** app, open **Requests**, and open `REQ-SEED-004`.

You should see **Request status** has changed from **Approved** to **In fulfillment**, **Availability** has changed from **Not checked** to **Available**, and **Availability checked on** and **Processing note** are both populated. That delay you waited through is exactly why this work runs in an Azure Function instead of inside a Dataverse plug-in: a plug-in would have held the save operation open for those same few seconds.

> **Try this**: The supplied `RecalculateAllowances` function is already deployed. In the [Azure portal](https://portal.azure.com){:target="_blank"} at `https://portal.azure.com`, open your function app, select **Functions**, select **RecalculateAllowances**, select **Code + Test**, and use the **Test/Run** pane to run it once on demand instead of waiting for its schedule. Afterward, open the Allowance `EMP-002-CURRENT` in the **Equipment Provisioning** app: its **Devices used** and **Remaining** values correct from the stale 2 and 0 to 0 and 2, and **Last recalculated on** updates. This isn't required to complete the exercise, but it's worth about two minutes if you have time, because it's the only place in this exercise you see a scheduled trigger run.

## Summary

In this exercise, you deployed an Azure Functions app to a Flex Consumption plan using a supplied Bicep template, and connected it to Dataverse through a system-assigned managed identity instead of a client secret. You completed an HTTP-triggered function that looked up a request by its alternate key, simulated a slow external dependency, and updated the request's status and availability columns. You also deployed a supplied timer-triggered function and, optionally, ran it on demand to see a scheduled trigger correct a stale allowance.

You have successfully completed this exercise.

## Clean up

If you've finished exploring the Azure resources used in this exercise, delete them to avoid incurring unnecessary costs.

1. Open the [Azure portal](https://portal.azure.com){:target="_blank"} at `https://portal.azure.com`, and view the contents of the resource group you used in this exercise (`rg-ab400-lab03`, unless you chose a different name).
1. On the toolbar, select **Delete resource group**.
1. Enter the resource group name, and confirm that you want to delete it.

Deleting the resource group also deletes the function app's managed identity. You can leave the Dataverse application user in place; it's unlicensed and harmless to keep. If you later redeploy this exercise's resources into the same or a different resource group, the new function app gets a new managed identity, so the existing application user will no longer authenticate successfully. In that case, remove the old application user and create a new one by repeating the earlier task.
