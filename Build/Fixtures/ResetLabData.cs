using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace EquipmentProvisioning.Fixtures
{
    // Main operation of the eqp_ResetLabData custom API (F3). Restores the seed rows from the
    // foundation spec §2 by their alternate keys, with every date relative to today, and links
    // EMP-001 to the caller. Writes only the columns that differ from the baseline, so a reset with
    // nothing to restore doesn't fire flows or plug-ins left registered by other labs.
    // Never deletes rows and never touches rows without a seed key.
    // Output: Summary (String). Runs as the caller, so it fails cleanly for users who can't write the tables.
    public sealed class ResetLabData : IPlugin
    {
        private const int Laptop = 100000000, Monitor = 100000001, Phone = 100000002, Tablet = 100000003;
        private const int InStock = 100000000, Issued = 100000001;
        private const int Submitted = 100000000, Approved = 100000001, Rejected = 100000002, Fulfilled = 100000004;
        private const int NotChecked = 100000000, Available = 100000001;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);
            var today = DateTime.UtcNow.Date;
            var run = new RestoreRun(service);

            // Employees. EMP-005's user link is left alone so a lab can link it to a second user.
            run.Restore(Employee("EMP-001", "Avery Chen", "IT", new EntityReference("systemuser", context.UserId)));
            run.Restore(Employee("EMP-002", "Jordan Patel", "Finance", null));
            run.Restore(Employee("EMP-003", "Sam Rivera", "Sales", null));
            run.Restore(Employee("EMP-004", "Taylor Brooks", "Operations", null));
            var emp5 = Employee("EMP-005", "Morgan Diaz", "Marketing", null);
            emp5.Attributes.Remove("eqp_user");
            run.Restore(emp5);

            // Devices
            run.Restore(Device("LAP-1001", Laptop, "Contoso Book 14", Issued, "EMP-001", today.AddMonths(-8), false, 4));
            run.Restore(Device("PHN-1002", Phone, "Contoso Phone 12", Issued, "EMP-001", today.AddMonths(-26), false, 3));
            run.Restore(Device("LAP-1003", Laptop, "Contoso Book 14", Issued, "EMP-002", today.AddMonths(-8), true, 1));
            run.Restore(Device("LAP-1004", Laptop, "Contoso Book 13", Issued, "EMP-003", today.AddMonths(-30), false, 2));
            run.Restore(Device("MON-1005", Monitor, "Contoso View 27", Issued, "EMP-004", today.AddMonths(-24), false, null));
            run.Restore(Device("TAB-1006", Tablet, "Contoso Tab 11", Issued, "EMP-004", today.AddMonths(-23), false, 5));
            run.Restore(Device("LAP-2001", Laptop, "Contoso Book 14", InStock, null, null, false, 5));
            run.Restore(Device("LAP-2002", Laptop, "Contoso Book 14", InStock, null, null, false, 5));
            run.Restore(Device("MON-2003", Monitor, "Contoso View 27", InStock, null, null, false, 5));

            // Requests
            run.Restore(Request("REQ-SEED-001", "EMP-001", "LAP-1001", Laptop, Submitted, null, NotChecked, null,
                "Battery no longer lasts a full working day."));
            run.Restore(Request("REQ-SEED-002", "EMP-002", "LAP-1003", Laptop, Submitted, null, NotChecked, null,
                "Screen cracked after a fall; the device is damaged beyond repair."));
            run.Restore(Request("REQ-SEED-003", "EMP-003", "LAP-1004", Laptop, Submitted, null, NotChecked, null,
                "Laptop is over two years old and slow to start."));
            run.Restore(Request("REQ-SEED-004", "EMP-004", "MON-1005", Monitor, Approved, today.AddDays(-2), NotChecked, null,
                "Monitor flickers intermittently."));
            run.Restore(Request("REQ-SEED-005", "EMP-004", "TAB-1006", Tablet, Rejected, null, NotChecked, null,
                "Would like a newer model."));
            run.Restore(Request("REQ-SEED-006", "EMP-001", "PHN-1002", Phone, Fulfilled, today.AddDays(-30), Available, today.AddDays(-29),
                "Phone no longer receives security updates."));

            // Allowances: period from six months ago to six months ahead
            var start = today.AddMonths(-6);
            var end = today.AddMonths(6);
            run.Restore(Allowance("EMP-001-CURRENT", "EMP-001", start, end, 2, 1, 1, today.AddDays(-1)));
            run.Restore(Allowance("EMP-002-CURRENT", "EMP-002", start, end, 2, 2, 0, today.AddDays(-3))); // planted stale row
            run.Restore(Allowance("EMP-003-CURRENT", "EMP-003", start, end, 1, 0, 1, today.AddDays(-1)));
            run.Restore(Allowance("EMP-004-CURRENT", "EMP-004", start, end, 2, 0, 2, today.AddDays(-1)));

            context.OutputParameters["Summary"] =
                $"Reset 5 employees, 9 devices, 6 requests and 4 allowances for {today:yyyy-MM-dd}: " +
                $"{run.Created} created, {run.Restored} restored, {run.Unchanged} already correct. EMP-001 is linked to you.";
        }

        // Per-execution state; plug-in instances are shared, so this can't live in fields of ResetLabData.
        private sealed class RestoreRun
        {
            private readonly IOrganizationService _service;
            private readonly Dictionary<string, Guid> _ids = new Dictionary<string, Guid>();

            public RestoreRun(IOrganizationService service) => _service = service;

            public int Created { get; private set; }
            public int Restored { get; private set; }
            public int Unchanged { get; private set; }

            public void Restore(Entity desired)
            {
                var key = desired.KeyAttributes.First();
                ResolveLookups(desired);

                Entity existing = null;
                try
                {
                    existing = ((RetrieveResponse)_service.Execute(new RetrieveRequest
                    {
                        Target = new EntityReference(desired.LogicalName, key.Key, key.Value),
                        ColumnSet = new ColumnSet(desired.Attributes.Keys.ToArray()),
                    })).Entity;
                }
                catch (FaultException<OrganizationServiceFault>)
                {
                    // Not there yet: create it below.
                }

                if (existing == null)
                {
                    var created = (UpsertResponse)_service.Execute(new UpsertRequest { Target = desired });
                    _ids[$"{desired.LogicalName}|{key.Value}"] = created.Target.Id;
                    Created++;
                    return;
                }

                _ids[$"{desired.LogicalName}|{key.Value}"] = existing.Id;
                var changes = new Entity(desired.LogicalName, existing.Id);
                foreach (var attribute in desired.Attributes)
                {
                    existing.Attributes.TryGetValue(attribute.Key, out var current);
                    if (!Same(current, attribute.Value))
                    {
                        changes[attribute.Key] = attribute.Value;
                    }
                }
                if (changes.Attributes.Count == 0)
                {
                    Unchanged++;
                    return;
                }
                _service.Update(changes);
                Restored++;
            }

            // Lookups to seed rows are written by alternate key; swap in the IDs of rows restored earlier.
            private void ResolveLookups(Entity desired)
            {
                foreach (var attribute in desired.Attributes.ToList())
                {
                    if (attribute.Value is EntityReference reference && reference.Id == Guid.Empty && reference.KeyAttributes.Count > 0)
                    {
                        desired[attribute.Key] = new EntityReference(reference.LogicalName,
                            _ids[$"{reference.LogicalName}|{reference.KeyAttributes.First().Value}"]);
                    }
                }
            }

            private static bool Same(object current, object desired)
            {
                if (current == null || desired == null)
                {
                    return current == null && desired == null;
                }
                switch (desired)
                {
                    case OptionSetValue option:
                        return current is OptionSetValue o && o.Value == option.Value;
                    case EntityReference reference:
                        return current is EntityReference r && r.Id == reference.Id;
                    case DateTime date:
                        return current is DateTime d && Utc(d) == Utc(date);
                    default:
                        return Equals(current, desired);
                }
            }

            // Date-only values can come back with an unspecified kind; treat anything not local as UTC.
            private static DateTime Utc(DateTime value) =>
                value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static EntityReference EmployeeRef(string number) =>
            new EntityReference("eqp_employee", "eqp_employeenumber", number);

        private static EntityReference DeviceRef(string assetTag) =>
            new EntityReference("eqp_device", "eqp_name", assetTag);

        private static Entity Employee(string number, string name, string department, EntityReference user)
        {
            var entity = new Entity("eqp_employee", "eqp_employeenumber", number)
            {
                ["eqp_name"] = name,
                ["eqp_email"] = name.ToLowerInvariant().Replace(' ', '.') + "@example.com",
                ["eqp_department"] = department,
                ["eqp_user"] = user,
            };
            return entity;
        }

        private static Entity Device(string assetTag, int type, string model, int status, string assignedTo,
            DateTime? issuedOn, bool damaged, int? conditionRating) =>
            new Entity("eqp_device", "eqp_name", assetTag)
            {
                ["eqp_devicetype"] = new OptionSetValue(type),
                ["eqp_model"] = model,
                ["eqp_devicestatus"] = new OptionSetValue(status),
                ["eqp_assignedto"] = assignedTo == null ? null : EmployeeRef(assignedTo),
                ["eqp_issuedon"] = issuedOn,
                ["eqp_damagedbeyondrepair"] = damaged,
                ["eqp_conditionrating"] = conditionRating,
            };

        private static Entity Request(string number, string employee, string device, int requestedType, int status,
            DateTime? approvedOn, int availability, DateTime? availabilityCheckedOn, string justification) =>
            new Entity("eqp_request", "eqp_name", number)
            {
                ["eqp_employee"] = EmployeeRef(employee),
                ["eqp_currentdevice"] = DeviceRef(device),
                ["eqp_requesteddevicetype"] = new OptionSetValue(requestedType),
                ["eqp_requeststatus"] = new OptionSetValue(status),
                ["eqp_approvedon"] = approvedOn,
                ["eqp_availability"] = new OptionSetValue(availability),
                ["eqp_availabilitycheckedon"] = availabilityCheckedOn,
                ["eqp_processingnote"] = null,
                ["eqp_justification"] = justification,
            };

        private static Entity Allowance(string name, string employee, DateTime start, DateTime end,
            int allowed, int used, int remaining, DateTime lastRecalculated) =>
            new Entity("eqp_allowance", "eqp_name", name)
            {
                ["eqp_employee"] = EmployeeRef(employee),
                ["eqp_periodstart"] = start,
                ["eqp_periodend"] = end,
                ["eqp_devicesallowed"] = allowed,
                ["eqp_devicesused"] = used,
                ["eqp_remaining"] = remaining,
                ["eqp_lastrecalculatedon"] = lastRecalculated,
            };
    }
}
