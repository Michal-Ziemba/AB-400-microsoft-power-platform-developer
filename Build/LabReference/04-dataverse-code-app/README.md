# Lab 04 reference - Build a Dataverse equipment request code app

This is the **completed reference** for `Instructions/Labs/04-dataverse-code-app.md`,
matching the design in `lab-plan/04-dataverse-code-app-design.md`.

## What's here

- `src/App.tsx`, `src/components/RequestForm.tsx`, `src/components/FulfilmentContact.tsx` -
  the **hand-written** application code a learner produces by following the exercise:
  the request form, the eligibility gate, and the environment-variable read.
- `src/generated/` - **typed stubs only**, not real Power Apps CLI output (see below).
- Everything else (`package.json`, `vite.config.ts`, `tsconfig*.json`, `index.html`,
  `src/main.tsx`, `src/index.css`, `src/App.css`) is identical to the starter in
  `Allfiles/04-dataverse-code-app/`.

## Why `src/generated/` is a stub, not the real output

The real `src/generated/models/` and `src/generated/services/` files are produced by
running `pa app add data-source` and `pa app add dataverse-api` against a live,
code-apps-enabled Dataverse environment that has the base solution
(`EquipmentProvisioningBase_managed.zip`) imported. Producing them requires signing in
to that environment, which this reference build must not do.

Instead, `src/generated/` contains small stub files - clearly marked with a comment -
that mirror the documented shape of generated services (`IOperationResult<T>`,
`IGetAllOptions`, a `getAll`/`create` method per table, one method per custom API) and
return hard-coded sample data. They exist only so the hand-written files above type-check
and `npm run build` succeeds without a Dataverse connection; **they are not what the CLI
generates** and must not be copied into a real project.

The request-create stub uses numeric choice literals and optional `statecode: 0 | 1`.
The component supplies `statecode: 0 as const` to preserve the Active literal type,
matching the current generated model reported during learner testing.

To turn this reference into a working app, delete `src/generated/` and run the same
`pa app add data-source` / `pa app add dataverse-api` commands used in the exercise,
against your own environment. `src/App.tsx` and `src/components/` need no changes.

## Verified

`npm install` and `npm run build` both succeed in this folder without any sign-in or
network call to a Power Platform environment.
