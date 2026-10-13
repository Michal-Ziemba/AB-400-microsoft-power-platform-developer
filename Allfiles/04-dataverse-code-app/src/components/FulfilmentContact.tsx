import { useEffect, useState } from 'react';

export function FulfilmentContact() {
  const [contact, setContact] = useState<string | null>(null);

  useEffect(() => {
    loadFulfilmentContact();
  }, []);

  async function loadFulfilmentContact() {
    // Read the fulfilment contact environment variable
    // Call RetrieveEnvironmentVariableValue for eqp_FulfillmentContact and
    // set the result with setContact below. See "Read an environment
    // variable from the code app".
    setContact(null);
  }

  return (
    <aside className="fulfilment-contact">
      <h3>Questions about fulfilment?</h3>
      <p>{contact ?? 'Not loaded yet.'}</p>
    </aside>
  );
}
