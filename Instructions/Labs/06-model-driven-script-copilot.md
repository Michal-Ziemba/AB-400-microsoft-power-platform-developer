---
lab:
    title: 'Build a model-driven app script with GitHub Copilot'
    description: 'Use GitHub Copilot to draft a model-driven app form script that calls the Dataverse Web API, shows form notifications, and runs a confirmation dialog from a custom command.'
    level: 300
    duration: 30
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Build a model-driven app script with GitHub Copilot

In this exercise, you add client-side logic to a model-driven app form using GitHub Copilot, then review and correct what it generates. You'll write a script that runs when a coordinator opens a device replacement request: it calls a custom API over the Dataverse Web API to check whether the device is eligible for replacement, shows a warning if it isn't, and shows the employee's remaining equipment allowance. You'll also add a command to the form's command bar that opens a confirmation dialog before a request is sent to the fulfillment team.

The script never writes to Dataverse — everything it does is a read, which makes it safe to run against live data as often as you like while you're learning. GitHub Copilot drafts most of the code for you, but its output varies, so part of the exercise is reviewing what it produces against a working reference and correcting any mistakes.

This exercise should take approximately **30** minutes to complete.

## Prerequisites

To complete this exercise, you need:

- A developer environment with Microsoft Dataverse, with the **Equipment Provisioning (base)** solution installed.
- A Chromium-based browser with developer tools.
- [Visual Studio Code](https://code.visualstudio.com/){:target="_blank"} at `https://code.visualstudio.com/`, with GitHub Copilot enabled.
- A GitHub account with GitHub Copilot access (Copilot Free is sufficient for this exercise; see the setup instructions if your account can't use Copilot Free).

## Confirm the environment and reset the lab data

Every exercise in this course shares the same Dataverse tables and seed data, supplied as a solution called **Equipment Provisioning (base)**. This task confirms that solution is in place, then resets the data this exercise reads so you're starting from a known state.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`.

1. Confirm the environment selector, near the top of the page, shows the Dataverse environment you intend to use for this exercise.

1. Select **Solutions**, and confirm that **Equipment Provisioning (base)** is listed. If it isn't, follow the import steps in [Set up your environment](./00-setup.md){:target="_blank"} before continuing.

1. Select **Apps**, and then open the **Equipment Provisioning** app.

1. In the **Equipment provisioning** area, select **Requests**.

1. On the command bar, select **Reset lab data**. Wait for the progress indicator to finish, then review the summary dialog, which lists how many rows were created, restored, or were already correct.

1. Select **Refresh**.

You should see the **Active Requests** view populated with seeded rows, including `REQ-SEED-001` and `REQ-SEED-002`.

## Draft the form-load and command logic with GitHub Copilot

The starter file already contains the requirements for both functions you need, written as comments. You'll give those comments to GitHub Copilot one at a time and review what it produces.

1. Open Visual Studio Code.

1. Open the `06-model-driven-script-copilot` folder inside the repository you cloned during setup, or download [requestscript.js](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/06-model-driven-script-copilot/requestscript.js){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/06-model-driven-script-copilot/requestscript.js`.

1. Open **requestscript.js**. Review the two comment blocks in the file: one above the empty `onFormLoad` function, and one above the empty `confirmProvisioning` function. Each one describes exactly what that function must do.

1. Select the comment block and the empty `onFormLoad` function below it, open GitHub Copilot Chat, and send the following prompt:

    ```input
    Using the requirements in the comment above onFormLoad, implement that function.
    ```

1. Review what Copilot generates. If it's missing the allowance notification, or implemented the eligibility check some other way, ask it to adjust with a follow-up prompt, for example:

    ```input
    The function is missing the INFO notification for the employee's remaining allowance. Add it, reading eqp_remaining from the eqp_allowance table for the request's employee.
    ```

1. Select the comment block and the empty `confirmProvisioning` function, and send Copilot this prompt:

    ```input
    Using the requirements in the comment above confirmProvisioning, implement that function.
    ```

1. Save the file.

You should now see generated code in place of both empty functions. It won't always be correct on the first try — that's expected, and you'll check it in the next task.

## Review the generated code and correct it

GitHub Copilot's first draft is usually close, but not always exact. Check it against the list below, then compare it with the reference implementations that follow and correct anything that's different. Test the result on the form in the final task.

| Check | What to look for |
|---|---|
| Custom API name and parameter | Use `Xrm.WebApi.online.execute` with operation name `eqp_IsEligibleForReplacement` and one `DeviceId` parameter. The reference request uses `{ guid: deviceId }`, verified in the tested client |
| Authentication and errors | Use the signed-in model-driven app's Web API context. Check the HTTP status and Boolean `IsEligible` response, and report failures rather than treating them as an eligibility result |
| Table and column names | `eqp_device`, `eqp_issuedon`, `eqp_employee`, `eqp_allowance`, `eqp_remaining` — Copilot sometimes guesses a plausible but wrong logical name |
| Allowance filter | The filter needs the employee's ID on the lookup's value property (`_eqp_employee_value`), not the lookup's display name |
| Eligible-from date math | The warning date is the issue date plus 24 **calendar** months, not a fixed number of days |
| Confirm command signature | `confirmProvisioning` must accept a single parameter — the form context, passed in by the command's **Primary Control** parameter |
| No writes | Neither function should call `Xrm.WebApi.updateRecord` or save the form |

1. Compare your `onFormLoad` function with the reference implementation below, and correct any differences:

    ```javascript
    function onFormLoad(executionContext) {
        var formContext = executionContext.getFormContext();
        formContext.ui.clearFormNotification(ELIGIBILITY_NOTIFICATION_ID);
        formContext.ui.clearFormNotification(ALLOWANCE_NOTIFICATION_ID);

        var device = formContext.getAttribute("eqp_currentdevice").getValue();
        if (device && device.length > 0) {
            var deviceId = device[0].id.replace(/[{}]/g, "");
            checkEligibility(deviceId).then(function (result) {
                console.log("eqp_IsEligibleForReplacement returned IsEligible=" + result.IsEligible);
                if (result.IsEligible) {
                    formContext.ui.clearFormNotification(ELIGIBILITY_NOTIFICATION_ID);
                    return;
                }
                return Xrm.WebApi.retrieveRecord("eqp_device", deviceId, "?$select=eqp_issuedon").then(function (deviceRow) {
                    var issuedOn = deviceRow.eqp_issuedon.substring(0, 10);
                    var eligibleFrom = addMonthsUTC(issuedOn, 24);
                    var message = "This device was issued on " + issuedOn + ". It can be replaced from " +
                        eligibleFrom + ", or earlier if it's flagged as damaged beyond repair.";
                    formContext.ui.setFormNotification(message, "WARNING", ELIGIBILITY_NOTIFICATION_ID);
                });
            }).catch(function (error) {
                console.error(error.message);
                formContext.ui.setFormNotification(
                    "The eligibility check couldn't be completed. Check the browser console for details.",
                    "ERROR",
                    ELIGIBILITY_NOTIFICATION_ID
                );
            });
        }

        var employee = formContext.getAttribute("eqp_employee").getValue();
        if (employee && employee.length > 0) {
            var employeeId = employee[0].id.replace(/[{}]/g, "");
            var employeeName = employee[0].name;
            var options = "?$select=eqp_remaining&$filter=_eqp_employee_value eq " + employeeId;

            Xrm.WebApi.retrieveMultipleRecords("eqp_allowance", options).then(function (result) {
                if (result.entities.length === 0) {
                    formContext.ui.clearFormNotification(ALLOWANCE_NOTIFICATION_ID);
                    return;
                }
                var remaining = result.entities[0].eqp_remaining;
                console.log(employeeName + " has " + remaining + " remaining in the current allowance period.");
                var message = employeeName + " has " + remaining +
                    " replacement device(s) remaining in the current allowance period.";
                formContext.ui.setFormNotification(message, "INFO", ALLOWANCE_NOTIFICATION_ID);
            }).catch(function (error) {
                console.log(error.message);
            });
        }
    }
    ```

    This version calls the eligibility custom API with the device's ID and only shows the WARNING notification when the response's `IsEligible` value is `false`. The allowance read doesn't depend on the eligibility result, so it runs even if the eligibility call fails.

1. Confirm the file has a small helper near the top that calls the custom API, similar to this. If Copilot generated something different, replace it with this version so the operation name and parameter match F2 exactly:

    ```javascript
    function IsEligibleForReplacementRequest(deviceId) {
        this.DeviceId = { guid: deviceId };
    }
    IsEligibleForReplacementRequest.prototype.getMetadata = function () {
        return {
            boundParameter: null,
            parameterTypes: {
                "DeviceId": { "typeName": "Edm.Guid", "structuralProperty": 1 }
            },
            operationType: 0,
            operationName: "eqp_IsEligibleForReplacement"
        };
    };

    function checkEligibility(deviceId) {
        return Xrm.WebApi.online.execute(new IsEligibleForReplacementRequest(deviceId)).then(function (response) {
            if (response.ok) {
                return response.json();
            }
            return response.text().then(function (body) {
                throw new Error("Eligibility check failed (" + response.status + "): " + body);
            });
        }).then(function (result) {
            if (!result || typeof result.IsEligible !== "boolean") {
                throw new Error("Eligibility check returned an invalid IsEligible value.");
            }
            return result;
        });
    }
    ```

    The request uses the signed-in app's Web API context, without an API key or a separately acquired token. The GUID wrapper produced the correct string `DeviceId` in the tested client's outgoing JSON; it isn't a general requirement for every GUID parameter or SDK. An unsuccessful HTTP response or an invalid `IsEligible` value reaches the handler's error notification instead of becoming an eligibility decision.

1. Confirm the file has an `addMonthsUTC` (or similarly named) helper that adds calendar months, not days, and clamps to the last day of the target month. Compare it with this version:

    ```javascript
    function addMonthsUTC(isoDate, months) {
        var parts = isoDate.substring(0, 10).split("-");
        var year = Number(parts[0]);
        var month = Number(parts[1]) - 1;
        var day = Number(parts[2]);

        var targetIndex = month + months;
        var targetYear = year + Math.floor(targetIndex / 12);
        var targetMonth = ((targetIndex % 12) + 12) % 12;
        var lastDayOfTargetMonth = new Date(Date.UTC(targetYear, targetMonth + 1, 0)).getUTCDate();
        var targetDay = Math.min(day, lastDayOfTargetMonth);

        return new Date(Date.UTC(targetYear, targetMonth, targetDay)).toISOString().substring(0, 10);
    }
    ```

1. Compare your `confirmProvisioning` function with the reference implementation below:

    ```javascript
    function confirmProvisioning(primaryControl) {
        var formContext = primaryControl;
        var requestNumber = formContext.getAttribute("eqp_name").getValue();
        var confirmStrings = {
            title: "Send to fulfillment",
            text: "Send " + requestNumber + " to the fulfillment team?"
        };
        var confirmOptions = { height: 200, width: 450 };

        Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(function (result) {
            if (result.confirmed) {
                console.log(requestNumber + ": coordinator confirmed sending to fulfillment.");
            } else {
                console.log(requestNumber + ": coordinator canceled.");
            }
        });
    }
    ```

1. Save the file.

Your file should now have two complete functions and no remaining `TODO` comments. You'll wire them up to the form and command next, so you can test them together.

## Add the script as a web resource and register the form event handler

Now you'll create your own solution to hold this exercise's changes, upload the corrected script as a web resource, and register `onFormLoad` on the Request form's **On Load** event.

1. In the maker portal, select **Solutions**, then select **New solution**.

1. For **Display name**, enter `Equipment Provisioning Lab 06`. Note the **Name** value that's generated.

1. For **Publisher**, select the existing **Equipment Provisioning Labs** publisher (don't create a new one).

1. Select **Save**.

1. In your new, now-open solution, select **New**, point to **More**, and then select **Web resource**.

1. Select **Choose file** and select your edited `requestscript.js` file.

1. For **Display name**, enter `Equipment provisioning lab 06 script`. For **Name**, enter `requestscript` — the portal prefixes it with `eqp_`. Confirm **Type** is set to **JavaScript (JS)**.

1. Select **Save**.

1. In the same solution, select **Add existing**, and then select **App**.

1. Select the **Equipment Provisioning** app, and then select **Add**.

1. Select the **Equipment Provisioning** app, and then select **Edit** to open it in the app designer.

1. Under **Pages**, expand the **Request** table, and next to the **Information** form, select **...**, and then select **Edit**.

1. In the form designer, select **Form libraries**, then select **Add library**.

1. Select the web resource you uploaded, and then select **Add**.

1. Select the **Events** tab. Under **On Load**, select **+ Event Handler**.

1. For **Library**, select your web resource. For **Function**, enter `EquipmentProvisioning.Lab06.onFormLoad`.

1. Select **Pass execution context as first parameter**, and then select **Done**.

1. Select **Save**, and then select **Publish**.

You should see the form designer confirm the publish completed without errors. You'll trigger the handler itself once the command is in place too, in a later task.

## Create a command that runs the confirm-dialog function

With the web resource in place, add a command to the Request form's command bar that calls `confirmProvisioning`.

1. Still in the app designer for **Equipment Provisioning**, select **Requests view** under **Pages** so its **...** menu appears. Select **...** > **Edit command bar**.

1. Choose the **Main form** location, and then select **Edit**.

1. Select **New**, and then select **Command**.

1. For **Label**, enter `Send to fulfillment`.

1. For **Action**, select **Run JavaScript**.

1. Select **Add library** if your web resource isn't already listed, choose it, and select **Add**.

1. For **Function name**, enter `EquipmentProvisioning.Lab06.confirmProvisioning`.

1. Add a parameter for the function, and choose **Primary Control** as its type. This is what passes the form context into `confirmProvisioning` as its single argument.

1. Select **Save and Publish**.

1. Back in the app designer, select **Publish all customizations**.

You should see the new **Send to fulfillment** button listed as a command on the Request main form's command bar.

## Debug with browser developer tools and prove the script

Now trigger everything you've built, and use the browser's own developer tools — not a separate network-capture tool — to see what the script is doing.

1. Play the **Equipment Provisioning** app, go to **Requests**, and open `REQ-SEED-001`.

1. You should see a WARNING notification naming the device and the date it becomes eligible for replacement, and an INFO notification reporting the employee's remaining allowance.

1. Open the browser's developer tools (for example, by pressing **F12**), and select the **Console** tab. Reload the form. You should see the console messages your script logged, including the `IsEligible` result and the remaining allowance.

1. Select the **Sources** tab, and find your web resource (use **Ctrl+P** to search for it by name). Confirm the loaded script contains your `IsEligibleForReplacementRequest` implementation, and set a breakpoint on the line that calls `Xrm.WebApi.online.execute`. If the loaded code differs from your saved file, verify the uploaded web resource, form library, and published handler before testing the logic.

1. Reload the form. Execution should pause at your breakpoint. Inspect the `deviceId` variable, and then resume execution.

1. Select the **Network** tab, and reload the form again. Find the request to `eqp_IsEligibleForReplacement`, and select it. Review its request payload (the `DeviceId` you passed in) and its response (the `IsEligible` value it returned).

1. On the form's command bar, select **Send to fulfillment**. Confirm the dialog opens with the request number in its message, then select the confirm button. Check the **Console** tab again to see the outcome logged.

1. Optionally, open `REQ-SEED-002` instead. Its device is inside the same replacement period as `LAP-1001`, but it's flagged as damaged beyond repair, so the eligibility exception applies — confirm the WARNING notification doesn't appear for this request, while the INFO allowance notification still does.

You've now seen the corrected script run end to end: a warning and an allowance notification driven by live Dataverse Web API calls, visible in the Network tab, and a confirmation dialog from your new command — all without the script writing anything back.

## Clean up

If you've finished exploring, remove the customizations you added in this exercise.

> **Important**: This exercise creates an unmanaged solution. Deleting that solution removes only its container, not the command, form event handler, or web resource. Remove those lab-owned customizations first. Don't delete the shared app, the **Information** form, or any unrelated commands or event handlers.

1. In the maker portal, open the solution you created, **Equipment Provisioning Lab 06**, and edit the **Equipment Provisioning** app. Open the **Request** table's command bar editor and choose **Main form**.

1. Select the **Send to fulfillment** command you created in this exercise, delete it, and select **Save and Publish**. Return to the app designer and publish the app.

1. Open the Request **Information** form in the form designer. Under **Events** > **On Load**, remove only the handler whose function is `EquipmentProvisioning.Lab06.onFormLoad`.

1. Under **Form libraries**, remove **Equipment provisioning lab 06 script** (`eqp_requestscript`) from this form. Save and publish the form.

1. Return to the lab solution's component list and select the **Equipment provisioning lab 06 script** web resource. Select **Delete from this environment**, not **Remove from this solution**, and confirm.

    If dependencies prevent deletion, first check that the lab command, handler, and library references are removed and published. Don't delete unrelated components. A deleted modern command can leave an **App Action** dependency. Only for that case:

    1. Open the dependency details and identify the remaining App Action's ID. Keep those details available for comparison. If you can't identify the exact dependency, stop rather than guessing.
    1. Download [remove-lab-command.js](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/06-model-driven-script-copilot/remove-lab-command.js){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/06-model-driven-script-copilot/remove-lab-command.js`, and open it in Visual Studio Code. This optional cleanup helper uses the signed-in app's Web API to inspect the remaining action and delete it only after ownership checks and confirmation.
    1. Review the helper. It requires exactly one unmanaged action linked to the **Equipment Provisioning** app and `eqp_requestscript`, with the lab's function name and a `sendtofulfillment` unique name. It uses an ETag to reject a concurrent change. It never deletes the app, form, or web resource.
    1. In the **Equipment Provisioning** app, open browser developer tools and select **Console**. Use the context where `Xrm` is available. After reviewing the helper, paste its contents into the console and run it. Don't disable browser security controls to allow pasting.
    1. Compare the displayed action ID with the dependency details. Confirm deletion only if they match and you created that command in this exercise. Otherwise cancel. If any ownership check fails, leave the component intact and investigate; don't weaken the checks.
    1. Confirm the console reports that deletion and absence verification succeeded. Return to the maker portal and retry deletion of the lab web resource. This helper uses the [documented App Action DELETE operation](https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/appaction){:target="_blank"} at `https://learn.microsoft.com/power-apps/developer/data-platform/reference/entities/appaction`.

1. In the lab solution, remove any remaining shared app or table entries using **Remove from this solution**, never **Delete from this environment**. Return to **Solutions**, select the lab solution using the **Name** value you noted when you created it, and delete only its container.

1. Reopen a request in **Equipment Provisioning**. Confirm the lab's **Send to fulfillment** command and its eligibility/allowance notifications are no longer present, and the form loads without a missing-library error.

Don't delete the **Equipment Provisioning (base)** solution or any seed data rows — other exercises depend on them.
