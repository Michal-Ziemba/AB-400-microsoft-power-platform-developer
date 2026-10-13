# AB-400: Extend Microsoft Power Platform solutions with code and AI

This repository contains lab exercises and supporting files for
[AB-400](https://learn.microsoft.com/training/courses/ab-400t00).
The published instructions are at
https://microsoftlearning.github.io/AB-400-microsoft-power-platform-developer/.

The exercises may be used in both self-paced skilling experiences on [Microsoft Learn](https://learn.microsoft.com) and in Microsoft authorized instructor-led training.

| Folder | Contents |
|:--|:--|
| `Instructions/Labs/` | Setup instructions and lab exercises |
| `Allfiles/` | The shared base solution and each lab's starter files |
| `Build/` | Source, build tooling, and completed reference versions |

## Build and verify the supplied solutions

Run the build commands from `Build/EqpBuilder`. The base `export` command keeps the
full custom-table definitions needed for a clean install. If lab 01 is present in
the authoring environment, it uses `pac solution unpack` and `pac solution pack` to
remove lab 01's forms from the exported base source. The lab-specific solution and
its forms remain unchanged in Dataverse. The exporter rejects packages that depend
on missing `eqp_` components.

Test each new base package by importing it into a clean Dataverse environment
before you import any lab-specific solution.

Install the Power Platform CLI (`pac`) before you export from an environment that
also contains lab 01. If the working CLI executable isn't on `PATH`, set
`EQP_PAC_EXE` to its full path.

By default, the builder uses the isolated Azure CLI profile under
`%LOCALAPPDATA%\AB400\azcli`. To test another tenant without changing that profile,
set `EQP_AZURE_CONFIG_DIR` to the existing Azure CLI configuration directory signed
in to the target tenant:

```powershell
$env:EQP_AZURE_CONFIG_DIR = Join-Path $HOME ".azure"
dotnet run -- verify --url "<environment-url>" --user "<sign-in-name>"
Remove-Item Env:EQP_AZURE_CONFIG_DIR
```

Confirm the selected profile belongs to the intended tenant before you run a build
or import command. Don't copy access tokens or credentials into the repository.

## Information for MCTs

**Are you an MCT?** - Have a look at our [GitHub User Guide for MCTs](https://microsoftlearning.github.io/MCT-User-Guide/)

Any MCT (Microsoft Certified Trainer) can submit a pull request to the code or content in the GitHub repro. Microsoft and the course author will then triage and include content and lab code changes as needed. You can submit bugs, changes, improvement, and ideas. Find a new Azure or Microsoft 365 feature before we have? Submit a new demo!
