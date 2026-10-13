// Local type stub for offline verification only - see ../types.ts.
import type { IOperationResult, IGetAllOptions } from '../types';
import type { Devices } from '../models/Eqp_devicesModel';

export const Eqp_devicesService = {
  async getAll(_options?: IGetAllOptions): Promise<IOperationResult<Devices[]>> {
    return { success: true, data: [] };
  },
};
