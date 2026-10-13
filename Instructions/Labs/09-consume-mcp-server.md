---
lab:
    title: 'Consume a Power Platform MCP server from an MCP client'
    description: 'Connect GitHub Copilot CLI to an existing Dataverse MCP server, inspect the tools a supplied allowlist exposes, and retrieve equipment-provisioning data through a read-only connection.'
    level: 300
    duration: 30
    islab: true
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Consume a Power Platform MCP server from an MCP client

Microsoft Dataverse can act as a Model Context Protocol (MCP) server, exposing a governed set of tools that an MCP client can call to read and write Dataverse data. In this exercise, you connect GitHub Copilot CLI — an MCP client that runs in a terminal — to your own environment's Dataverse MCP server, restrict it to a supplied read-only tool list, and use it to retrieve open equipment-replacement requests. You then try an action the supplied tool list doesn't allow, and see the client refuse it, because no tool exists on this connection to perform it.

You don't build, register, or publish an MCP server in this exercise. The server already exists as a feature of your environment; your job is to turn it on, connect a client to it, and work within the boundary a supplied tool allowlist sets.

In this exercise, you will:

- Enable the Dataverse MCP server for your environment and allow the GitHub Copilot client to use it
- Install GitHub Copilot CLI and connect it to your environment's MCP server with a supplied, read-only tool allowlist
- Enumerate and inspect the tools the connection exposes
- Retrieve open replacement requests through a read-only tool call
- Confirm that a write attempt through the same connection is refused

This exercise should take approximately **30** minutes to complete.

> **Note**: GitHub Copilot CLI and the Dataverse MCP server are both actively developed. Command names, configuration file locations, and on-screen labels may have changed since this exercise was written. If a step doesn't match what you see, check the current [GitHub Copilot CLI documentation](https://docs.github.com/en/copilot/concepts/agents/copilot-cli/about-copilot-cli){:target="_blank"} at `https://docs.github.com/en/copilot/concepts/agents/copilot-cli/about-copilot-cli`.

## Prerequisites

To complete this exercise, you need:

- Your own Power Platform developer environment with Dataverse, where you have administrator rights (see [00-setup](./00-setup.md))
- [Node.js](https://nodejs.org/){:target="_blank"} version 22 or later, available at `https://nodejs.org/`
- A GitHub account with GitHub Copilot access (Copilot Free is sufficient)

> **Note**: Dataverse MCP tool calls made from a client other than Microsoft Copilot Studio are billed using Copilot Credits, unless your tenant holds a Dynamics 365 Premium license or a Microsoft 365 Copilot license. This exercise makes only two or three billable tool calls in total, but if your account has no Copilot Credits available, read the expected results shown in each task instead of running that step live.

## Before you start

Every exercise that uses the equipment-provisioning scenario starts from the same supplied base solution. Confirm it's in place before you connect a client to it.

1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, and confirm the environment selector shows the developer environment you intend to use.

1. Select **Solutions**, and confirm that **Equipment Provisioning (base)** appears in the list.

   If it isn't there, follow the import steps in [00-setup](./00-setup.md) before you continue.

1. Open the **Equipment Provisioning** app from the list of apps.

1. In the site map, select **Requests**.

1. On the command bar, select **Reset lab data**.

   A progress indicator appears, followed by a **Lab data reset** dialog summarizing what the command changed. The exact numbers depend on what's already in your environment.

1. Close the dialog, and then select **Refresh**.

You should see the **Active Requests** view populated with the seeded requests, including four requests numbered `REQ-SEED-001` through `REQ-SEED-004`. These are the rows you retrieve later in this exercise.

## Get your Dataverse environment URL

The Dataverse MCP server runs at a URL based on your own environment's instance URL. You need that URL before you can connect a client to it.

1. In the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, select the gear icon, and then select **Session details**.

1. Copy the value shown under **Instance url**. It looks similar to `https://contoso00001.crm.dynamics.com/`.

1. Note this value somewhere you can paste it from later in this exercise. The Dataverse MCP server endpoint is this instance URL with `/api/mcp` appended, for example `https://contoso00001.crm.dynamics.com/api/mcp`.

1. Close the **Session details** panel.

You should now have your environment's instance URL noted somewhere you can paste it from in a later task.

## Enable the Dataverse MCP server and allow the GitHub Copilot client

By default, the Dataverse MCP server only allows Microsoft Copilot Studio to connect. Before GitHub Copilot CLI can connect, you need to turn on the server for your environment and explicitly allow the GitHub Copilot client. Both changes are reversible environment settings, and you make them as the administrator of your own environment.

1. Open the [Power Platform admin center](https://admin.powerplatform.microsoft.com/){:target="_blank"} at `https://admin.powerplatform.microsoft.com/`.

1. Select **Manage**, and then select **Environments**.

1. Select the environment you confirmed in the previous tasks.

1. Select **Settings**.

1. Under **Settings**, select **Product**, and then select **Features**.

1. Scroll down to the **Dataverse Model Context Protocol** section, and confirm that **Allow MCP clients to interact with Dataverse MCP server** is turned on. If it isn't, turn it on.

1. On the same page, select **Advanced Settings**.

   A list of MCP clients appears, showing which ones are currently allowed to connect.

1. Open the **Microsoft GitHub Copilot** client record.

1. Set **Is Enabled** to **Yes**, and then select **Save & Close**.

You should now see the **Microsoft GitHub Copilot** client listed as enabled. Leaving this setting on doesn't cost anything until a client actually calls a tool, and you can safely leave it enabled after this exercise.

## Install GitHub Copilot CLI

GitHub Copilot CLI is a command-line MCP client: it runs in your terminal, and you can connect it to MCP servers in addition to the tools it provides out of the box.

1. Open a terminal, and confirm you have a supported version of Node.js installed:

    ```bash
    node --version
    ```

   The output must show version 22 or later. If it doesn't, install a current LTS release from [nodejs.org](https://nodejs.org/){:target="_blank"} at `https://nodejs.org/` before you continue.

1. Install GitHub Copilot CLI:

    ```bash
    npm install -g @github/copilot
    ```

1. Confirm the installation succeeded:

    ```bash
    copilot --version
    ```

1. Start an interactive session:

    ```bash
    copilot
    ```

1. If you're not already signed in to GitHub, follow the `/login` prompt that appears and complete the sign-in flow in your browser.

1. Type `/exit` to end the session for now. You'll reconnect once the MCP server configuration is in place.

You should see a version number from `copilot --version`, confirming GitHub Copilot CLI is installed and ready to connect to an MCP server.

## Add the supplied MCP server configuration

A sample configuration file restricts GitHub Copilot CLI's connection to the Dataverse MCP server to three read-only tools: `read_query`, `search`, and `describe`. None of the tools that create, update, or delete data appear in this list, so this connection can't make any of those calls, no matter what you ask it to do.

1. Download [mcp-config.sample.json](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/09-consume-mcp-server/mcp-config.sample.json){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/09-consume-mcp-server/mcp-config.sample.json`.

1. Open the file in a text editor, and review its contents:

    ```json
    {
      "mcpServers": {
        "DataverseMcp": {
          "type": "http",
          "url": "https://<your-org>.crm.dynamics.com/api/mcp",
          "tools": ["read_query", "search", "describe"]
        }
      }
    }
    ```

   The `"tools"` array is the allowlist. GitHub Copilot CLI only exposes the tools named here, even though the Dataverse MCP server supports more.

   > **Note**: The Dataverse MCP server's tool surface changed recently. `search` now searches table schemas and business skills (metadata), not equipment-provisioning data — the tool that used to do that is now called `search_data`. This allowlist doesn't include `search_data`, so this connection can't search request, device, or employee records by keyword; the next task uses `read_query` for that instead. If you maintain your own tool allowlists outside this exercise, re-check them against the current [Dataverse MCP server documentation](https://learn.microsoft.com/power-apps/maker/data-platform/data-platform-mcp){:target="_blank"} (`https://learn.microsoft.com/power-apps/maker/data-platform/data-platform-mcp`) for the same reason.

1. Replace `<your-org>.crm.dynamics.com` with the instance URL you noted earlier, keeping the `/api/mcp` suffix.

1. Save the file as `mcp-config.json` in your GitHub Copilot CLI configuration folder, `~/.copilot/mcp-config.json`. If that file already exists and contains other servers, add the `DataverseMcp` entry under the existing `mcpServers` object instead of replacing the file.

1. Save the file.

You should now have a `~/.copilot/mcp-config.json` file that contains a `DataverseMcp` entry pointing at your own environment, with a `"tools"` array limited to `read_query`, `search`, and `describe`.

## Connect and enumerate the server's tools

Now connect GitHub Copilot CLI to the Dataverse MCP server, and confirm that only the allowlisted tools are available on this connection.

1. In your terminal, start GitHub Copilot CLI again:

    ```bash
    copilot
    ```

1. Confirm the server loaded by listing configured MCP servers:

    ```bash
    copilot mcp list
    ```

   You should see `DataverseMcp` listed as a configured server.

1. At the Copilot prompt, ask:

    ```input
    What tools does the DataverseMcp server expose on this connection, and what does each one do? Show the JSON definition for each tool.
    ```

   Copilot responds with exactly three tools — `read_query`, `search`, and `describe` — along with each tool's JSON input schema. Compare this list against the Dataverse MCP server's full tool list in the [Dataverse MCP server documentation](https://learn.microsoft.com/power-apps/maker/data-platform/data-platform-mcp){:target="_blank"} at `https://learn.microsoft.com/power-apps/maker/data-platform/data-platform-mcp`: the full server also supports tools such as `create_record`, `update_record`, and `delete_record`, none of which appear here.

You should see that the connection exposes only the three tools named in the allowlist, confirming that the restriction you configured is in effect.

## Retrieve the open replacement requests

With the connection confirmed, use the read-only `read_query` tool to retrieve the open equipment-replacement requests you reset earlier.

1. At the Copilot prompt, ask:

    ```input
    Using the DataverseMcp server, run a read_query against the eqp_request table for rows where the request status is Submitted or Approved. Return the request number, employee, and status for each row.
    ```

1. Review the response. Copilot CLI calls the `read_query` tool and returns the results as structured data.

    ```output
    [
      { "eqp_name": "REQ-SEED-001", "eqp_requeststatus": "Submitted", ... },
      { "eqp_name": "REQ-SEED-002", "eqp_requeststatus": "Submitted", ... },
      { "eqp_name": "REQ-SEED-003", "eqp_requeststatus": "Submitted", ... },
      { "eqp_name": "REQ-SEED-004", "eqp_requeststatus": "Approved", ... }
    ]
    ```

   The exact shape of the response depends on the columns Copilot includes in the query it builds, but the four request numbers should match `REQ-SEED-001` through `REQ-SEED-004`, the open requests you confirmed in **Before you start**.

> **Note**: Each tool call made through this connection is billed in Copilot Credits at a low per-response rate, unless your tenant holds a qualifying license. If the call fails because a rate or credit limit is reached, wait a few seconds and try again, or compare your results against the JSON shown above instead.

You should see the same four request numbers you confirmed after running Reset lab data, returned as JSON through the MCP connection rather than through the app's UI.

## Confirm the allowlist blocks a write

The Dataverse MCP server itself can create, update, and delete rows, but this connection can't, because those tools were never added to the allowlist. Prove it by asking GitHub Copilot CLI to do something only a write tool could do.

1. At the Copilot prompt, ask:

    ```input
    Using the DataverseMcp server, update REQ-SEED-004 to set its request status to Rejected.
    ```

1. Review Copilot's response. Instead of making the change, it reports that it has no tool available on this connection that can update a Dataverse record, because `update_record` isn't in the allowlist.

1. Confirm the request is unchanged by running a read-only check:

    ```input
    Using the DataverseMcp server, run a read_query against eqp_request for REQ-SEED-004 and show its current status.
    ```

   The status should still show **Approved**, unchanged from **Before you start**.

This is the hands-on version of the governance boundary this exercise is built around: the Dataverse MCP server can do far more than this connection allows, and the allowlist in `mcp-config.json` — not a permission check inside Dataverse itself — is what stops it here. Building or registering a different MCP server with a different tool surface is a separate, administrator-level task that this exercise doesn't cover.

You should see that the write request is refused, and that `REQ-SEED-004` still shows **Approved** when you read it back.

## Clean up

If you've finished exploring the Dataverse MCP server, you can remove the local configuration you added:

1. Open `~/.copilot/mcp-config.json` in a text editor.

1. Remove the `DataverseMcp` entry you added, or delete the file entirely if it contains nothing else.

1. Save the file.

Removing the local entry is optional. The environment-level **Allow MCP clients to interact with Dataverse MCP server** setting and the **Microsoft GitHub Copilot** client enablement you turned on are both safe to leave in place: they're reversible, they cost nothing until a client calls a tool, and another exercise later in this course also connects to the same environment's Dataverse MCP server.

## Summary

In this exercise, you enabled the Dataverse MCP server for your environment, allowed the GitHub Copilot client to connect to it, and installed GitHub Copilot CLI as an MCP client. You configured a supplied read-only tool allowlist, connected to the server, and confirmed that only the allowlisted tools were exposed. You then retrieved open equipment-replacement requests through a `read_query` call, and confirmed that a write attempt through the same connection was refused because no write tool was ever exposed.

You have successfully completed this exercise.
