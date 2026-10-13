// Local type stub for offline verification only.
//
// This is NOT a real Power Apps CLI-generated file. The actual file is
// produced by `pa app add data-source` / `pa app add dataverse-api` when
// run against a live, code-apps-enabled Dataverse environment. This stub
// mirrors the shape documented for generated services
// (https://learn.microsoft.com/power-apps/developer/code-apps/how-to/connect-to-dataverse)
// so this reference project's TypeScript compiles without network access.
export interface IOperationResult<T> {
  success: boolean;
  data?: T;
  error?: { message: string };
}

export interface IGetAllOptions {
  maxPageSize?: number;
  select?: string[];
  filter?: string;
  orderBy?: string[];
  top?: number;
  skip?: number;
  skipToken?: string;
}
