---
lab:
    title: 'Build a device-condition Power Apps code component'
    description: 'Create, test, debug, and deploy a Power Apps component framework (PCF) control that renders and edits a bound Dataverse column.'
    level: 300
    duration: 40
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Build a device-condition Power Apps code component

The equipment-provisioning app in this course uses the default Power Apps rendering for every column, including the device condition rating: a plain number box. A code component built with the Power Apps component framework (PCF) can replace that number box with a control that's easier for a coordinator to read and update at a glance.

In this exercise, you build, test, debug, and deploy a PCF field control that renders and edits the **Condition rating** column on the Device table. You start from a supplied, partially scaffolded code component project and complete it using TypeScript, the local test harness, your browser's developer tools, and the Power Platform CLI (`pac`).

In this exercise, you will:

- Declare a bound property in a code component manifest
- Implement the component's lifecycle methods to render and report a rating
- Test and debug the component in the local test harness
- Deploy the component and add it to a model-driven app form

This exercise should take approximately **40** minutes to complete.

## Before you start

Before you start this exercise, confirm your developer environment has the Equipment Provisioning base solution installed, reset the lab data, and sign in to the Power Platform CLI. Every Dataverse exercise in this course starts the same way.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, and confirm the environment selector shows the developer environment you're using for these exercises.
1. In the left navigation pane, select **Solutions**, and confirm that **Equipment Provisioning (base)** is listed. If it isn't installed yet, follow the import steps in [Set up your environment](./00-setup.md) before you continue.
1. In the left navigation pane, select **Apps**, and open the **Equipment Provisioning** app. In the site map, select **Requests**.
1. On the command bar, select **Reset lab data**. Wait for the progress indicator to finish, review the summary in the dialog that appears, and then close it.
1. On the command bar, select **Refresh**.
1. Open a terminal, and sign in to the Power Platform CLI, pointing it at the same environment:

    ```powershell
    pac auth create --url <your environment URL>
    ```

    Replace the placeholder with the URL shown for your environment in the maker portal (for example, something like `https://orgname.crm.dynamics.com`). Sign in with an account that has the System Administrator or System Customizer security role.
1. Confirm the authentication profile was created:

    ```powershell
    pac auth list
    ```

    You should see your profile listed and marked as the active one.

## Open the starter component project

The starter files for this exercise contain a code component project that was created using `pac pcf init`, with the namespace `EquipmentProvisioning` and the component name `DeviceConditionRating`. The control manifest and the lifecycle methods are left incomplete for you to finish.

1. Open Visual Studio Code.
1. Open the `Allfiles/07-device-condition-pcf/DeviceConditionRating` folder from the lab files you cloned in the setup (**File** > **Open Folder**).
1. Take a moment to review the project structure:
    - `DeviceConditionRating.pcfproj` is the project file used when packaging the component.
    - `DeviceConditionRating/ControlManifest.Input.xml` is the control's manifest.
    - `DeviceConditionRating/index.ts` contains the four lifecycle methods you'll implement.
    - `DeviceConditionRating/css/DeviceConditionRating.css` is a stylesheet for the control's elements.
1. Open a new terminal (**Terminal** > **New Terminal**), and install the project's build tools:

    ```powershell
    npm install
    ```

    You should see the command finish without errors. This installs the Power Apps component framework build tools (`pcf-scripts` and `pcf-start`) that the project depends on.

## Declare the bound rating property in the manifest

A code component's manifest declares the data it expects from Dataverse through `property` elements. The starter project still has the generic property the scaffolding tool generated, so your first task is to replace it with one that matches the **Condition rating** column: a whole number from 1 through 5.

1. In the `DeviceConditionRating` subfolder, open `ControlManifest.Input.xml`.
1. Find the `property` element:

    ```xml
    <property name="sampleProperty" display-name-key="Property_Display_Key" description-key="Property_Desc_Key" of-type="SingleLine.Text" usage="bound" required="true" />
    ```

1. Replace it with the following property:

    ```xml
    <property name="rating"
        display-name-key="Rating"
        description-key="The device condition rating, from 1 to 5."
        of-type="Whole.None"
        usage="bound"
        required="true" />
    ```

    The `usage="bound"` attribute marks this property as one the control can both read and change on the underlying column, and `of-type="Whole.None"` matches the whole number type used by the **Condition rating** column. The `name` attribute, `rating`, is how you refer to this value from your TypeScript code.
1. Save the file (**Ctrl+S**).
1. In the terminal, regenerate the TypeScript types for the manifest:

    ```powershell
    npm run refreshTypes
    ```

1. Open `DeviceConditionRating/generated/ManifestTypes.d.ts`, and confirm that `IInputs` and `IOutputs` both declare a `rating` member. This file is regenerated automatically every time you build, so you don't edit it directly.

## Implement the control's lifecycle methods

Every standard code component implements four lifecycle methods: `init` runs once when the control is created, `updateView` runs whenever the bound value changes, `getOutputs` reports a new value back to the platform, and `destroy` runs cleanup when the control is removed. The starter project already has empty stubs for all four, each with a `console.log` line so you can confirm they're firing once you test the control.

> **Tip**: If you have GitHub Copilot available, you can ask it to help draft the body of each method below. Review any suggested code before you accept it, so that you understand how the rating value flows between Dataverse and the control.

### Render the current rating

1. Open `DeviceConditionRating/index.ts`.
1. Find the comment **Add properties for the control** near the top of the class, and add the following private fields:

    ```typescript
    private _value: number;
    private _notifyOutputChanged: () => void;
    private _container: HTMLDivElement;
    private _ratingInput: HTMLInputElement;
    private _ratingLabel: HTMLLabelElement;
    private _onRatingChanged: EventListenerOrEventListenerObject;
    ```

1. Inside the `init` method, find the comment **Add control initialization code**, and add the following code:

    ```typescript
    this._notifyOutputChanged = notifyOutputChanged;
    this._onRatingChanged = this.onRatingChanged.bind(this);

    this._container = document.createElement("div");
    this._container.setAttribute("class", "eqp-rating-container");

    this._ratingInput = document.createElement("input");
    this._ratingInput.setAttribute("type", "range");
    this._ratingInput.setAttribute("min", "1");
    this._ratingInput.setAttribute("max", "5");
    this._ratingInput.setAttribute("step", "1");
    this._ratingInput.setAttribute("class", "eqp-rating-slider");
    this._ratingInput.addEventListener("input", this._onRatingChanged);

    this._ratingLabel = document.createElement("label");
    this._ratingLabel.setAttribute("class", "eqp-rating-label");

    this._value = context.parameters.rating.raw ?? 1;
    this._ratingInput.setAttribute("value", this._value.toString());
    this._ratingLabel.innerHTML = this._value.toString();

    this._container.appendChild(this._ratingInput);
    this._container.appendChild(this._ratingLabel);
    container.appendChild(this._container);
    ```

    This code builds a slider and a label, sets the slider's range to match the rating's 1 through 5 values, reads the current value from `context.parameters.rating`, and adds the elements to the container the framework gave you.
1. Directly after the `init` method (and before `updateView`), add a new method that handles the slider's `input` event:

    ```typescript
    public onRatingChanged(evt: Event): void {
        this._value = parseInt(this._ratingInput.value, 10);
        this._ratingLabel.innerHTML = this._value.toString();
        this._notifyOutputChanged();
    }
    ```

    This method updates the label immediately as the learner drags the slider, and calls `notifyOutputChanged`, which tells the framework to call `getOutputs` to retrieve the new value.
1. Inside the `updateView` method, find the comment **Add code to update control view**, and add the following code:

    ```typescript
    this._value = context.parameters.rating.raw ?? this._value;
    this._ratingInput.setAttribute("value", this._value.toString());
    this._ratingLabel.innerHTML = this._value.toString();
    ```

    `updateView` runs whenever the bound value changes from outside the control, for example when a record is refreshed, so it keeps the slider and label in sync with the column.

### Report a changed rating

1. Inside the `getOutputs` method, find the comment **Add code to report the changed rating**, and replace the `return {};` statement with the following code:

    ```typescript
    return {
        rating: this._value
    };
    ```

    This is what actually reports the new rating back to the platform. Without it, the slider would update its own label, but the underlying column would never change.
1. Inside the `destroy` method, find the comment **Add code to cleanup control if necessary**, and add the following code:

    ```typescript
    this._ratingInput.removeEventListener("input", this._onRatingChanged);
    ```

    This removes the event listener you added in `init`, so the control doesn't leave anything behind when it's removed from the page.
1. Save the file (**Ctrl+S**).

## Run and test the control in the local test harness

The Power Platform CLI includes a local test harness that builds your component and opens it in a browser window, without needing a Dataverse connection. It's the same test harness regardless of whether the component ends up in a model-driven app, a canvas app, or a Power Pages site.

1. In the terminal, from the project's root folder (not the `DeviceConditionRating` subfolder), run:

    ```powershell
    npm start watch
    ```

1. Wait for a browser window to open automatically. This is the test harness, running your component in watch mode: any change you save to `index.ts` or `ControlManifest.Input.xml` rebuilds and reloads it automatically.

    <!-- SCREENSHOT: The test harness browser window, showing the rendered slider control on the left and the Context Inputs, Data Inputs, and Outputs panels on the right. -->
1. In the **Data Inputs** panel, find **rating**, enter a value such as `3`, and confirm the slider and the label both update to match.
1. In the rendered control itself, drag the slider to a different value, and confirm:
    - The label next to the slider updates immediately.
    - The **Outputs** panel shows the new rating value, which confirms `getOutputs` is reporting it correctly.
1. Open your terminal and confirm you see `DeviceConditionRating: init` and `DeviceConditionRating: updateView` logged when the harness first loads, and `DeviceConditionRating: getOutputs` logged each time you move the slider. These are the lifecycle methods you implemented, firing in response to the test harness.

Nicely done. You've built and tested a working code component without ever leaving your local machine. Leave `npm start watch` running. You use the same browser window in the next task.

## Debug the control with browser developer tools

Browser developer tools are the primary way to step through a code component's logic while you're still developing it. You set one breakpoint here to see the incoming context value as `updateView` runs.

1. With the test harness still open from the previous task, open your browser's developer tools (**Ctrl+Shift+I**).
1. Select the **Sources** tab.
1. Use **Ctrl+P** to open the file search, and enter `index.ts`.
1. Select the matching file (a path that looks similar to `webpack://.../DeviceConditionRating/index.ts`).
1. Find the `updateView` method, and set a breakpoint on its first line (the line with the comment, not the method signature) by selecting the line number in the gutter.
1. Switch back to the test harness tab, and change the value in the **Data Inputs** panel for **rating**.
1. Confirm execution pauses at your breakpoint. Hover over `context.parameters.rating.raw` to inspect its value.
1. Select **Resume script execution** (or press **F8**) to continue.
1. When you're finished, stop the running process in the terminal (**Ctrl+C**).

## Deploy the control with `pac pcf push`

`pac pcf push` compiles your component and imports it directly into your Dataverse environment, without the longer cycle of building and importing a full solution. It's intended for the kind of fast iteration you just did locally, now pointed at a real environment.

1. In the terminal, from the project's root folder, confirm you're still authenticated to the correct environment:

    ```powershell
    pac auth list
    ```

1. Deploy the component, using the `eqp` publisher prefix to match the Equipment Provisioning Labs publisher:

    ```powershell
    pac pcf push --publisher-prefix eqp
    ```

1. Wait for the command to finish. It builds a production version of the component and imports it into your environment inside a temporary unmanaged solution, so the component becomes immediately available to add to any table column.

    > **Note**: If you receive an error about a missing `MSBuild.exe` or `dotnet.exe`, confirm a .NET SDK is installed, or run the command from a developer command prompt.

You don't see the control in action yet — that's the next task.

## Add the control to the device main form

With the component deployed, you can add it to a column just like any other component, using the form designer.

1. In the maker portal, select **Tables**, and open the **Device** table.
1. Select **Forms**, and open the **Information** form.
1. In the form preview, select the **Condition rating** column.
1. In the property pane, under **Components**, select **+ Component**.
1. In the **Add component** dialog, search for `Device Condition Rating`, select **Device Condition Rating (Lab 07)**, and select **Add**.

    <!-- SCREENSHOT: The Add component dialog with Device Condition Rating (Lab 07) found by search and highlighted. -->
1. In the dialog that appears, leave **Web**, **Mobile**, and **Tablet** all selected, and select **Done**.
1. On the command bar, select **Save**, and then select **Publish**.

You should see **Device Condition Rating (Lab 07)** listed under **Components** for the **Condition rating** column.

## Change a device rating and confirm the update

This is the moment that proves the control is really bound to the column, and not just updating its own local state.

1. In the **Equipment Provisioning** app, in the site map, select **Devices**.
1. Open the device with asset tag `LAP-1001` (Avery Chen's laptop).
1. Confirm the **Condition rating** field now renders as your slider control, instead of a plain number box.

    <!-- SCREENSHOT: The device Information form for LAP-1001, with the Condition rating field rendered as the rating slider and label instead of a number box. -->
1. Drag the slider to a different value, and select **Save**.
1. Refresh the record (or close and reopen it), and confirm the rating you set is still there.

If you see your new rating after the refresh, you've confirmed the full round trip: your `getOutputs` method reported the change, and the platform saved it to the `eqp_conditionrating` column. The next time anyone runs **Reset lab data**, this device's rating is restored to its baseline value automatically.

## Clean up

If you've finished exploring, remove the control from the device form so that the column is restored to the base solution's default appearance for other learners and other exercises that use the same environment.

1. In the maker portal, select **Tables**, open the **Device** table, select **Forms**, and open the **Information** form.
1. In the form preview, select the **Condition rating** column.
1. In the property pane, under **Components**, select the **Device Condition Rating (Lab 07)** component, and remove it from the column.
1. Confirm the form preview shows the column's default number rendering again.
1. On the command bar, select **Save**, and then select **Publish**.

You don't need to delete the deployed component itself or uninstall the base solution — leaving the component in the environment is harmless, and it's reused if you repeat this exercise later.

## Summary

In this exercise, you built a Power Apps code component from a partially scaffolded starter project. You declared a bound manifest property, implemented all four of a standard control's lifecycle methods, tested and debugged the control locally using the test harness and your browser's developer tools, and deployed it with the Power Platform CLI. Finally, you added the control to a Dataverse column and confirmed that changing its value through the control updates the underlying record.

You have successfully completed this exercise.
