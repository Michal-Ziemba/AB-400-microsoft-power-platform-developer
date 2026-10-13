import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';

// This describes just the columns this form needs. Once you add the Device
// table as a data source (see "Add Dataverse tables as data sources"), you
// can replace it with the generated model from src/generated/models instead.
interface DeviceRow {
  eqp_deviceid: string;
  eqp_name: string;
  eqp_devicetype: number;
}

const DEVICE_TYPE_LABELS: Record<number, string> = {
  100000000: 'Laptop',
  100000001: 'Monitor',
  100000002: 'Phone',
  100000003: 'Tablet',
};

export function RequestForm() {
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [devices, setDevices] = useState<DeviceRow[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState('');
  const [justification, setJustification] = useState('');
  const [status, setStatus] = useState<string | null>(
    'Dataverse tables are not connected yet.',
  );
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    loadEmployeeAndDevices();
  }, []);

  async function loadEmployeeAndDevices() {
    // Load the employee and device data sources
    // Find your own Employee row by employee number (EMP-001), then the
    // devices assigned to that employee. Use the generated Employees and
    // Devices services added in "Add Dataverse tables as data sources".
    setEmployeeId(null);
    setDevices([]);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!employeeId || !selectedDeviceId || !justification.trim()) {
      setStatus('Choose a device and enter a justification.');
      return;
    }

    const selectedDevice = devices.find((d) => d.eqp_deviceid === selectedDeviceId);
    if (!selectedDevice) {
      return;
    }

    setSubmitting(true);
    setStatus(null);

    try {
      let isEligible = false;

      // Call the eligibility custom API
      // Call eqp_IsEligibleForReplacement with the selected device's ID and
      // read the IsEligible value from the response. See "Call the
      // eligibility custom API and gate the request form".

      if (!isEligible) {
        setStatus(
          `${selectedDevice.eqp_name} isn't eligible for replacement yet.`,
        );
        return;
      }

      // Create the replacement request
      // Create an eqp_request row for the employee and the selected device
      // using the generated Requests service. See "Add Dataverse tables as
      // data sources".
      setStatus('Request creation is not connected yet.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="request-form" onSubmit={handleSubmit}>
      <h2>Request a replacement device</h2>

      <label htmlFor="device">Device to replace</label>
      <select
        id="device"
        value={selectedDeviceId}
        onChange={(event) => setSelectedDeviceId(event.target.value)}
      >
        <option value="">Select a device</option>
        {devices.map((device) => (
          <option key={device.eqp_deviceid} value={device.eqp_deviceid}>
            {device.eqp_name} ({DEVICE_TYPE_LABELS[device.eqp_devicetype] ?? 'Device'})
          </option>
        ))}
      </select>

      <label htmlFor="justification">Justification</label>
      <textarea
        id="justification"
        rows={4}
        placeholder="Why do you need a replacement?"
        value={justification}
        onChange={(event) => setJustification(event.target.value)}
      />

      <button type="submit" disabled={submitting}>
        {submitting ? 'Submitting...' : 'Submit request'}
      </button>

      {status && <p className="status-message">{status}</p>}
    </form>
  );
}
