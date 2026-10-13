import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Eqp_employeesService } from '../generated/services/Eqp_employeesService';
import { Eqp_devicesService } from '../generated/services/Eqp_devicesService';
import { Eqp_requestsService } from '../generated/services/Eqp_requestsService';
import { Eqp_IsEligibleForReplacementService } from '../generated/services/Eqp_IsEligibleForReplacementService';

// This describes just the columns this form needs from the generated
// Devices model, normalized below so this component doesn't depend on
// exactly which columns the generated model marks as optional.
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
    // Find your own Employee row by employee number (plain column, not the
    // eqp_key_employeenumber alternate key - code apps don't support
    // alternate keys).
    const employeeResult = await Eqp_employeesService.getAll({
      select: ['eqp_employeeid'],
      filter: "eqp_employeenumber eq 'EMP-001'",
      top: 1,
    });

    const myEmployee = employeeResult.data?.[0];
    if (!myEmployee) {
      setStatus('Could not find your Employee row (EMP-001). Run Reset lab data and try again.');
      return;
    }

    setEmployeeId(myEmployee.eqp_employeeid);

    // Devices assigned to that employee, filtered on the lookup's plain
    // _eqp_assignedto_value column.
    const deviceResult = await Eqp_devicesService.getAll({
      select: ['eqp_deviceid', 'eqp_name', 'eqp_devicetype'],
      filter: `_eqp_assignedto_value eq ${myEmployee.eqp_employeeid}`,
    });

    setDevices(
      (deviceResult.data ?? []).map((device) => ({
        eqp_deviceid: device.eqp_deviceid,
        eqp_name: device.eqp_name ?? '',
        eqp_devicetype: Number(device.eqp_devicetype ?? 0),
      })),
    );
    setStatus(null);
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
      // Call the eligibility custom API
      const eligibility = await Eqp_IsEligibleForReplacementService.eqp_IsEligibleForReplacement(
        selectedDeviceId,
      );
      const isEligible = Boolean(eligibility.data?.IsEligible);

      if (!isEligible) {
        // This is a client-side convenience only. Dataverse itself still
        // allows the create - the server-side rule runs at approval, not
        // creation - so this gate never claims to be a security boundary.
        setStatus(`${selectedDevice.eqp_name} isn't eligible for replacement yet.`);
        return;
      }

      // Create the replacement request
      //
      // The @odata.bind property names match the generated model exactly,
      // including capitalization (eqp_Employee, eqp_CurrentDevice) - a
      // different casing from the table's own logical name (eqp_employee,
      // eqp_currentdevice). eqp_requesteddevicetype is sent as a string, and
      // statecode is required by the generated type even though Dataverse
      // defaults it on create; '0' is Active for every table in this
      // solution.
      const newRequest = {
        eqp_justification: justification,
        eqp_requesteddevicetype: String(selectedDevice.eqp_devicetype),
        statecode: '0',
        'eqp_Employee@odata.bind': `/eqp_employees(${employeeId})`,
        'eqp_CurrentDevice@odata.bind': `/eqp_devices(${selectedDeviceId})`,
      };

      const result = await Eqp_requestsService.create(newRequest);

      if (result.data) {
        setStatus(`Request ${result.data.eqp_name ?? ''} submitted for ${selectedDevice.eqp_name}.`);
        setJustification('');
        setSelectedDeviceId('');
      } else {
        setStatus('Failed to create the request.');
      }
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
