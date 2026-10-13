---
lab:
    title: 'Display profile data through a Power Platform connection in a code app'
    description: 'Configure a React and TypeScript Power Apps code app to call a Power Platform connection with the Power Apps CLI, then verify the connection locally and after publishing.'
    level: 300
    duration: 20
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Display profile data through a Power Platform connection in a code app

Power Apps code apps let you build a web app in your own framework and still call Power Platform connectors, Dataverse, and cloud flows. In this exercise, you configure a supplied React and TypeScript code app to call the Office 365 Users connector through a Power Platform connection, so it can show the signed-in user's own profile information. You use the Power Apps CLI (`pa`) to create the connection, add it to the code app as a data source, and publish the finished app.

Once you can wire one connector into a code app this way, the same pattern applies to every other connector a code app can use.

This exercise should take approximately **20** minutes to complete.

> **Note**: Code apps are an actively developed Power Platform feature. You may notice small differences between what's described here and what you see in the Power Apps CLI or the Power Apps portal.

## Prerequisites

To complete this exercise, you need:

- A Power Platform environment with code apps enabled, as selected in the setup instructions. If you haven't confirmed this yet, see the [setup instructions](./00-setup.md) before you continue.
- [Node.js](https://nodejs.org/){:target="_blank"} (`https://nodejs.org/`) LTS version and npm, installed on your local machine.
- [Git](https://git-scm.com/){:target="_blank"} (`https://git-scm.com/`), installed on your local machine.
- The Power Apps CLI (`pa`), installed on your local machine.
- A code editor, such as [Visual Studio Code](https://code.visualstudio.com/){:target="_blank"} (`https://code.visualstudio.com/`).
- A Chromium-based browser (for example, Microsoft Edge or Google Chrome) with developer tools.

## Get the starter code app and confirm prerequisites

The starter project is a minimal React, TypeScript, and Vite code app, with a read-only profile component that's supplied but not yet wired to any data.

1. Open a terminal and change to the `Allfiles/05-code-app-connection` folder from the repository you cloned in the setup instructions.
1. Install the project's dependencies.

    ```bash
    npm install
    ```

1. Confirm the Power Apps CLI is available on your `PATH`.

    ```bash
    pa --version
    ```

   You should see dependencies install without error, and `pa --version` print a version number, such as `1.2.0`.

## Initialize the code app for your environment

The starter project doesn't include a `power.config.json` file, because that file binds the project to one specific Power Platform environment, and that value is different for every learner. You create it by initializing the app against the environment you selected in the setup instructions.

1. From the `Allfiles/05-code-app-connection` folder, run the app-initialization command.

    ```bash
    pa app init --display-name "ab400-lab05-code-app"
    ```

1. If you're not already signed in, the CLI opens your browser. Sign in using your credentials.
1. When prompted for an environment, select the environment you selected in the setup instructions, the one with code apps enabled.
1. Confirm initialization succeeded. In the project folder, open the new `power.config.json` file, and confirm it now contains your environment ID.

   You should see a `power.config.json` file that didn't exist before this task. Don't edit this file directly — the Power Apps CLI manages its contents for you.

## Create or select a connection and add it as a data source

This code app calls the Office 365 Users connector (connector ID `shared_office365users`) to retrieve the signed-in user's own profile.

> **Note**: Current Microsoft Learn documentation and the Power Apps CLI both name this connector "Office 365 Users." You might also see it referred to as "Microsoft 365 Users" elsewhere — both names refer to the same connector.

1. List the connectors available in your environment, and confirm the Office 365 Users connector appears.

    ```bash
    pa connector list --search "Office 365 Users"
    ```

1. Before creating a new connection, check whether one already exists for this connector.

    ```bash
    pa connection list
    ```

   Review the output for a row whose connector column shows `shared_office365users`. If you find one, copy its **Connection ID** and skip to step 4.
1. If no matching connection exists, create one.

    ```bash
    pa connection create --connector shared_office365users --display-name "AB-400 Lab 05 connection"
    ```

   Approve any sign-in or consent prompts that appear, then copy the connection ID the command returns.
1. Add the connection to the code app as a data source. Replace `<connection-id>` with the ID you copied in the previous step.

    ```bash
    pa app add data-source --connector shared_office365users --connection-id <connection-id>
    ```

   This command generates two new TypeScript files: `Office365UsersModel.ts` and `Office365UsersService.ts`, in `src/generated/models` and `src/generated/services`.

> **Note**: If any `pa` command in this exercise fails with `fetch failed` or a connection timeout, run it again — see [Troubleshooting](./00-setup.md#troubleshooting) in the setup guide. It's a transient connectivity issue, not a sign that something's set up wrong.

   You should see a confirmation message, and the two new files under `src/generated`.

## Bind the profile component to the connection

The supplied `ProfileCard` component already handles loading and error states. It's missing only the call to the generated service, marked by comment placeholders.

1. Open `src/components/ProfileCard.tsx` in your code editor.
1. Find the comment **Import the generated Office 365 Users service here** and add the following line below it:

    ```typescript
    import { Office365UsersService } from '../generated/services/Office365UsersService';
    ```

1. Find the comment **Call the Office 365 Users connection's MyProfile_V2 operation here** and replace the line below it with the following code:

    ```typescript
    const result = await Office365UsersService.MyProfile_V2('displayName,jobTitle,department');
    setProfile({
      displayName: result.data.displayName ?? '',
      jobTitle: result.data.jobTitle ?? '',
      department: result.data.department ?? '',
    });
    ```

   `MyProfile_V2` is the current operation name for retrieving the signed-in user's own profile. The string argument selects which fields the connector returns, so the app only requests the three fields it displays.
1. Save the file (**Ctrl+S**).

   > **Tip**: If you have GitHub Copilot available in your code editor, you can select the two comments and ask Copilot to wire the generated service into the component for you, instead of typing the code yourself.

As you edit the file, keep the existing indentation, using the supplied comments as your guide.

## Run the app locally and sign in to the connection

Now you'll run the app through the Power Apps CLI's local host, which gives the app the same signed-in identity it would have when published.

1. Start the local host.

    ```bash
    pa app run
    ```

1. Open the URL labeled **Local Play** in the same browser profile you use for your Power Platform tenant.
1. The first time the app calls the connection, you'll see a consent dialog that names the Office 365 Users connector and the data it can access. Review it, then accept it. This dialog is expected the first time any app uses a new connection — it isn't an error.
1. Review the profile card in the running app.

   You should see your own display name, job title, and department, in place of the placeholder text "Profile data will appear here once the connection is wired up."

   > **Tip**: If the app shows an error instead of your profile, confirm you completed every step in the previous section, and that the connection ID you used in **Create or select a connection and add it as a data source** matches a connection to `shared_office365users`.

## Push the app and verify the published profile

Running the app locally proves the connection works during development. Publishing it with the Power Apps CLI proves the same connection keeps working once the app is a published, standalone app in your environment — not just a local artifact.

1. Build the compiled app.

    ```bash
    npm run build
    ```

1. Publish the app to your environment.

    ```bash
    pa app push
    ```

1. Open the Power Apps URL the command returns, or go to [Power Apps](https://make.powerapps.com){:target="_blank"} (`https://make.powerapps.com`), find `ab400-lab05-code-app` in your list of apps, and open it.
1. Review the profile card in the published app.

   You should see the same display name, job title, and department you saw when you ran the app locally, confirming the connection you configured survives publishing and isn't a local-only artifact.

## Summary

In this exercise, you configured a React and TypeScript code app to call a Power Platform connection. You used the Power Apps CLI to initialize the app for your environment, create a connection to the Office 365 Users connector, and add that connection as a data source. You then wired the generated service into a supplied profile component, and confirmed the signed-in user's profile appeared both when running the app locally and after publishing it with the Power Apps CLI.

You have successfully completed this exercise.

## Clean up

Clean-up for this exercise is optional. The Power Apps CLI has no command to delete a code app or a connection, and a leftover connection or app from this exercise doesn't interfere with any other exercise. If you want to remove what you created:

1. Go to [Power Apps](https://make.powerapps.com){:target="_blank"} (`https://make.powerapps.com`).
1. Select **Apps**, find `ab400-lab05-code-app`, and delete it.
1. Select **Connections**, find **AB-400 Lab 05 connection**, and delete it.
