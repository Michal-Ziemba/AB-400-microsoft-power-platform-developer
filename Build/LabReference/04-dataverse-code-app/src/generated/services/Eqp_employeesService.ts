// Local type stub for offline verification only - see ../types.ts.
import type { IOperationResult, IGetAllOptions } from '../types';
import type { Employees } from '../models/Eqp_employeesModel';

export const Eqp_employeesService = {
  async getAll(_options?: IGetAllOptions): Promise<IOperationResult<Employees[]>> {
    return { success: true, data: [] };
  },
};
