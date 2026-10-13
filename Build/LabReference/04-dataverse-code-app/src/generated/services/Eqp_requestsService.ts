// Local type stub for offline verification only - see ../types.ts.
//
// The real generated create() input requires the model's own @odata.bind
// property names, capitalized exactly as Dataverse's schema names them
// (eqp_Employee, eqp_CurrentDevice) - a different casing from the table's
// logical name (eqp_employee, eqp_currentdevice). It also requires
// statecode, even though Dataverse defaults it on create.
import type { IOperationResult } from '../types';
import type { Requests } from '../models/Eqp_requestsModel';

export interface RequestsCreateInput {
  eqp_justification?: string;
  eqp_requesteddevicetype?: string;
  statecode: string;
  'eqp_Employee@odata.bind': string;
  'eqp_CurrentDevice@odata.bind': string;
}

export const Eqp_requestsService = {
  async create(_record: RequestsCreateInput): Promise<IOperationResult<Requests>> {
    return {
      success: true,
      data: { eqp_requestid: '00000000-0000-0000-0000-000000000000', eqp_name: 'REQ-00000' },
    };
  },
};
