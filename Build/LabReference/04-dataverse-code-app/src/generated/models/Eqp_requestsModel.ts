// Local type stub for offline verification only - see src/generated/types.ts.
export interface Requests {
  eqp_requestid: string;
  // Autonumbered request number Dataverse assigns on create, e.g. REQ-00007.
  eqp_name?: string;
  eqp_justification?: string;
  eqp_requesteddevicetype?: 100000000 | 100000001 | 100000002 | 100000003;
  statecode?: 0 | 1;
}
