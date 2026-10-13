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

1. Select **Lab 08 approval flow**, and on the command bar select **Turn on**. Repeat for **Lab 08 fulfilment child flow**. You should see **On** in the status column for both.

1. Prepare a Microsoft Entra service principal for later in this exercise:

    1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com){:target="_blank"} at `https://entra.microsoft.com`.
    1. Browse to **Identity** > **Applications** > **App registrations**, and select **New registration** — or select an existing registration named `ab400-lab08-flow` if you've already created one.
    1. Enter the name `ab400-lab08-flow`, leave **Redirect URI** blank, and select **Register**.
    1. On the app's **Overview** page, note the **Application (client) ID** and **Directory (tenant) ID**. You'll need both later.
    1. Select **Certificates & secrets** > **Client secrets** > **New client secret**. Enter a description, choose the shortest expiry offered, and select **Add**.
    1. Copy the secret's **Value** immediately and store it somewhere temporary — you can't retrieve it again after you leave this page.

1. Create or select the application user for this service principal:

    1. Sign in to the [Power Platform admin center](https://admin.powerplatform.microsoft.com){:target="_blank"} at `https://admin.powerplatform.microsoft.com`.
    1. Select **Environments**, select your developer environment, and select **Settings**.
    1. Select **Users + permissions** > **Application users**, and select **+ New app user**.
    1. Select **+ Add an app**, search for `ab400-lab08-flow`, select it, and select **Add**.
    1. Select a **Business unit**, then select the edit icon next to security roles and assign **Equipment Integration** and **Basic User**. Select **Save**, confirm the role assignment, and select **Create**.

1. Switch back to the Power Apps maker portal, open the **Equipment Provisioning** app, open the **Requests** view, and select **Reset lab data** on the command bar. Wait for it to finish, then select **Refresh**.

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

1. Still editing **Lab 08 approval flow**, inside the **Is_newly_approved** condition's **Yes** branch, add a new **Scope** action positioned immediately before **Record_availability**. Rename it `Try`.

1. Drag the existing **Check_availability** action into the **Try** scope. The designer automatically updates **Record_availability**'s run-after condition to run after **Try** succeeds, instead of after **Check_availability** directly.

1. Select **Check_availability**, open its **...** menu, select **Settings**, and under **Retry Policy** set:

    - **Type**: Fixed Interval
    - **Count**: `2`
    - **Interval**: `PT5S`

    This replaces the supplied **None** policy with two retries, five seconds apart, so a transient failure gets a bounded second chance without the forced-failure test in the final task taking long to run.

1. Add a second **Scope** action immediately after **Try**. Rename it `Catch`.

1. Select **Catch**, open its **...** menu, select **Configure run after**, and set it to run when **Try** **has failed** and **has timed out**. Clear **is successful**.

1. Inside **Catch**, add an **Update a row** action for the **Microsoft Dataverse** connector:

    - **Table name**: Requests
    - **Row ID**: the dynamic content item for the triggering row's unique identifier (it may be labeled **Request** or similar, depending on your designer)
    - **Request status**: Needs attention
    - **Processing note**: select **Expression**, and build:

        ```text
        concat('The availability check failed and exhausted its retries on ', utcNow(), '. Route this request to ', <Fulfilment contact>, ' for manual follow-up.')
        ```

        Type the literal text segments and `utcNow()` yourself, then switch to the **Dynamic content** tab and select **Fulfilment contact** at the point marked `<Fulfilment contact>` — don't type that part, because the designer inserts the correct underlying reference for you.

        > **Tip**: GitHub Copilot can help you draft this `concat()` expression if you describe what it should say.

1. Save the flow.

You should see **Check_availability** nested inside **Try**, a **Catch** scope configured to run only when **Try** fails or times out, and **Catch**'s **Update a row** action ready to compose the failure note. You'll trigger this path for real in the final task.

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

1. Still editing **Lab 08 approval flow**, select the existing **Start_fulfilment** action — the **Update a row** action that runs after **Record_availability** — and delete it.

1. In its place, add an action. On the **Built-in** tab, select the **Flows** connector, and select **Run a Child Flow**.

1. Select **Lab 08 fulfilment child flow** as the child flow to run.

1. For its **Request ID** input, select the dynamic content item for the triggering row's unique identifier.

1. Save the flow.

1. Test it: in the **Equipment Provisioning** app, open `REQ-SEED-002` — now showing **In fulfillment** from the previous task — change **Request status** back to **Submitted**, save, then change it to **Approved** again, and save.

1. Check **Run history**. You should see a new, successful run, and `REQ-SEED-002`'s **Processing note** should now read "Fulfilment started on … **by the child flow**" — proof that the child flow ran, not the parent's old inline action.

## Authenticate the Dataverse connection with a service principal

The approval flow still authenticates as you. Switch its Dataverse connection to the service principal you prepared earlier, so the flow's identity no longer depends on whoever happens to be signed in.

1. In the **Equipment Provisioning lab 08** solution, select **Connection References**, and select **Lab 08 Dataverse (approval flow)**.

1. Under **Connection**, select **+ New connection**.

1. In the new-connection dialog for **Microsoft Dataverse**, select **Connect with service principal** instead of **Sign in**.

1. Enter:

    - **Connection name**: `Lab 08 service principal`
    - **Application (client) ID**: the value you noted when you registered `ab400-lab08-flow`
    - **Client Secret**: the secret value you copied earlier
    - **Tenant**: the **Directory (tenant) ID** you noted

1. Select **Create**.

1. Confirm **Lab 08 service principal** is selected on the connection reference pane, and select **Save**.

> **Note**: Only the parent flow's connection switches to the service principal. **Lab 08 fulfilment child flow** keeps its own Dataverse connection on your sign-in — child flows in a solution must use embedded connections, and splitting a second authentication mechanism into this exercise wouldn't teach anything new.

1. Open **Lab 08 approval flow**'s details page, and confirm there's no **Invalid Connection** warning. Turn the flow **Off**, then **On** again, so it picks up the new connection.

You should see the flow's details page report a healthy connection, with no sign-in prompt the next time it runs.

## Force a failure and confirm the flow routes it for follow-up

Now put the whole chain under stress: force the availability check to fail on every attempt, and confirm the flow recovers gracefully instead of failing silently.

1. In **Solutions** > **Equipment Provisioning (base)**, filter components to **Environment Variables**, select **Availability mode**, set its **Current value** to `Error`, and save and publish.

1. In the **Equipment Provisioning** app, open the **Requests** view and select **Reset lab data** again. Wait for it to finish, then select **Refresh**. This restores `REQ-SEED-003` to **Submitted**.

1. Open `REQ-SEED-003`, change **Request status** to **Approved**, and save.

1. Wait about 20 seconds for the two retries to run their course, then check **Run history** for **Lab 08 approval flow**. You should see a new run whose overall status is **Succeeded**, even though **Check_availability** inside **Try** shows **Failed** after exhausting its retries — the **Catch** scope's **Configure run after** condition turned a failed call into a handled outcome.

1. Open `REQ-SEED-003` again. You should see:

    - **Request status**: Needs attention
    - **Processing note**: a message naming the failure and routing it to the fulfilment contact's address, for example "The availability check failed and exhausted its retries on 2026-09-30T18:42:11Z. Route this request to equipment-desk@example.com for manual follow-up."

This is the exercise's payoff: the same failure that disappeared without a trace in the second task now exhausts a bounded retry, gets caught, and leaves a record naming both the problem and who should look at it.

## Summary

In this exercise, you started from a supplied naive approval flow and made it production-safe. You configured the Dataverse trigger's **Select columns** and **Filter rows** so it only reacts to a request becoming Approved, wrapped its external availability call in a Try/Catch scope with a bounded retry policy, composed an expression that records a failure and routes it to the fulfilment contact, extracted the fulfilment steps into a reusable child flow, and switched the approval flow's Dataverse connection to authenticate as a Microsoft Entra service principal instead of your own sign-in.

You have successfully completed this exercise.

## Clean up

1. Turn off **Lab 08 approval flow** and **Lab 08 fulfilment child flow** (select each flow, then select **Turn off** on the command bar). This is essential: left on, a later **Reset lab data** run — in this exercise or another — can silently restore `REQ-SEED-004` to a state the flow reacts to.

1. In the [Microsoft Entra admin center](https://entra.microsoft.com){:target="_blank"} at `https://entra.microsoft.com`, open the `ab400-lab08-flow` app registration, select **Certificates & secrets**, and delete the client secret you created. Leave the app registration and the application user in place, so you can select them again if you repeat this exercise.

1. In **Solutions** > **Equipment Provisioning (base)**, filter components to **Environment Variables**, select **Availability mode**, clear its **Current value** so it falls back to the `Available` default, and save and publish.

**Reset lab data** already restored `REQ-SEED-003` and `REQ-SEED-004` to their baseline values, so you don't need to touch seed data yourself.
