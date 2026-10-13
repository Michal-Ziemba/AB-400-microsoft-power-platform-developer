import { useEffect, useState } from 'react';
import { RetrieveEnvironmentVariableValueService } from '../generated/services/RetrieveEnvironmentVariableValueService';

export function FulfilmentContact() {
  const [contact, setContact] = useState<string | null>(null);

  useEffect(() => {
    loadFulfilmentContact();
  }, []);

  async function loadFulfilmentContact() {
    // Read the fulfilment contact environment variable
    // This always re-reads the live value - never assume the published
    // default, because a previous run or another learner may already have
    // set a current value.
    const result = await RetrieveEnvironmentVariableValueService.RetrieveEnvironmentVariableValue(
      'eqp_FulfillmentContact',
    );
    const value = result.data?.Value;
    setContact(typeof value === 'string' ? value : null);
  }

  return (
    <aside className="fulfilment-contact">
      <h3>Questions about fulfilment?</h3>
      <p>{contact ?? 'Not loaded yet.'}</p>
    </aside>
  );
}
