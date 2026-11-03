---
lab:
    title: 'Build a resilient equipment-approval cloud flow'
    description: 'Harden a naive Power Automate cloud flow with Dataverse trigger filtering, a Try/Catch scope with a bounded retry policy, a reusable child flow, and a Microsoft Entra service-principal connection.'
    level: 300
    duration: 35
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Build a resilient equipment-approval cloud flow

A cloud flow that passes its first test can still be unsafe to run in production. In this exercise, you start from a supplied Power Automate flow that approves equipment replacement requests, and you harden it one problem at a time: you stop it firing on every unrelated change, you make its one external call recover from failure instead of failing silently, you split its fulfilment logic into a reusable child flow, and you move its Dataverse connection off your own sign-in and onto a Microsoft Entra service principal.

You'll work with the **Microsoft Dataverse** connector in Power Automate, a Dataverse trigger filter, a Try/Catch scope with a retry policy, a child flow, and a service-principal connection.

This exercise should take approximately **35** minutes to complete.

## Before you start

This exercise starts from a supplied solution that contains a working, but naive, approval flow. You'll also need a Microsoft Entra service principal to authenticate the flow's Dataverse connection later in the exercise.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, and confirm the environment selector shows the developer environment you're using for these exercises.

1. Select **Solutions**, and confirm **Equipment Provisioning (base)** is listed. If it isn't, follow the import steps in [00-setup](./00-setup.md) before continuing.

1. Download [EquipmentProvisioningLab08.zip](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/08-resilient-cloud-flow/EquipmentProvisioningLab08.zip){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/08-resilient-cloud-flow/EquipmentProvisioningLab08.zip`.

1. On the **Solutions** page, select **Import solution**, select **Browse**, choose the file you downloaded, and select **Next**.

1. On the connections step, select **+ New connection** for each of the two connection references it lists — **Lab 08 Dataverse (approval flow)** and **Lab 08 Dataverse (child flow)** — choose **Microsoft Dataverse**, and sign in with your own credentials. Use your own connection for both rows, even though one of them moves to a service principal later in this exercise.

1. Select **Import**, and wait for the import to finish.

1. Open the **Equipment Provisioning lab 08** solution once the import finishes, and select the component list filter for **Cloud flows**. You should see **Lab 08 approval flow** and **Lab 08 fulfilment child flow**, both showing **Off** — the supplied solution ships with both flows turned off.

1. Leave both flows **Off** while you prepare the starting data. If you're repeating this exercise and either flow is already on, turn it off and wait for any in-progress runs to finish before continuing.

1. Prepare a Microsoft Entra service principal for later in this exercise:

    1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com){:target="_blank"} at `https://entra.microsoft.com`.
    1. Confirm the selected tenant is the one associated with your Dataverse environment. Browse to **Identity** > **Applications** > **App registrations**. Select an existing registration named `ab400-lab08-flow` if you created it for this environment; otherwise select **New registration**.
    1. If you're creating a registration, enter the name `ab400-lab08-flow`, leave **Redirect URI** blank, and select **Register**.
    1. On the app's **Overview** page, note the **Application (client) ID** and **Directory (tenant) ID**. You'll need both later.
    1. Select **Certificates & secrets** > **Client secrets** > **New client secret**. Enter a description, choose the shortest expiry offered, and select **Add**.
    1. Copy the secret's **Value** immediately and store it somewhere temporary — you can't retrieve it again after you leave this page.

1. Create or select the application user for this service principal:

    1. Sign in to the [Power Platform admin center](https://admin.powerplatform.microsoft.com){:target="_blank"} at `https://admin.powerplatform.microsoft.com`.
    1. Select **Environments**, select your developer environment, and select **Settings**.
    1. Select **Users + permissions** > **Application users**. Compare the **App ID** column with your registration's **Application (client) ID**, rather than relying on its display name. If a matching user exists, confirm it's active and has **Equipment Integration** and **Basic User**. Assign any missing role, then skip the remaining creation steps.
    1. If no matching user exists, select **+ New app user**, select **+ Add an app**, and find your registration. Verify its application ID, select it, and select **Add**.
    1. Select a **Business unit**, then select the edit icon next to security roles and assign **Equipment Integration** and **Basic User**. Select **Save**, confirm the role assignment, and select **Create**.

1. Switch back to the Power Apps maker portal, open the **Equipment Provisioning** app, open the **Requests** view, and select **Reset lab data** on the command bar. Wait for it to finish, then select **Refresh**.

1. In the **Equipment Provisioning lab 08** solution, turn on **Lab 08 fulfilment child flow**, then **Lab 08 approval flow**. Confirm both show **On**.

You should see a confirmation dialog summarizing what was reset, the application user listed under **Application users** with **Equipment Integration** and **Basic User**, and both Lab 08 flows showing **On**.

## See why the naive flow isn't production-ready

The supplied **Lab 08 approval flow** triggers on every change to a request, has no error handling around its one external call, and always runs under whichever Dataverse connection its trigger uses. Before you fix anything, see these three problems for yourself.

1. In the **Equipment Provisioning** app, open request `REQ-SEED-001`, make an unrelated edit — for example, add a sentence to **Justification** — and save.

1. In the Power Apps maker portal, open the **Equipment Provisioning lab 08** solution, select **Lab 08 approval flow**, and select **Run history**. You should see a new run, even though you didn't touch the request's status. The flow has no trigger filter, so it reacts to every change.

1. Force the flow's one external call to fail: open **Solutions** > **Equipment Provisioning (base)**, filter components to **Environment Variables**, select **Availability mode**, set its **Current value** to `Error`, and save and publish.

1. In the **Equipment Provisioning** app, open `REQ-SEED-003` and change **Request status** to **Approved**. Save.

1. Return to **Run history** for **Lab 08 approval flow**. You should see a new, **Failed** run. Open it and expand **Check_availability** — it failed with an HTTP 503 error, because `eqp_AvailabilityMode` is now set to `Error`.

1. Open `REQ-SEED-003` again. You should see **Request status** is still **Approved**, but **Availability** is still **Not checked** and **Processing note** is empty. Nothing records that anything went wrong — the failure is silent from the request's point of view, even though Power Automate logged it.

1. In the failed run's details, expand the trigger. The connection it used is your own sign-in. Whoever happens to approve a request, the flow's Dataverse actions always run as whoever owns that connection — not as a shared, auditable identity. You'll fix this in a later task.

1. Before you continue, set `eqp_AvailabilityMode`'s **Current value** back to `Available` (same steps as above), and save and publish, so the next task's tests don't fail for the wrong reason.

You've now seen the naive flow's three problems: it fires on irrelevant changes, it fails silently on a real approval, and it runs as a personal identity. The rest of this exercise fixes each one.

## Filter the trigger to the approval transition

The Dataverse trigger has its own filtering, separate from a generic condition action. Configure it so the flow only reacts to a request becoming Approved.

1. Open **Lab 08 approval flow** for editing, and select the trigger, **When a row is added, modified or deleted**.

1. In the trigger's **Select columns** box, enter `eqp_requeststatus`.

    > **Note**: **Select columns** restricts the trigger to changes that include this column. Lookup columns aren't supported here — only scalar columns such as choices, text, numbers, and dates.

1. In the trigger's **Filter rows** box, enter:

    ```text
    eqp_requeststatus eq 100000001
    ```

    This OData-style expression further restricts the trigger to changes where the request's status is becoming Approved (choice value `100000001`).

1. Save the flow.

1. In the **Equipment Provisioning** app, open `REQ-SEED-001` again, add another sentence to **Justification**, and save.

1. Check **Run history** for **Lab 08 approval flow**. You should see **no new run** — the edit didn't touch `eqp_requeststatus`, so the trigger's **Select columns** filter now excludes it entirely.

1. Open `REQ-SEED-002` and change **Request status** to **Approved**. Save.

1. Check **Run history** again. You should see exactly one new run, and it should **succeed**: `REQ-SEED-002` ends up with **Availability** set to **Available** and **Request status** set to **In fulfillment**, with a **Processing note** that starts "Fulfilment started on".

> **Note**: The in-flow condition, `Is_newly_approved`, still checks both status and availability. The trigger's **Filter rows** now guarantees status is Approved before the flow even starts, so the condition's status check is technically redundant. This exercise leaves it in place for defensive clarity and to save time — try removing it yourself afterward to see the simplification.

## Handle failures with a Try/Catch scope and a bounded retry policy

Right now, a failed availability check stops the flow dead with nothing recorded. Wrap the call in a Try/Catch pattern: a short, bounded retry policy gives the external call a fair second chance, and a Catch scope records what happened if it still fails.

> **Tip**: If you'd rather copy and paste than retype the values in this task, download [expression-snippets.txt](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/08-resilient-cloud-flow/expression-snippets.txt){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/08-resilient-cloud-flow/expression-snippets.txt`.

1. Turn **Lab 08 approval flow** off and wait for any active runs to finish before editing. Inside the **Is_newly_approved** condition's **Yes** branch, add a new **Scope** action positioned immediately before **Record_availability**. Rename it `Try`.

1. Move the availability action inside **Try**. If dragging doesn't work, use the action's **...** menu > **Copy to my clipboard**, then **Add an action** inside **Try** > **My clipboard** and select the copied action. The designer may name it `Check_availability_2`. Note the actual name; don't delete the original action yet.

1. In **Record_availability**, update **Availability** to read the action inside **Try**. For a copied action named `Check_availability_2`, use this expression:

    ```text
    if(equals(outputs('Check_availability_2')?['body/IsAvailable'], true), 100000001, 100000002)
    ```

    This expression maps the custom API's Boolean result to the Available or Unavailable choice value. Replace `Check_availability_2` with the actual action name if it differs. Select the existing expression token to edit it. If necessary, remove the token, choose **Enter custom value**, and insert a fresh **Expression**. Confirm in **Code view** that it references the action inside **Try**, not the original action, and isn't escaped as `@@if`.

1. In **Record_availability**'s **Settings** > **Run after**, select only **Try** **is successful**. After you repair the expression and run-after reference, delete the original availability action if you copied it.

1. Select the availability action inside **Try**, open **Settings**, and under **Retry Policy** set:

    - **Type**: Fixed Interval
    - **Count**: `2`
    - **Interval**: `PT5S`

    This replaces the supplied **None** policy with two retries, five seconds apart, so a transient failure gets a bounded second chance without the forced-failure test in the final task taking long to run.

1. Add a second **Scope** action on a **parallel branch after Try**, alongside the successful **Record_availability** path. In the new designer, right-click the plus sign between **Try** and **Record_availability**, and select **Add a parallel branch** > **Scope**. Rename it `Catch`. Keep **Record_availability** and subsequent fulfilment on the success branch.

1. Select **Catch**, open **Settings** > **Run after** (or **...** > **Configure run after** in the classic designer), and select **Try** **has failed** and **has timed out**. Clear **is successful** and **is skipped**. The selected failure and timeout states are alternatives: either state starts Catch.

1. Inside **Catch**, add an **Update a row** action for the **Microsoft Dataverse** connector:

    - **Table name**: Requests
    - **Row ID**: the dynamic content item for the triggering row's unique identifier (it may be labeled **Request** or similar, depending on your designer)
    - **Request status**: Needs attention
    - **Processing note**: select **Expression**, and build:

        ```text
        concat('The availability check failed and exhausted its retries on ', utcNow(), '. Route this request to ', <Fulfilment contact>, ' for manual follow-up.')
        ```

        Type the literal text segments and `utcNow()` yourself, then switch to **Dynamic content** and select **Fulfillment contact (eqp_FulfillmentContact)** at the point marked `<Fulfilment contact>`. Don't type the placeholder. Verify the saved expression references that environment-variable parameter.

        > **Tip**: GitHub Copilot can help you draft this `concat()` expression if you describe what it should say.

1. Inside **Catch**, immediately after its **Update a row** action, add **Control** > **Terminate** and set **Status** to **Succeeded**. In **Settings** > **Run after**, select only **is successful** for that **Update a row** action. Don't put an unconditional success terminator after the condition: if the recovery update fails, the terminator must be skipped so the failure stays visible.

1. Save and reopen the flow. Verify in **Code view** that the action inside **Try** has a fixed retry policy with count `2` and interval `PT5S`, **Record_availability** references that action and runs after **Try** succeeds, and **Catch** runs only after **Try** fails or times out. Confirm **Terminate** is inside **Catch** and runs only after the recovery update succeeds. Check **Flow checker** for errors. Keep the parent off until you're ready to test.

You should see the availability action nested inside **Try**, a parallel **Catch** scope configured to run only when **Try** fails or times out, and **Catch**'s **Update a row** followed by its conditional **Terminate**. You test this path in the final task.

> **Note**: The Dataverse connector's **Perform an unbound action** step is the fastest way to call `eqp_CheckAvailability` from a flow, and it's what this exercise uses throughout. The same custom API is also callable as a plain Web API request, using the **HTTP with Microsoft Entra ID** action — not the generic **HTTP** action, which has no built-in Microsoft Entra ID authentication. The equivalent raw call looks like this:
>
> ```http
> POST https://<your-environment>.crm.dynamics.com/api/data/v9.2/eqp_CheckAvailability
> Content-Type: application/json
>
> {
>   "RequestId": "<request-guid>"
> }
> ```
>
> This exercise stays on the Dataverse connector's action, because it's faster to configure and because the service principal connection you set up in the next task already authenticates this exact call.

## Extract the fulfilment steps into a child flow

Instead of running the fulfilment steps inline, delegate them to the supplied **Lab 08 fulfilment child flow**. This keeps the approval flow focused on approval, and makes the fulfilment logic reusable.

1. Still editing **Lab 08 approval flow** from the **Equipment Provisioning lab 08** solution, add the child-flow action after **Record_availability** while leaving the existing **Start_fulfilment** action in place temporarily.

1. On the **Built-in** tab, select the **Flows** connector, and select **Run a Child Flow**. If the child-flow list is empty, confirm you opened the parent from the solution rather than **My flows**, and turn on the supplied child flow.

1. Select **Lab 08 fulfilment child flow** as the child flow to run.

1. For its **Request ID** input, select the dynamic content item for the triggering row's unique identifier.

1. Confirm **Run a Child Flow** runs after **Record_availability** succeeds. Delete the old inline **Start_fulfilment** action, save, reopen, and check that the success branch has only the child-flow action after **Record_availability**.

1. Turn on **Lab 08 fulfilment child flow**, then **Lab 08 approval flow**. Prepare the test request: in the **Equipment Provisioning** app, open `REQ-SEED-002`, now showing **In fulfillment** from the previous task. Set **Request status** to **Submitted**, set **Availability** to **Not checked**, clear **Processing note**, and save.

    The previous test set availability to **Available**. The `Is_newly_approved` condition requires **Not checked** as well as **Approved**, so changing only the status wouldn't run the child flow.

1. Set **Request status** to **Approved** and save.

1. Check **Run history**. Confirm **Record_availability** and **Run a Child Flow** succeeded, while **Catch** and its **Terminate** were skipped. The overall run should be **Succeeded**, and `REQ-SEED-002`'s **Processing note** should read "Fulfilment started on … **by the child flow**" — proof that the child flow ran, not the parent's old inline action.

## Authenticate the Dataverse connection with a service principal

The approval flow still authenticates as you. Switch its Dataverse connection to the service principal you prepared earlier, so the flow's identity no longer depends on whoever happens to be signed in.

1. Turn **Lab 08 approval flow** off and wait for any active runs to finish. In the **Equipment Provisioning lab 08** solution, select **Connection references**, open **Lab 08 Dataverse (approval flow)**, and note the personal connection currently selected. You restore it during cleanup.

1. Edit **Lab 08 approval flow** from the solution. Select the Dataverse availability action inside **Try**, such as **Check_availability_2**. Under **Parameters**, select **Change connection reference** > **Add new**.

1. In **Create connection**, enter `Lab 08 service principal` for **Connection name**. Set **Authentication Type** to **Service Principal**, not **Oauth**. In a designer that shows **Connect with service principal** instead, select that option.

1. Enter:

    - **Client ID**: the **Application (client) ID** you noted for `ab400-lab08-flow`
    - **Client Secret**: the secret's **Value** you copied earlier, not its secret ID
    - **Tenant**: the **Directory (tenant) ID** you noted

1. Select **Create new** (or **Create**, depending on the designer).

    > **Tip**: A newly created secret wasn't accepted immediately in testing but worked later without being changed. If you see `AADSTS7000215` or **Invalid client secret**, first verify the client ID, tenant ID, and secret **Value**. For a newly created, verified secret, wait 30 seconds before retrying with the same value, then wait another 60 seconds before one final retry if necessary. If it still fails, stop and investigate rather than repeatedly creating replacement secrets. Don't proceed until the connection reports **Connected**.

1. Check the action's connection reference. If the designer created and selected an automatically named reference, change the action back to **Lab 08 Dataverse (approval flow)** and save. Don't leave just one parent action on a separate reference.

1. In the solution's **Connection references** list, open the original **Lab 08 Dataverse (approval flow)** reference. Select **Lab 08 service principal** under **Connection**, select **Save**, and confirm the impact dialog. Reopen the reference to verify the selected connection.

1. Confirm the parent's Dataverse trigger and actions use **Lab 08 Dataverse (approval flow)**, while the child's Dataverse actions retain **Lab 08 Dataverse (child flow)**. If an extra reference was created, use **Show dependencies** to verify it's unused before deleting only that extra reference.

> **Note**: Only the parent flow's connection switches to the service principal. **Lab 08 fulfilment child flow** keeps its own Dataverse connection on your sign-in — child flows in a solution must use embedded connections, and splitting a second authentication mechanism into this exercise wouldn't teach anything new.

1. Open **Lab 08 approval flow**'s details page and confirm there's no **Invalid Connection** warning. With the parent still off, prepare `REQ-SEED-002` by setting **Request status** to **Submitted**, **Availability** to **Not checked**, and clearing **Processing note**. Save the request.

1. Confirm the child flow is on, then turn the parent on. Open `REQ-SEED-002`, change **Request status** to **Approved**, and save. Confirm a new parent run succeeds and the request becomes **In fulfillment / Available** with the child-flow note.

The new run verifies that the parent works with the selected service-principal connection; an **On** indicator alone doesn't prove the trigger or actions ran. You test failure recovery under the same connection in the final task.

## Force a failure and confirm the flow routes it for follow-up

Now put the whole chain under stress: force the availability check to fail on every attempt, and confirm the flow recovers gracefully instead of failing silently.

1. Turn off **Lab 08 approval flow** and wait for any in-progress runs to finish.

1. In the **Equipment Provisioning** app, open the **Requests** view and select **Reset lab data** again. Wait for it to finish, then select **Refresh**. This restores `REQ-SEED-003` to **Submitted**.

    Keep the parent flow off during the reset so it doesn't process the baseline approval on `REQ-SEED-004`.

1. In **Solutions** > **Equipment Provisioning (base)**, filter components to **Environment Variables**, select **Availability mode**, set its **Current value** to exactly `Error`, and save and publish. Reopen the variable and confirm the stored current value is `Error` in the same environment used by the availability action.

1. Confirm `REQ-SEED-003` is **Submitted** with **Availability** set to **Not checked**, then turn on the child flow and parent flow in that order.

1. Open `REQ-SEED-003`, change **Request status** to **Approved**, and save.

    Confirm a new parent run appears and corresponds to this request. If no run appears, verify the saved status, target environment, trigger filters, flow state, and connection reference before continuing. No run means the failure-recovery path hasn't been tested; don't treat it as a Catch failure or repeatedly approve an unchanged row.

1. Wait for the two retries to finish, then open the new run in **Run history** for **Lab 08 approval flow**. Confirm the availability action inside **Try** failed with HTTP **503** after the initial attempt and two retries. If it succeeded instead, stop and check the stored mode value and the action's target environment before repeating the test.

1. Confirm **Catch**'s recovery update and **Terminate** succeeded, the overall run is **Succeeded**, and **Record_availability** and child fulfilment were skipped. The conditional **Terminate** ends the run only after recovery succeeds; **Try** still shows **Failed**, and **Catch** and its enclosing condition may show **Terminated** or **Cancelled**. If the recovery update fails or **Terminate** doesn't execute, don't report the flow as recovered.

1. Check for unexpected additional parent runs after the update. **Flow checker** may warn about a circular loop because the flow writes to the triggering Requests table. With **Select columns** set to `eqp_requeststatus` and **Filter rows** set to `eqp_requeststatus eq 100000001`, availability-only updates don't match the selected column, and **Needs attention** and **In fulfillment** don't match the Approved filter. Verify run history as well as the configuration.

1. Open `REQ-SEED-003` again. You should see:

    - **Request status**: Needs attention
    - **Processing note**: a message naming the failure and routing it to the fulfilment contact's address, for example "The availability check failed and exhausted its retries on 2026-09-30T18:42:11Z. Route this request to equipment-desk@example.com for manual follow-up."

This is the exercise's payoff: the same failure that disappeared without a trace in the second task now exhausts a bounded retry, gets caught, and leaves a record naming both the problem and who should look at it. Only a successful recovery update reaches **Terminate** and marks the overall run **Succeeded**.

## Summary

In this exercise, you started from a supplied naive approval flow and added filtering and error handling. You configured the Dataverse trigger's **Select columns** and **Filter rows**, added a Try/Catch scope with a bounded retry policy, recorded a failure and routed it to the fulfilment contact before conditionally terminating the recovered run as succeeded, extracted fulfilment into a reusable child flow, and switched the approval flow's Dataverse connection to a Microsoft Entra service principal.

You have successfully completed this exercise.

## Clean up

1. Turn off **Lab 08 approval flow** and wait for any in-progress parent and child runs to finish. Then turn off **Lab 08 fulfilment child flow**. This is essential: left on, a later **Reset lab data** run can restore `REQ-SEED-004` to a state the parent flow reacts to.

1. In the **Equipment Provisioning lab 08** solution, open **Connection references** > **Lab 08 Dataverse (approval flow)**. Restore the personal connection you noted before the service-principal test, save, and reopen to verify it. Leave both flows off. This avoids leaving the parent reference tied to the test secret you're about to revoke.

1. In the [Microsoft Entra admin center](https://entra.microsoft.com){:target="_blank"} at `https://entra.microsoft.com`, open the `ab400-lab08-flow` app registration, select **Certificates & secrets**, and delete the client secret you created. Leave the app registration and the application user in place, so you can select them again if you repeat this exercise.

1. In **Solutions** > **Equipment Provisioning (base)**, filter components to **Environment Variables**, select **Availability mode**, clear its **Current value** so it falls back to the `Available` default, and save and publish.

1. With both flows still off, return to the **Requests** view, select **Reset lab data**, wait for it to finish, and select **Refresh**.

Confirm `REQ-SEED-003` is **Submitted**, `REQ-SEED-004` is **Approved**, and both have **Availability** set to **Not checked**. Leave both flows off so the restored data stays ready for other exercises.
