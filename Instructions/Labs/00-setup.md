---
lab:
    title: 'Set up your lab environment'
    description: 'Create a Power Platform developer environment, turn on code apps, install the developer tools, sign in to GitHub and GitHub Copilot, and import the base solution used by every lab in this course.'
    level: 200
    duration: 30
    islab: false
    status: 'in-development'
    targetDate: '2026-10-16'
---

# Set up your lab environment

Every lab in this course runs in the same Power Platform developer environment and reads the same equipment-provisioning data. In this guide, you set that environment up once: you create or select a developer environment, turn on code apps, install the tools the labs use, sign in to GitHub and GitHub Copilot, get the lab files, and import the base solution and sample data. Each lab's first task then confirms this starting point and continues from there, so complete this guide before you start lab 01.

This exercise should take approximately **30** minutes to complete.

## Create a developer environment

Every lab needs a Power Platform environment of type **Developer**, with a Dataverse database. You only need one for the whole course.

1. If you already have access to a Developer-type environment for this course and you're an admin on it, confirm its name and skip ahead to the verification step at the end of this task.
1. If you don't yet have access to any Power Platform environment, sign up for the free [Power Apps Developer Plan](https://aka.ms/PowerAppsDevPlan){:target="_blank"} at `https://aka.ms/PowerAppsDevPlan`. The Developer Plan provisions a Developer-type environment for you automatically, and supports up to three developer environments.
1. If you already work in a Power Platform tenant and can create environments there, sign in to the [Power Platform admin center](https://admin.powerplatform.microsoft.com){:target="_blank"} at `https://admin.powerplatform.microsoft.com`, select **Environments**, and then select **New**.
1. Enter a name, such as `AB-400 labs`, choose a region, and set **Type** to **Developer**.
1. Set **Add a Dataverse data store** to **Yes**, and then select **Next**.
1. Choose a language and currency, and then select **Save**. Wait a few minutes while the environment is provisioned.

> **Important**: Don't turn this environment into a Managed Environment. Managed Environments need extra licensing for the apps and flows the labs build, and that's not reversible.

Open the environment list and confirm your environment appears with **Type** set to **Developer** and status **Ready**. You'll select this same environment throughout the rest of this guide and in every lab.

## Turn on code apps

Two labs in this course build Power Apps code apps, which need this environment setting turned on. It's a reversible setting, so turning it on now has no side effects on the other labs.

1. In the [Power Platform admin center](https://admin.powerplatform.microsoft.com){:target="_blank"} at `https://admin.powerplatform.microsoft.com`, select **Environments**, and then select the environment you created or selected in the previous task.
1. Select **Settings**.
1. Expand **Product**, and then select **Features**.
1. Find **Power Apps code apps**, and set the **Enable code apps** toggle to **On**.
1. Select **Save**.

Return to **Features** and confirm **Enable code apps** still shows **On**.

## Install the tools

Install the tools in the following table before you start the labs. Each lab's introduction names only the tools that specific lab uses, so you won't need all of these for any single lab.

| Tool | Labs that use it | Get it |
|:--|:--|:--|
| Visual Studio 2022 with the **.NET desktop development** workload (Windows only) | Lab 01 | [Visual Studio downloads](https://visualstudio.microsoft.com/downloads/){:target="_blank"} at `https://visualstudio.microsoft.com/downloads/` |
| .NET SDK 8 | Labs 02, 03, 07, and to install `pac` | [.NET 8 downloads](https://dotnet.microsoft.com/download/dotnet/8.0){:target="_blank"} at `https://dotnet.microsoft.com/download/dotnet/8.0` |
| Plug-in Registration Tool | Lab 01 | Installed with the Power Platform CLI command `pac tool prt` (see the `pac` row below) |
| Node.js LTS (version 22 or later) | Labs 04, 05, 07, 09 | [Node.js downloads](https://nodejs.org/){:target="_blank"} at `https://nodejs.org/` |
| Power Apps CLI (`pa`) | Labs 04, 05 | `npm install --global @microsoft/power-apps-cli` |
| Power Platform CLI (`pac`) | Labs 01, 07 | `dotnet tool install --global Microsoft.PowerApps.CLI.Tool` |
| Python 3.12 | Labs 02, 10 | [Python downloads](https://www.python.org/downloads/){:target="_blank"} at `https://www.python.org/downloads/` |
| Azure CLI | Labs 01, 03, 10 | [Install the Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli){:target="_blank"} at `https://learn.microsoft.com/cli/azure/install-azure-cli` |
| Azure Functions Core Tools v4 | Lab 03 | `npm install -g azure-functions-core-tools@4 --unsafe-perm true` |
| GitHub Copilot CLI | Lab 09 | `npm install -g @github/copilot` |
| Visual Studio Code with the GitHub Copilot extension | Every lab | [Visual Studio Code downloads](https://code.visualstudio.com/download){:target="_blank"} at `https://code.visualstudio.com/download` |
| Git | Every lab, to get the lab files | [Git downloads](https://git-scm.com/downloads){:target="_blank"} at `https://git-scm.com/downloads` |
| A Chromium-based browser with developer tools (for example, Microsoft Edge) | Several labs, to inspect network requests and debug code apps | Likely already on your computer |

1. Install the tools from the table that you don't already have.
1. Open a terminal and confirm the command-line tools respond:

    ```bash
    dotnet --version
    pac --version
    pa --version
    func --version
    az --version
    node --version
    git --version
    python --version
    copilot --version
    ```

Each command should print a version number rather than an error. If one doesn't, reopen your terminal first — some installers only update your terminal's path the next time you open it.

## Sign in to GitHub and GitHub Copilot

Several labs in this course use GitHub Copilot Chat or agent mode. Sign in once, here, so every lab can use it.

1. Sign in to [GitHub](https://github.com){:target="_blank"} at `https://github.com` using a personal, non-managed GitHub account. An Enterprise Managed User account or an account that your organization assigned a Copilot seat through can't use GitHub Copilot Free.
1. Go to your [GitHub Copilot settings](https://github.com/settings/copilot){:target="_blank"} at `https://github.com/settings/copilot`, and confirm your plan shows **Copilot Free** or higher.
1. In Visual Studio Code, select the **GitHub Copilot Chat** extension, and sign in with the same account.
1. Open Copilot Chat and send a test prompt, such as `Hello`, to confirm you get a response.

> **Note**: If GitHub Copilot Chat or agent mode stops responding partway through a lab, you've likely reached your monthly GitHub Copilot Free usage allowance. Wait until it resets on the first of next month, upgrade to GitHub Copilot Pro, or — if you access GitHub through a work or organization account — ask your organization's GitHub administrator about a Copilot Business or Copilot Enterprise seat.

## Get the lab files

Every lab in this course starts from files in this repository. Get a local copy once, here, and reuse it for every lab.

1. Open a terminal, and clone the [AB-400 labs repository](https://github.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer.git){:target="_blank"} at `https://github.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer.git`:

    ```bash
    git clone https://github.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer.git
    ```

1. Each lab names the folder it uses inside the `Allfiles` folder of your cloned copy, for example `Allfiles/01-equipment-validation-plugin`.
1. If you'd rather not clone the whole repository, each lab also links to its individual files by their raw download URL, so you can download only what that lab needs instead.

Confirm your cloned `AB-400-microsoft-power-platform-developer` folder contains both an `Allfiles` folder and an `Instructions` folder.

## Import the base solution

Every Dataverse lab in this course reads and writes the same four tables: Employee, Device, Request, and Allowance. Import the solution that creates them once, here, so each lab can confirm it's there and start from the same sample data.

1. Download [EquipmentProvisioningBase_managed.zip](https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/shared/EquipmentProvisioningBase_managed.zip){:target="_blank"} from `https://raw.githubusercontent.com/MicrosoftLearning/AB-400-microsoft-power-platform-developer/main/Allfiles/shared/EquipmentProvisioningBase_managed.zip`.
1. Sign in to the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`.
1. Confirm the environment selector in the top right shows the developer environment you set up earlier in this guide. Solutions import into the selected environment, and importing into the wrong one is the easiest mistake to make at this step.
1. Select **Solutions**, and then select **Import solution**.
1. Select **Browse**, choose the file you downloaded, and select **Next**.
1. Select **Import**, and wait about two minutes for the import to complete.
1. When the import finishes, select **Solutions** again and confirm **Equipment Provisioning (base)** appears in the list.

> **Note**: You only need to do this once per environment. If a later version of the base solution is already installed, the import is refused — if that happens, you can skip this step and move on to loading the sample data.

## Load the sample data

The base solution includes the **Equipment Provisioning** app and a repeatable way to load sample data into it.

1. In the [Power Apps maker portal](https://make.powerapps.com){:target="_blank"} at `https://make.powerapps.com`, with your developer environment selected, select **Apps**, and then open **Equipment Provisioning**.
1. In the app's site map, select **Requests**.
1. On the command bar, select **Reset lab data**.
1. Read the dialog that appears — it summarizes what it's about to create or restore — and then select **Refresh** to see the result once it finishes.
1. Confirm the **Requests** view lists six requests numbered `REQ-SEED-001` through `REQ-SEED-006`.

> **Note**: Every lab in this course runs **Reset lab data** again at its own start, so you always begin from the same known state, even if a previous lab left changes behind. It never deletes a row; it only restores seed rows that have drifted from their starting values.

## Summary

You now have a Power Platform developer environment with code apps turned on, the developer tools this course uses, GitHub and GitHub Copilot signed in, a local copy of the lab files, and the base solution and sample data every lab depends on.

Keep this environment for the rest of the course — don't delete it between labs. You're ready to start with lab 01.

## Troubleshooting

| Issue | Cause | Solution |
|:--|:--|:--|
| A `pa` command (`pa app add data-source`, `pa app find-dataverse-api`, `pa app add dataverse-api`, and similar) fails with a generic `fetch failed` or `Network request failed … Connection timed out` error | A transient connection problem between your computer and the Power Platform service, unrelated to your environment or the exercise steps. The CLI makes a single attempt and doesn't retry on its own. | Run the exact same command again. In testing, failures like this cleared within a few attempts; they don't indicate a misconfigured environment or a mistake in the exercise. |
