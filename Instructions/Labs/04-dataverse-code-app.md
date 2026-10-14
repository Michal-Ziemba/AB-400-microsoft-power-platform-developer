---
lab:
    title: 'Build a Dataverse equipment request code app'
    description: 'Connect a Power Apps code app to Dataverse tables, call a Dataverse custom API and an environment variable, and gate a business rule on the client.'
    level: 300
    duration: 35
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Build a Dataverse equipment request code app

In this exercise, you build a Power Apps code app - a React and TypeScript application that calls Dataverse directly through strongly typed, generated services instead of a low-code canvas. You'll connect it to three Dataverse tables, call a Dataverse custom API to check a business rule, and read an environment variable at run time. Together, these are the three ways a code app uses Dataverse beyond simple data storage.

The app you build lets an employee ask for a replacement for a device they currently hold. It reads the employee's own record and their devices from Dataverse, calls a supplied eligibility check before letting the request through, and shows a fulfilment contact address that's read live from an environment variable rather than hardcoded.

This exercise should take approximately **35** minutes to complete.

> **Note**: Code apps and the Power Apps CLI are being actively developed, so command names, flags, and generated file names can change between releases. If a command or a generated file name in this exercise doesn't quite match what you see, check the current [Power Apps CLI command reference](https://learn.microsoft.com/power-apps/developer/code-apps/reference/cli){:target="_blank"} at `https://learn.microsoft.com/power-apps/developer/code-apps/reference/cli`.

## Confirm the base solution and reset lab data

This and every other Dataverse exercise in this course share one base solution, with the same four tables, sample data, a custom API, and an environment variable. Confirm it's installed, then reset the sample data to a known starting point.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, and select the environment you're using for these exercises.
1. Select **Solutions**, and confirm that **Equipment Provisioning (base)** is listed. If it isn't, follow the import steps in the [setup guide](./00-setup.md), then come back here.
1. In the left navigation, select **Apps**, and open **Equipment Provisioning**.
1. In the app, select **Requests** under **Equipment provisioning**.
1. On the command bar, select **Reset lab data**.
1. Wait for the progress indicator, review the **Lab data reset** summary, and close the dialog.
1. Select **Refresh**.

You should see the **Active Requests** view populated with sample requests, including open requests for employee `EMP-001` - that's your own employee row for the rest of this exercise.

> **Note**: Reset lab data only writes values that differ from the lab's starting data, and it never deletes a request you create later in this exercise. You can run it as many times as you like, on this or any other exercise.

## Get the starter code app and connect it to your environment

The starter files for this exercise are a React and TypeScript project, scaffolded from the Power Apps CLI's Vite template and already set up to work with code apps. It has a request form with nothing connected to Dataverse yet - you'll wire that up over the next three tasks.

1. Open a terminal, and go to the `Allfiles/04-dataverse-code-app` folder in the lab files you cloned in the setup. If you didn't clone the repository, download the files from the [04-dataverse-code-app folder](https://github.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/tree/main/Allfiles/04-dataverse-code-app){:target="_blank"} at `https://github.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/tree/main/Allfiles/04-dataverse-code-app`.
1. Open the folder in Visual Studio Code.
1. In the integrated terminal, install the project's dependencies:

    ```bash
    npm install
    ```

1. Confirm the Power Apps CLI version:

    ```bash
    pa --version
    ```

    Use version 1.3.0 or later. If your installed version is older, update it before you initialize the app:

    ```bash
    npm install --global @microsoft/power-apps-cli@latest
    ```

    This avoids generating the service models with an older CLI.

1. Initialize the code app for your environment:

    ```bash
    pa app init
    ```

    When prompted, sign in using your credentials, select the environment you're using for these exercises, and create a code app named `AB400 Lab 04 - Equipment Request` - or select it if you already created it on an earlier run of this exercise.

1. Start the local host for the app:

    ```bash
    pa app run
    ```

1. Open the URL labeled **Local Play** in the same browser profile you use for your Power Platform tenant.

You should see the **Equipment request** form, with a status message reading "Dataverse tables are not connected yet." That's expected - you connect the form to Dataverse in the next task.

> **Note**: Leave `pa app run` running in this terminal for the rest of the exercise; it picks up most code changes automatically, so you usually only need to refresh the **Local Play** page. Open a second terminal in the same folder for the `pa` commands in the tasks that follow.

## Add Dataverse tables as data sources

Connect the form to the three Dataverse tables it needs: Request, Employee, and Device. The Power Apps CLI generates a strongly typed model and a service for each one.

1. In a second terminal, in the same folder, add each table as a data source by its logical name:

    ```bash
    pa app add data-source --connector dataverse --table eqp_request
    pa app add data-source --connector dataverse --table eqp_employee
    pa app add data-source --connector dataverse --table eqp_device
    ```

    > **Note**: If a command fails with `fetch failed` or a connection timeout, run it again — see [Troubleshooting](./00-setup.md#troubleshooting) in the setup guide. It's a transient connectivity issue, not a sign that something's set up wrong.

1. Open the `src/generated/services` folder and note the three service file names the CLI generated — one per table. The CLI names them from each table's logical name, with only the first letter capitalized (for example `Eqp_requestsService.ts`, `Eqp_employeesService.ts`, `Eqp_devicesService.ts`); these names are case-sensitive, so confirm the exact names here before the next step.

    > **Note**: Every table in this solution also has an alternate key (for example `eqp_key_employeenumber` on Employee), used by the Reset lab data fixture you ran earlier in this exercise. Code apps don't support alternate keys today, so - like every code app - this exercise finds rows by filtering on a plain column instead, such as `eqp_employeenumber`.

1. Open `src/components/RequestForm.tsx`. At the top of the file, under the existing imports, add imports for the three generated services (adjust the names if yours differ from the step above):

    ```typescript
    import { Eqp_employeesService } from '../generated/services/Eqp_employeesService';
    import { Eqp_devicesService } from '../generated/services/Eqp_devicesService';
    import { Eqp_requestsService } from '../generated/services/Eqp_requestsService';
    ```

1. Find the comment **Load the employee and device data sources** and replace the entire `loadEmployeeAndDevices` function with the following code:

    ```typescript
    async function loadEmployeeAndDevices() {
      // Load the employee and device data sources
      const employeeResult = await Eqp_employeesService.getAll({
        select: ['eqp_employeeid'],
        filter: "eqp_employeenumber eq 'EMP-001'",
        top: 1,
      });

      const myEmployee = employeeResult.data?.[0];
      if (!myEmployee) {
        setStatus('Could not find your Employee row (EMP-001). Run Reset lab data and try again.');
        return;
      }

      setEmployeeId(myEmployee.eqp_employeeid);

      const deviceResult = await Eqp_devicesService.getAll({
        select: ['eqp_deviceid', 'eqp_name', 'eqp_devicetype'],
        filter: `_eqp_assignedto_value eq ${myEmployee.eqp_employeeid}`,
      });

      setDevices(
        (deviceResult.data ?? []).map((device) => ({
          eqp_deviceid: device.eqp_deviceid,
          eqp_name: device.eqp_name ?? '',
          eqp_devicetype: Number(device.eqp_devicetype ?? 0),
        })),
      );
      setStatus(null);
    }
    ```

    This finds your own Employee row by its employee number (a plain column, not the alternate key), then retrieves only the devices whose `eqp_assignedto` lookup points at that row - using the lookup's plain `_eqp_assignedto_value` column, the same pattern Dataverse uses for any lookup filter. The `.map` step copies just the fields this form needs, so the form doesn't depend on exactly which columns the generated model marks as optional.

1. Save the file, and refresh the **Local Play** page.

You should see the device dropdown populated with `LAP-1001` and `PHN-1002` - the two devices seeded for `EMP-001` - and the status message is gone.

> **Tip**: If you use GitHub Copilot, this is a good point to ask it to explain the generated service file for one of your tables, or to suggest the `select`/`filter` options for a query you haven't written yet.

## Call the eligibility custom API and gate the request form

Before the form creates a request, it calls the supplied `eqp_IsEligibleForReplacement` custom API to check whether the selected device is due for replacement, and only continues if the answer is yes.

1. In your terminal, search for the custom API:

    ```bash
    pa app find-dataverse-api --search "eqp_IsEligibleForReplacement"
    ```

    > **Note**: A custom API that was imported recently can take a few minutes before `pa app find-dataverse-api` discovers it - this is a known timing issue after import (tracked as [microsoft/PowerAppsCodeApps#433](https://github.com/microsoft/PowerAppsCodeApps/issues/433){:target="_blank"}, `https://github.com/microsoft/PowerAppsCodeApps/issues/433`). If the search returns nothing, wait a few minutes and try again - it isn't something you did wrong.

1. Add it to the app:

    ```bash
    pa app add dataverse-api --api-name eqp_IsEligibleForReplacement
    ```

1. Open `src/generated/services` again, and confirm the name of the generated service file for `eqp_IsEligibleForReplacement` — it keeps the custom API's own name, with a capital first letter (`Eqp_IsEligibleForReplacementService.ts`). Import it at the top of `src/components/RequestForm.tsx`:

    ```typescript
    import { Eqp_IsEligibleForReplacementService } from '../generated/services/Eqp_IsEligibleForReplacementService';
    ```

1. In the same file, find the comment **Call the eligibility custom API**. Replace it, the `let isEligible = false;` line above it, and the explanatory comment lines below it, with the following code:

    ```typescript
      // Call the eligibility custom API
      const eligibility = await Eqp_IsEligibleForReplacementService.eqp_IsEligibleForReplacement(
        selectedDeviceId,
      );
      const isEligible = Boolean(eligibility.data?.IsEligible);
    ```

    This passes the selected device's ID as the `DeviceId` parameter, and reads the `IsEligible` value from the response.

1. Find the comment **Create the replacement request**. Replace it, the explanatory comment lines below it, and the `setStatus('Request creation is not connected yet.');` placeholder, with the following code:

    ```typescript
      // Create the replacement request
      const requestedDeviceType = selectedDevice.eqp_devicetype;
      if (
        requestedDeviceType !== 100000000 &&
        requestedDeviceType !== 100000001 &&
        requestedDeviceType !== 100000002 &&
        requestedDeviceType !== 100000003
      ) {
        setStatus('The selected device has an unsupported device type. Check its Device type in Dataverse.');
        return;
      }

      const newRequest: Parameters<typeof Eqp_requestsService.create>[0] = {
        eqp_justification: justification,
        eqp_requesteddevicetype: requestedDeviceType,
        statecode: 0 as const,
        'eqp_Employee@odata.bind': `/eqp_employees(${employeeId})`,
        'eqp_CurrentDevice@odata.bind': `/eqp_devices(${selectedDeviceId})`,
      };

      const result = await Eqp_requestsService.create(newRequest);

      if (result.data) {
        setStatus(`Request ${result.data.eqp_name ?? ''} submitted for ${selectedDevice.eqp_name}.`);
        setJustification('');
        setSelectedDeviceId('');
      } else {
        setStatus('Failed to create the request.');
      }
    ```

    `Parameters<typeof Eqp_requestsService.create>[0]` uses the generated service's input type to check the payload before the call. The `@odata.bind` property names match that model exactly, including capitalization (`eqp_Employee`, `eqp_CurrentDevice`). They differ from the lowercase lookup logical names.

    The device-type check narrows the value to the four numeric options supported by the generated choice type. Don't convert it to a string. Set `statecode` to the number `0` (Active). `as const` preserves the literal `0` type. Dataverse fills in the rest, including the autonumbered request number (`eqp_name`) this code reads back from the response.

1. Save the file, refresh the **Local Play** page, select `LAP-1001`, enter a justification, and select **Submit request**.

You should see the message "LAP-1001 isn't eligible for replacement yet." - `LAP-1001` was issued 8 months ago, inside the 24-month replacement window. You'll submit an eligible request, and prove this gate properly, in a later task.

> **Note**: This gate runs entirely in the code app. Dataverse itself still allows the request to be created either way - the eligibility rule is enforced server-side only when a coordinator approves a request, not when it's created - so don't rely on this check as a security boundary; it's a convenience for the person filling in the form.

> **Tip**: If a call to the custom API fails with an error you don't recognize, ask GitHub Copilot to help interpret the error message and check the request payload against the custom API's parameters.

## Read an environment variable from the code app

The app also shows a fulfilment contact address, read at run time from the `eqp_FulfillmentContact` environment variable, so it can change per environment without a code change.

1. List the environment variables available to your code app:

    ```bash
    pa app list-environment-variables
    ```

    This lists variable names only - including `eqp_FulfillmentContact` - not their values. You read a value by calling a Dataverse function, next.

1. Find the function that reads a variable's value:

    ```bash
    pa app find-dataverse-api --search "RetrieveEnvironmentVariableValue"
    ```

    > **Note**: You might see environment variable references written as `@envvar:schemaname` elsewhere - that shorthand only works for a connector data source such as SharePoint, and doesn't apply to Dataverse tables. This task calls the platform function directly instead, which works the same way for any Dataverse environment variable.

1. Add it to the app:

    ```bash
    pa app add dataverse-api --api-name RetrieveEnvironmentVariableValue
    ```

1. Open `src/generated/services`, confirm the generated service file's name, and import it at the top of `src/components/FulfilmentContact.tsx` (adjust the name if yours differs):

    ```typescript
    import { RetrieveEnvironmentVariableValueService } from '../generated/services/RetrieveEnvironmentVariableValueService';
    ```

1. Find the comment **Read the fulfilment contact environment variable**. Replace it, the explanatory comment lines below it, and the `setContact(null);` placeholder, with the following code:

    ```typescript
      // Read the fulfilment contact environment variable
      const result = await RetrieveEnvironmentVariableValueService.RetrieveEnvironmentVariableValue(
        'eqp_FulfillmentContact',
      );
      const value = result.data?.Value;
      setContact(typeof value === 'string' ? value : null);
    ```

    This always re-reads the live value - never assume you're seeing the published default, because a previous run of this exercise, or another learner in the same environment, may already have set a current value. The generated service returns the response as an untyped record, so the `typeof` check is what lets this value be assigned to a `string | null` field.

1. Save the file, and refresh the **Local Play** page.

You should see a contact address under "Questions about fulfilment?" - this is whatever value the environment currently holds, default or not.

1. In the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, select **Solutions**, and open **Equipment Provisioning (base)**.
1. Filter the components list to **Environment variables**, and select **Fulfillment contact**.
1. Under **Current Value**, enter a new value, for example `lab04-fulfilment@example.com`, then select **Save**.
1. Refresh the **Local Play** page again.

You should now see the new value you just set, confirming the app reads the environment variable live, not a value baked in at build time.

## Submit a request and prove the eligibility rule

You've connected all three building blocks - Dataverse tables, a custom API, and an environment variable. Now prove the rule they implement actually holds, in both directions.

1. In the running app, select `PHN-1002`, enter a justification such as `Device is past its replacement period`, and select **Submit request**.

You should see a message confirming a new request number was submitted for `PHN-1002` - `PHN-1002` was issued 26 months ago, past the 24-month window, so the eligibility check passed and the app created the row.

1. In the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, open the **Equipment Provisioning** app, select **Requests**, and confirm your new request appears in the **Active Requests** view, with status **Submitted** and **Device to replace** set to `PHN-1002`.
1. Back in the app, select `LAP-1001`, enter a justification, and select **Submit request** again.

You should see the same "isn't eligible for replacement yet" message as before, and no new row for `LAP-1001` in the Requests view - the same rule, holding for the same employee, for two devices with different issue dates.

## Clean up

If you set a current value on the `eqp_FulfillmentContact` environment variable in this exercise, you can remove it so a later run of this exercise - or the resilient cloud flow exercise, which reads the same variable - sees the published default again.

1. In the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, select **Solutions**, and open **Equipment Provisioning (base)**.
1. Filter the components list to **Environment variables**, and select **Fulfillment contact**.
1. Under **Current Value**, delete the value you entered, and select **Save**.

The base solution, its tables, and the seed data are shared by every exercise in this course, so this exercise's clean-up never removes or changes them. You also don't need to delete the code app itself - leaving it doesn't affect any other exercise.
