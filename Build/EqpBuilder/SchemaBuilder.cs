using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EqpBuilder;

// Creates the F1 schema defined in lab-plan\00-foundation-spec.md §1. Every step checks first and
// creates only what's missing, so the command can be rerun safely.
internal sealed class SchemaBuilder(ServiceClient client)
{
    public const string PublisherName = "EquipmentProvisioningLabs";
    public const string SolutionName = "EquipmentProvisioningBase";
    private const int Lcid = 1033;

    private static Label L(string text) => new(text, Lcid);
    private static AttributeRequiredLevelManagedProperty Required => new(AttributeRequiredLevel.ApplicationRequired);
    private static AttributeRequiredLevelManagedProperty Optional => new(AttributeRequiredLevel.None);

    public void Run()
    {
        EnsurePublisherAndSolution();
        EnsureGlobalChoice();
        EnsureTables();
        EnsureColumns();
        EnsureRelationships();
        EnsureKeys();
        EnsureEnvironmentVariables();
        Log("publish", "all customizations");
        client.Execute(new PublishAllXmlRequest());
        Log("done", "schema is in place (alternate key indexes may take a few minutes to activate)");
    }

    private void EnsurePublisherAndSolution()
    {
        var publisher = FindOne("publisher", "uniquename", PublisherName);
        Guid publisherId;
        if (publisher is null)
        {
            publisherId = client.Create(new Entity("publisher")
            {
                ["uniquename"] = PublisherName,
                ["friendlyname"] = "Equipment Provisioning Labs",
                ["customizationprefix"] = "eqp",
                ["customizationoptionvalueprefix"] = 10000,
                ["description"] = "Publisher for the AB-400 lab solutions.",
            });
            Log("create", $"publisher {PublisherName}");
        }
        else
        {
            publisherId = publisher.Id;
            Log("exists", $"publisher {PublisherName}");
        }

        if (FindOne("solution", "uniquename", SolutionName) is null)
        {
            client.Create(new Entity("solution")
            {
                ["uniquename"] = SolutionName,
                ["friendlyname"] = "Equipment Provisioning (base)",
                ["publisherid"] = new EntityReference("publisher", publisherId),
                ["version"] = "1.0.0.0",
                ["description"] = "AB-400 base solution: scenario tables, roles, environment variables and lab fixtures.",
            });
            Log("create", $"solution {SolutionName}");
        }
        else
        {
            Log("exists", $"solution {SolutionName}");
        }
    }

    private void EnsureGlobalChoice()
    {
        if (Exists(() => client.Execute(new RetrieveOptionSetRequest { Name = "eqp_devicetype" })))
        {
            Log("exists", "choice eqp_devicetype");
            return;
        }
        client.Execute(new CreateOptionSetRequest
        {
            OptionSet = new OptionSetMetadata
            {
                Name = "eqp_devicetype",
                DisplayName = L("Device type"),
                IsGlobal = true,
                OptionSetType = OptionSetType.Picklist,
                Options =
                {
                    new OptionMetadata(L("Laptop"), 100000000),
                    new OptionMetadata(L("Monitor"), 100000001),
                    new OptionMetadata(L("Phone"), 100000002),
                    new OptionMetadata(L("Tablet"), 100000003),
                },
            },
            SolutionUniqueName = SolutionName,
        });
        Log("create", "choice eqp_devicetype");
    }

    private void EnsureTables()
    {
        EnsureTable("eqp_Employee", "Employee", "Employees", "A person who holds and requests equipment.",
            PrimaryName("Name", 100, Required));
        EnsureTable("eqp_Device", "Device", "Devices", "A physical asset and its issue history.",
            PrimaryName("Asset tag", 20, Required));
        var requestNumber = PrimaryName("Request number", 100, Optional);
        requestNumber.AutoNumberFormat = "REQ-{SEQNUM:5}";
        EnsureTable("eqp_Request", "Request", "Requests", "A replacement request and its progress.", requestNumber);
        EnsureTable("eqp_Allowance", "Allowance", "Allowances", "How many devices an employee may receive in the current period.",
            PrimaryName("Name", 100, Required));
    }

    private static StringAttributeMetadata PrimaryName(string displayName, int maxLength, AttributeRequiredLevelManagedProperty level) => new()
    {
        SchemaName = "eqp_Name",
        DisplayName = L(displayName),
        RequiredLevel = level,
        MaxLength = maxLength,
        FormatName = StringFormatName.Text,
    };

    private void EnsureTable(string schemaName, string displayName, string pluralName, string description, StringAttributeMetadata primaryName)
    {
        var logicalName = schemaName.ToLowerInvariant();
        if (Exists(() => client.Execute(new RetrieveEntityRequest { LogicalName = logicalName, EntityFilters = EntityFilters.Entity })))
        {
            Log("exists", $"table {logicalName}");
            return;
        }
        client.Execute(new CreateEntityRequest
        {
            Entity = new EntityMetadata
            {
                SchemaName = schemaName,
                DisplayName = L(displayName),
                DisplayCollectionName = L(pluralName),
                Description = L(description),
                OwnershipType = OwnershipTypes.UserOwned,
                IsActivity = false,
            },
            PrimaryAttribute = primaryName,
            HasActivities = false,
            HasNotes = false,
            SolutionUniqueName = SolutionName,
        });
        Log("create", $"table {logicalName}");
    }

    private void EnsureColumns()
    {
        // Employee
        EnsureColumn("eqp_employee", Text("eqp_EmployeeNumber", "Employee number", 20, Required));
        EnsureColumn("eqp_employee", Email("eqp_Email", "Email"));
        EnsureColumn("eqp_employee", Text("eqp_Department", "Department", 100, Optional));

        // Device
        EnsureColumn("eqp_device", GlobalChoice("eqp_DeviceType", "Device type"));
        EnsureColumn("eqp_device", Text("eqp_Model", "Model", 100, Optional));
        EnsureColumn("eqp_device", LocalChoice("eqp_DeviceStatus", "Device status", null,
            ("In stock", 100000000), ("Issued", 100000001), ("Retired", 100000002)));
        EnsureColumn("eqp_device", DateOnly("eqp_IssuedOn", "Issued on",
            "The date the device was issued to its current holder. Used by the eligibility rule."));
        EnsureColumn("eqp_device", new BooleanAttributeMetadata
        {
            SchemaName = "eqp_DamagedBeyondRepair",
            DisplayName = L("Damaged beyond repair"),
            Description = L("When Yes, the device can be replaced at any time. Used by the eligibility rule."),
            RequiredLevel = Optional,
            DefaultValue = false,
            OptionSet = new BooleanOptionSetMetadata(new OptionMetadata(L("Yes"), 1), new OptionMetadata(L("No"), 0)),
        });
        EnsureColumn("eqp_device", WholeNumber("eqp_ConditionRating", "Condition rating", 1, 5,
            "Condition from 1 (poor) to 5 (as new). Not used by the eligibility rule."));

        // Request
        EnsureColumn("eqp_request", GlobalChoice("eqp_RequestedDeviceType", "Requested device type"));
        EnsureColumn("eqp_request", new MemoAttributeMetadata
        {
            SchemaName = "eqp_Justification",
            DisplayName = L("Justification"),
            RequiredLevel = Optional,
            MaxLength = 2000,
            Format = StringFormat.TextArea,
        });
        EnsureColumn("eqp_request", LocalChoice("eqp_RequestStatus", "Request status", 100000000,
            ("Submitted", 100000000), ("Approved", 100000001), ("Rejected", 100000002),
            ("In fulfillment", 100000003), ("Fulfilled", 100000004), ("Needs attention", 100000005)));
        EnsureColumn("eqp_request", DateTime("eqp_ApprovedOn", "Approved on", DateTimeBehavior.TimeZoneIndependent));
        EnsureColumn("eqp_request", LocalChoice("eqp_Availability", "Availability", 100000000,
            ("Not checked", 100000000), ("Available", 100000001), ("Unavailable", 100000002), ("Check failed", 100000003)));
        EnsureColumn("eqp_request", DateTime("eqp_AvailabilityCheckedOn", "Availability checked on", DateTimeBehavior.UserLocal));
        EnsureColumn("eqp_request", new MemoAttributeMetadata
        {
            SchemaName = "eqp_ProcessingNote",
            DisplayName = L("Processing note"),
            Description = L("Where functions and flows record outcomes and failures."),
            RequiredLevel = Optional,
            MaxLength = 2000,
            Format = StringFormat.TextArea,
        });

        // Allowance
        EnsureColumn("eqp_allowance", DateOnly("eqp_PeriodStart", "Period start", null));
        EnsureColumn("eqp_allowance", DateOnly("eqp_PeriodEnd", "Period end", null));
        EnsureColumn("eqp_allowance", WholeNumber("eqp_DevicesAllowed", "Devices allowed", 0, 100, null));
        EnsureColumn("eqp_allowance", WholeNumber("eqp_DevicesUsed", "Devices used", 0, 100, null));
        EnsureColumn("eqp_allowance", WholeNumber("eqp_Remaining", "Remaining", 0, 100,
            "A plain column, recalculated by a job, not a formula column."));
        EnsureColumn("eqp_allowance", DateTime("eqp_LastRecalculatedOn", "Last recalculated on", DateTimeBehavior.UserLocal));
    }

    private static StringAttributeMetadata Text(string schemaName, string displayName, int maxLength, AttributeRequiredLevelManagedProperty level) => new()
    {
        SchemaName = schemaName,
        DisplayName = L(displayName),
        RequiredLevel = level,
        MaxLength = maxLength,
        FormatName = StringFormatName.Text,
    };

    private static StringAttributeMetadata Email(string schemaName, string displayName) => new()
    {
        SchemaName = schemaName,
        DisplayName = L(displayName),
        RequiredLevel = Optional,
        MaxLength = 100,
        FormatName = StringFormatName.Email,
    };

    private static PicklistAttributeMetadata GlobalChoice(string schemaName, string displayName) => new()
    {
        SchemaName = schemaName,
        DisplayName = L(displayName),
        RequiredLevel = Optional,
        OptionSet = new OptionSetMetadata { IsGlobal = true, Name = "eqp_devicetype" },
    };

    private static PicklistAttributeMetadata LocalChoice(string schemaName, string displayName, int? defaultValue, params (string Label, int Value)[] options)
    {
        var optionSet = new OptionSetMetadata { IsGlobal = false, OptionSetType = OptionSetType.Picklist };
        foreach (var (label, value) in options)
        {
            optionSet.Options.Add(new OptionMetadata(L(label), value));
        }
        return new PicklistAttributeMetadata
        {
            SchemaName = schemaName,
            DisplayName = L(displayName),
            RequiredLevel = Optional,
            OptionSet = optionSet,
            DefaultFormValue = defaultValue,
        };
    }

    private static DateTimeAttributeMetadata DateOnly(string schemaName, string displayName, string? description) => new()
    {
        SchemaName = schemaName,
        DisplayName = L(displayName),
        Description = description is null ? null : L(description),
        RequiredLevel = Optional,
        Format = DateTimeFormat.DateOnly,
        DateTimeBehavior = DateTimeBehavior.DateOnly,
    };

    private static DateTimeAttributeMetadata DateTime(string schemaName, string displayName, DateTimeBehavior behavior) => new()
    {
        SchemaName = schemaName,
        DisplayName = L(displayName),
        RequiredLevel = Optional,
        Format = DateTimeFormat.DateAndTime,
        DateTimeBehavior = behavior,
    };

    private static IntegerAttributeMetadata WholeNumber(string schemaName, string displayName, int min, int max, string? description) => new()
    {
        SchemaName = schemaName,
        DisplayName = L(displayName),
        Description = description is null ? null : L(description),
        RequiredLevel = Optional,
        Format = IntegerFormat.None,
        MinValue = min,
        MaxValue = max,
    };

    private void EnsureColumn(string table, AttributeMetadata attribute)
    {
        var logicalName = attribute.SchemaName.ToLowerInvariant();
        if (Exists(() => client.Execute(new RetrieveAttributeRequest { EntityLogicalName = table, LogicalName = logicalName })))
        {
            Log("exists", $"column {table}.{logicalName}");
            return;
        }
        client.Execute(new CreateAttributeRequest { EntityName = table, Attribute = attribute, SolutionUniqueName = SolutionName });
        Log("create", $"column {table}.{logicalName}");
    }

    private void EnsureRelationships()
    {
        EnsureLookup("eqp_systemuser_employee_user", "systemuser", "eqp_employee", "eqp_User", "User", Optional,
            "Links an employee row to a sign-in identity.");
        EnsureLookup("eqp_employee_device_assignedto", "eqp_employee", "eqp_device", "eqp_AssignedTo", "Assigned to", Optional, null);
        EnsureLookup("eqp_employee_request_employee", "eqp_employee", "eqp_request", "eqp_Employee", "Employee", Required, null);
        EnsureLookup("eqp_device_request_currentdevice", "eqp_device", "eqp_request", "eqp_CurrentDevice", "Device to replace", Required, null);
        EnsureLookup("eqp_employee_allowance_employee", "eqp_employee", "eqp_allowance", "eqp_Employee", "Employee", Required, null);
    }

    private void EnsureLookup(string relationshipName, string referenced, string referencing, string lookupSchemaName,
        string lookupDisplayName, AttributeRequiredLevelManagedProperty level, string? description)
    {
        if (Exists(() => client.Execute(new RetrieveRelationshipRequest { Name = relationshipName })))
        {
            Log("exists", $"relationship {relationshipName}");
            return;
        }
        client.Execute(new CreateOneToManyRequest
        {
            OneToManyRelationship = new OneToManyRelationshipMetadata
            {
                SchemaName = relationshipName,
                ReferencedEntity = referenced,
                ReferencingEntity = referencing,
                AssociatedMenuConfiguration = new AssociatedMenuConfiguration
                {
                    Behavior = AssociatedMenuBehavior.UseCollectionName,
                    Group = AssociatedMenuGroup.Details,
                    Order = 10000,
                },
                CascadeConfiguration = new CascadeConfiguration
                {
                    Assign = CascadeType.NoCascade,
                    Delete = CascadeType.RemoveLink,
                    Merge = CascadeType.NoCascade,
                    Reparent = CascadeType.NoCascade,
                    Share = CascadeType.NoCascade,
                    Unshare = CascadeType.NoCascade,
                },
            },
            Lookup = new LookupAttributeMetadata
            {
                SchemaName = lookupSchemaName,
                DisplayName = L(lookupDisplayName),
                Description = description is null ? null : L(description),
                RequiredLevel = level,
            },
            SolutionUniqueName = SolutionName,
        });
        Log("create", $"relationship {relationshipName} ({referencing}.{lookupSchemaName.ToLowerInvariant()})");
    }

    private void EnsureKeys()
    {
        EnsureKey("eqp_employee", "eqp_key_employeenumber", "Employee number key", "eqp_employeenumber");
        EnsureKey("eqp_device", "eqp_key_assettag", "Asset tag key", "eqp_name");
        EnsureKey("eqp_request", "eqp_key_requestnumber", "Request number key", "eqp_name");
        EnsureKey("eqp_allowance", "eqp_key_allowancename", "Allowance name key", "eqp_name");
    }

    private void EnsureKey(string table, string keyName, string displayName, string column)
    {
        if (Exists(() => client.Execute(new RetrieveEntityKeyRequest { EntityLogicalName = table, LogicalName = keyName })))
        {
            Log("exists", $"key {table}.{keyName}");
            return;
        }
        client.Execute(new CreateEntityKeyRequest
        {
            EntityName = table,
            EntityKey = new EntityKeyMetadata { SchemaName = keyName, DisplayName = L(displayName), KeyAttributes = [column] },
            SolutionUniqueName = SolutionName,
        });
        Log("create", $"key {table}.{keyName}");
    }

    private void EnsureEnvironmentVariables()
    {
        EnsureStringVariable("eqp_FulfillmentContact", "Fulfillment contact", "equipment-desk@example.com",
            "Who handles fulfilment problems. Read at run time by the code app (lab 04) and the cloud flow (lab 08).");
        EnsureStringVariable("eqp_AvailabilityMode", "Availability mode", "Available",
            "Controls the availability stand-in (eqp_CheckAvailability): Available, Unavailable or Error.");
    }

    private void EnsureStringVariable(string schemaName, string displayName, string defaultValue, string description)
    {
        if (FindOne("environmentvariabledefinition", "schemaname", schemaName) is not null)
        {
            Log("exists", $"environment variable {schemaName}");
            return;
        }
        var create = new CreateRequest
        {
            Target = new Entity("environmentvariabledefinition")
            {
                ["schemaname"] = schemaName,
                ["displayname"] = displayName,
                ["description"] = description,
                ["type"] = new OptionSetValue(100000000), // String
                ["defaultvalue"] = defaultValue,
            },
        };
        create.Parameters["SolutionUniqueName"] = SolutionName;
        client.Execute(create);
        Log("create", $"environment variable {schemaName}");
    }

    private Entity? FindOne(string table, string column, string value)
    {
        var query = new QueryExpression(table) { ColumnSet = new ColumnSet(false), TopCount = 1 };
        query.Criteria.AddCondition(column, ConditionOperator.Equal, value);
        return client.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    private static bool Exists(Action retrieve)
    {
        try
        {
            retrieve();
            return true;
        }
        catch (FaultException<OrganizationServiceFault>)
        {
            return false;
        }
    }

    private static void Log(string action, string message) => Console.WriteLine($"[{action,-7}] {message}");
}
