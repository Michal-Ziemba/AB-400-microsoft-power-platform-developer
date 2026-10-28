---
lab:
    title: 'Build and register an equipment-validation plug-in'
    description: 'Write, sign, and register a Microsoft Dataverse plug-in that uses a pre-image and the Organization service to enforce a replacement-eligibility rule, then catalog the rule as a Dataverse business event.'
    level: 300
    duration: 35
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Build and register an equipment-validation plug-in

Equipment Provisioning is a Microsoft Dataverse solution that lets employees request replacement equipment and lets coordinators review and approve those requests. A replacement is only allowed if the device being replaced is at least 24 months old, or is flagged as damaged beyond repair — but today that rule is enforced only by a script on the request form, so it can be bypassed by anyone who updates a request through the Dataverse Web API instead of the form.

In this exercise, you write and register a Dataverse plug-in that enforces the same rule on the server, where it can't be bypassed. You use the Dataverse SDK for .NET, a pre-image to read the request's previous status, and the Organization service to look up the device being replaced. You register the plug-in with the Plug-in Registration Tool, then prove it closes the gap the form-only check left open. Finally, you catalog the rule's custom API as a Dataverse business event, so other automation can discover and react to it.

In this exercise, you will:

- Try to approve an ineligible request directly through the Dataverse Web API, bypassing the form's client-side check
- Write a plug-in that reads a pre-image and queries Dataverse through the Organization service to enforce the eligibility rule
- Register the plug-in with the Plug-in Registration Tool
- Confirm the plug-in blocks the same bypass that worked before — and still allows a legitimate exception
- Catalog the eligibility rule's custom API as a Dataverse business event

This exercise should take approximately **35** minutes to complete.

## Prerequisites

To complete this exercise, you need:

- A Windows computer. Visual Studio and the Plug-in Registration Tool are Windows-only desktop applications, and this exercise has no cross-platform alternative.
- [Visual Studio](https://visualstudio.microsoft.com/){:target="_blank"} at `https://visualstudio.microsoft.com/`, 2022 or later, with the **Class Library (.NET Framework)** project template available (installed with the **.NET desktop development** workload).
- The .NET Framework 4.8 targeting pack. If it isn't already installed, Visual Studio prompts you to install it the first time you build a project that targets it.
- The [Power Platform CLI](https://learn.microsoft.com/power-platform/developer/cli/introduction){:target="_blank"} at `https://learn.microsoft.com/power-platform/developer/cli/introduction`, used to launch the Plug-in Registration Tool.
- The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli){:target="_blank"} at `https://learn.microsoft.com/cli/azure/install-azure-cli`, used in this exercise to get an access token for a direct Web API call.
- A Dataverse environment where you have the **System Administrator** or **System Customizer** security role. The owner of a developer environment already has this role by default.

> **Note**: If you have GitHub Copilot available in Visual Studio, feel free to use it to help draft or review the plug-in code in this exercise — it isn't required, and every code block you need is provided.

## Before you start

This exercise assumes you already have a Dataverse developer environment with the **Equipment Provisioning (base)** solution installed, as described in [Set up your lab environment](./00-setup.md). If you haven't completed that setup, do so now before continuing.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, and confirm the environment selector shows the environment you're using for these exercises.

1. Select **Solutions**, and confirm that **Equipment Provisioning (base)** appears in the list. If it doesn't, follow the import steps in [Set up your lab environment](./00-setup.md), then come back here.

1. Download [EquipmentProvisioningLab01_managed.zip](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/EquipmentProvisioningLab01_managed.zip){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/EquipmentProvisioningLab01_managed.zip`. This solution adds a **Request intake** form to the Request table, with the client-side eligibility check you'll bypass in the next task.

1. On the **Solutions** page, select **Import solution**, select **Browse**, choose the file you downloaded, and select **Next**, then **Import**. Wait for the import to finish.

1. Open the **Equipment Provisioning** app and select **Requests** in the site map.

1. On the command bar of the **Active Requests** view, select **Reset lab data**. Wait for it to finish, review the summary in the **Lab data reset** dialog, close the dialog, and select **Refresh**.

   You should see `REQ-SEED-001` through `REQ-SEED-004` listed as open requests, each with the status described in the data summary.

## Try to approve an ineligible request

Before you write any code, see the gap you're about to close. `REQ-SEED-001` requests a replacement for a laptop that was issued 8 months ago and isn't damaged — it shouldn't be eligible for another 16 months. A client-side check on the request form already stops a coordinator from approving it by mistake, but that check only runs in the browser.

1. In the **Equipment Provisioning** app, open `REQ-SEED-001`.

1. Select the form selector at the top of the record (it shows the current form's name), and switch to the **Request intake** form.

1. Set **Request status** to **Approved**, and then save the record.

   You should see a form notification similar to *This device was issued on {date}, less than 24 months ago, and isn't damaged beyond repair. The request can't be approved yet.* The save is blocked, and **Request status** remains **Submitted**.

1. Select the gear icon (**Settings**) in the upper-right corner of the maker portal, then select **Developer resources**. Copy the **Web API endpoint** value — you'll use it as the environment URL for the rest of this exercise.

1. Download [Approve-RequestViaWebApi.ps1](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/Approve-RequestViaWebApi.ps1){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/Approve-RequestViaWebApi.ps1`. This script gets an access token for your environment with the Azure CLI, then sends the same status change directly to the Dataverse Web API — no form, and no client-side check.

1. In a terminal, sign in to the Azure CLI using your credentials. The `--allow-no-subscriptions` option lets you sign in even if your account has no Azure subscription; this script needs only access to your Power Platform environment:

    ```powershell
    az login --allow-no-subscriptions
    ```

    > **Note**: If you use the Azure CLI with more than one account, sign in with the account you use for your Power Platform environment. If that account belongs to a different tenant than your usual one, add `--tenant` followed by your tenant's domain name.

1. Run the script, passing the environment URL you copied and the request number:

    ```powershell
    .\Approve-RequestViaWebApi.ps1 -EnvironmentUrl "<your environment URL>" -RequestNumber "REQ-SEED-001"
    ```

1. Go back to the **Equipment Provisioning** app and refresh `REQ-SEED-001`.

   You should see **Request status** is now **Approved** — the same change the form just blocked a moment ago succeeded through the Web API. This is the gap the rest of the exercise closes.

## Implement the eligibility rule in the plug-in

A client-side script can never close this gap on its own, because it only runs when a form is saved. You need a plug-in: code that Dataverse itself runs whenever a request is updated, no matter which client made the change.

The lab files you cloned in the setup include a starter project at `Allfiles/01-equipment-validation-plugin/AB400.Lab01`. It already references the `Microsoft.CrmSdk.CoreAssemblies` package and has a stubbed plug-in class with comments marking where your code goes. If you didn't clone the repository, download these files individually, keeping the same folder structure:

- [AB400.Lab01.csproj](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/AB400.Lab01/AB400.Lab01.csproj){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/AB400.Lab01/AB400.Lab01.csproj`, into an `AB400.Lab01` folder
- [RequestApprovalPlugin.cs](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/AB400.Lab01/RequestApprovalPlugin.cs){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/AB400.Lab01/RequestApprovalPlugin.cs`, into the same `AB400.Lab01` folder
- [New-StrongNameKey.ps1](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/New-StrongNameKey.ps1){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/01-equipment-validation-plugin/New-StrongNameKey.ps1`, into the folder that contains `AB400.Lab01`

1. Dataverse only accepts signed plug-in assemblies, and strong-name keys aren't shipped with the lab files, so create your own. In a terminal, in the `Allfiles/01-equipment-validation-plugin` folder, run:

    ```powershell
    .\New-StrongNameKey.ps1
    ```

    The script creates `AB400.Lab01\AB400.Lab01.snk`, the key file the project is already set up to sign with. You should see **Created the strong-name key**.

1. In Visual Studio, select **File** > **Open** > **Project/Solution**, and open `AB400.Lab01.csproj`.

1. In **Solution Explorer**, open `RequestApprovalPlugin.cs`. Review the comment block above the class — it records exactly how you'll register this plug-in in the next task: `Update` of `eqp_request`, `PreOperation`, synchronous, filtered on `eqp_requeststatus`, with a pre-image named `PreImage` that carries `eqp_requeststatus` and `eqp_currentdevice`.

1. Find the comment **Confirm the request is being approved (read the Target and the pre-image)** and add the following code:

    ```csharp
    // Confirm the request is being approved (read the Target and the pre-image)
    var target = (Entity)context.InputParameters["Target"];
    if (!target.Attributes.Contains("eqp_requeststatus"))
    {
        return;
    }

    var newStatus = ((OptionSetValue)target["eqp_requeststatus"]).Value;
    if (newStatus != StatusApproved)
    {
        return;
    }

    var preImage = context.PreEntityImages["PreImage"];
    var previousStatus = ((OptionSetValue)preImage["eqp_requeststatus"]).Value;
    if (previousStatus == StatusApproved)
    {
        return;
    }
    ```

    The `Target` carries only the attributes included in the update — here, just the new `eqp_requeststatus` value, because that's the filtering attribute. The pre-image carries the request's values *before* the update, so comparing `previousStatus` to the new value is how the plug-in detects an actual transition into Approved, rather than a save that leaves an already-approved request unchanged.

1. Find the comment **Retrieve the device being replaced** and add the following code:

    ```csharp
    // Retrieve the device being replaced
    var deviceRef = preImage.GetAttributeValue<EntityReference>("eqp_currentdevice");

    Entity device;
    try
    {
        device = service.Retrieve("eqp_device", deviceRef.Id, new ColumnSet("eqp_issuedon", "eqp_damagedbeyondrepair"));
    }
    catch (FaultException<OrganizationServiceFault>)
    {
        throw new InvalidPluginExecutionException("The device being replaced couldn't be found.");
    }
    ```

    `eqp_currentdevice` isn't part of this update, so it comes from the pre-image too. The `IOrganizationService.Retrieve` call reads only the two columns the rule needs — the issue date and the damaged flag.

1. Find the comment **Apply the replacement-eligibility rule** and add the following code:

    ```csharp
    // Apply the replacement-eligibility rule
    var issuedOn = device.GetAttributeValue<DateTime?>("eqp_issuedon");
    var damagedBeyondRepair = device.GetAttributeValue<bool>("eqp_damagedbeyondrepair");

    var isEligible = damagedBeyondRepair
        || !issuedOn.HasValue
        || issuedOn.Value.Date <= DateTime.UtcNow.Date.AddMonths(-24);
    ```

    This is the rule: a device is eligible for replacement if it's flagged damaged beyond repair, if it has no recorded issue date, or if it was issued on or before today (in UTC) minus 24 calendar months.

1. Find the comment **Block the approval if the device isn't eligible** and add the following code:

    ```csharp
    // Block the approval if the device isn't eligible
    if (!isEligible)
    {
        var eligibleFrom = issuedOn.Value.Date.AddMonths(24);
        throw new InvalidPluginExecutionException(
            $"This device was issued on {issuedOn.Value:yyyy-MM-dd}. It can be replaced from {eligibleFrom:yyyy-MM-dd}, or earlier if it's flagged as damaged beyond repair.");
    }
    ```

    Throwing `InvalidPluginExecutionException` inside a synchronous, pre-operation step stops the update before Dataverse commits it. The caller — whether it's the form or, as you'll see shortly, a direct Web API call — receives this message as the error.

1. Select **Build** > **Build Solution** (or press **Ctrl+Shift+B**) and confirm the project builds with no errors.

## Register the plug-in with the Plug-in Registration Tool

With the code compiled, you need to tell Dataverse to run it — and exactly when. You'll register the assembly, then a step on the `Update` message of `eqp_request`, then a pre-image the step reads from.

1. Open a terminal and launch the Plug-in Registration Tool:

    ```powershell
    pac tool prt
    ```

1. Select **+ Create new connection**, select option "Display list of available organizations", sign in using your credentials, and connect to the Dataverse environment you've been using in this exercise.

1. On the **Register** menu, select **New Assembly**.

1. In the **Register New Assembly** dialog, select the ellipsis (**…**) button and browse to `AB400.Lab01.dll` in your project's `bin\Debug\net48` folder. Confirm **Isolation Mode** is **Sandbox** and **Location** is **Database**, then select **Register Selected Plugins**.

1. Expand **(Assembly) AB400.Lab01**, right-click **(Plugin) AB400.Lab01.RequestApprovalPlugin**, and select **Register New Step**.

1. In the **Register New Step** dialog, set:

    | Setting | Value |
    |:--|:--|
    | Message | `Update` |
    | Primary Entity | `eqp_request` |
    | Filtering Attributes | `eqp_requeststatus` only |
    | Event Pipeline Stage of Execution | **PreOperation** |
    | Execution Mode | **Synchronous** |
    | Description | *Lab 01: enforce replacement eligibility on approval* |

    Select the **Filtering Attributes** selector and choose only `eqp_requeststatus` — don't include the primary key, and don't select every column. Select **Register New Step**.
   > **Note**: After you select **Register New Step**, the Plug-in Registration Tool might show an error like this:
    >
    > ```
    > Unhandled Exception: System.NullReferenceException: Object reference not set to an instance of an object.
    >    at Microsoft.Crm.Tools.Libraries.OrganizationHelper.GetColumnSet(String entityName, CrmOrganization org)
    >    at Microsoft.Crm.Tools.Libraries.OrganizationHelper.RefreshStep(CrmOrganization org, CrmPluginStep step)
    >    at Microsoft.Crm.Tools.PluginRegistration.StepRegistrationViewModel.RegisterStep(Object sender, EventArgs e)
    > ```
    >
    > The error comes from the tool refreshing its view after the save. It doesn't come from your plug-in code, and the step is usually already created. To continue:
    >
    > 1. Select **Cancel**
    > 1. Expand the assembly and the plug-in class, and check that the step is listed.

1. Right-click the step you just registered, and select **Register New Image**.

1. In the **Register New Image** dialog, set:

    | Setting | Value |
    |:--|:--|
    | Image Type | **Pre Image** |
    | Name | `PreImage` |
    | Entity Alias | `PreImage` |
    | Parameters | `eqp_requeststatus`, `eqp_currentdevice` only |

    The entity alias must be exactly `PreImage` — it's the string your code uses to read `context.PreEntityImages["PreImage"]`. Select only the two columns the plug-in reads; registering every column on an image is a common, avoidable performance cost. Select **Register New Image** (or **Save**) to finish.

   You should see the new step, with its pre-image, listed under **(Plugin) AB400.Lab01.RequestApprovalPlugin** in the Plug-in Registration Tool.

## Reset the lab data and confirm the plug-in blocks the bypass

Now repeat the exact bypass from earlier in the exercise, with the plug-in in place.

1. Back in the **Equipment Provisioning** app, on the **Active Requests** view's command bar, select **Reset lab data**. This restores `REQ-SEED-001` to **Submitted**, undoing the approval from the Web API bypass.

1. In your terminal, run the bypass script again for the same request:

    ```powershell
    .\Approve-RequestViaWebApi.ps1 -EnvironmentUrl "<your environment URL>" -RequestNumber "REQ-SEED-001"
    ```

   This time the script fails. You should see an error similar to:

    ```output
    The Web API rejected the request for REQ-SEED-001. ... This device was issued on 2024-02-01. It can be replaced from 2026-02-01, or earlier if it's flagged as damaged beyond repair. ...
    ```

   Refresh `REQ-SEED-001` in the app and confirm **Request status** is still **Submitted**. The exact call that succeeded at the start of the exercise is now blocked on the server — where a client-side check never could have stopped it.

1. Now run the same script for `REQ-SEED-002`, the request for a device that's flagged as damaged beyond repair:

    ```powershell
    .\Approve-RequestViaWebApi.ps1 -EnvironmentUrl "<your environment URL>" -RequestNumber "REQ-SEED-002"
    ```

   This time it succeeds. Refresh `REQ-SEED-002` in the app and confirm **Request status** is now **Approved**.

   This second check matters as much as the first: a plug-in that simply rejected every approval would also "pass" the first test. Approving `REQ-SEED-002` proves your plug-in applies the rule selectively, not as a blanket block.

## Catalog the eligibility rule as a business event

The same replacement-eligibility logic you just implemented is also available as a Dataverse custom API, `eqp_IsEligibleForReplacement`, supplied with the base solution. To make it discoverable by Power Automate and other subscribers, you catalog it as a Dataverse business event using the `Catalog` and `CatalogAssignment` tables.

1. In the maker portal, select **Solutions**, then select **+ New solution** (or select it if you already created it earlier in this exercise). Name it `AB400 Lab 01 catalog`, choose a publisher, and select **Create**.

1. Inside the new solution, select **New**, and then select **Catalog**. (If **Catalog** isn't listed directly, select **More** first.)

1. Create the root catalog for this solution:

    | Field | Value |
    |:--|:--|
    | Parent Catalog | *leave blank* |
    | Name | `AB400 Lab 01` |
    | Display Name | `AB400 Lab 01` |
    | Description | `Business events cataloged for AB-400 Lab 01.` |

    As you type in **Name**, **Unique Name** autofills using your publisher's prefix — you can leave the generated value as is. Save and close the form.

1. Select **New** > **Catalog** again to create the second-level catalog (the category):

    | Field | Value |
    |:--|:--|
    | Parent Catalog | `AB400 Lab 01` (the catalog you just created) |
    | Name | `Equipment eligibility` |
    | Display Name | `Equipment eligibility` |
    | Description | `Business events related to equipment replacement eligibility.` |

    Save and close the form.

1. Select Catalog Assignment tab and choose **New Catalog Assignment**. Enter `eqp_IsEligibleForReplacement` as **Name**, set **Catalog Assignment Object** to the custom API `eqp_IsEligibleForReplacement` (search for it by name), and set the catalog to the **Equipment eligibility** category you just created. Save and close the form.

1. Confirm the business event is now discoverable. Open [Power Automate](https://make.powerautomate.com){:target="_blank"} at `https://make.powerautomate.com`, and confirm the environment selector (top-right) shows the same environment you've been using.

1. Select **Create**, then **Automated cloud flow**. Give it any name — for example, `AB400 Lab 01 flow check - don't save` — search for **Dataverse**, select the trigger **When an action is performed**, and select **Create**.

1. On the trigger, set **Catalog** to `AB400 Lab 01`, **Category** to `Equipment eligibility`, and **Table name** to **(none)**.

   You should see `eqp_IsEligibleForReplacement` listed in **Action name** — the same rule you implemented as a plug-in is now cataloged and discoverable by any flow in the environment.

1. Close the flow **without saving** it. This exercise only needs to prove the event is discoverable, not build a flow that consumes it.

## Clean up

If you've finished exploring, remove what you registered and created in this exercise. Leaving it in place doesn't break any other exercise, but removing it keeps your environment tidy.

1. Open the Plug-in Registration Tool (`pac tool prt`) and connect to your environment.

1. Expand **(Assembly) AB400.Lab01** > **(Plugin) AB400.Lab01.RequestApprovalPlugin**, right-click the registered step, and select **Unregister**. Confirm the pre-image is removed along with it.

1. Right-click **(Assembly) AB400.Lab01** and select **Unregister** to remove the assembly.

1. In the maker portal, open the **AB400 Lab 01 catalog** solution and delete the components you created, in this order: the **Catalog Assignment**, the **Equipment eligibility** category, and then the **AB400 Lab 01** root catalog.

1. On the **Solutions** page, select **EquipmentProvisioningLab01**, and select **Delete** to uninstall it. This removes the **Request intake** form and its client-side check; it doesn't affect **Equipment Provisioning (base)**.

## Summary

In this exercise, you confirmed that a client-side check on a Dataverse form can be bypassed through the Web API, then closed that gap with a plug-in. You used a pre-image to detect an approval transition, the Organization service to look up related data, and the Plug-in Registration Tool to register a synchronous, pre-operation step with filtering attributes and an entity image. You confirmed the plug-in enforces the rule selectively — blocking an ineligible approval while still allowing a legitimate exception — and cataloged the same rule's custom API as a Dataverse business event so other automation can discover it.

You have successfully completed this exercise.
