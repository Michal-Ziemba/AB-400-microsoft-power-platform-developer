var EquipmentProvisioning = window.EquipmentProvisioning || {};

// Lab 06: client-side logic for the Request main form in the Equipment Provisioning app.
// Everything in this file only reads Dataverse data - it never writes anything back.
//
// This is the completed, corrected reference version: what a good GitHub Copilot draft
// plus the review-and-correct task in the lab should produce.
EquipmentProvisioning.Lab06 = (function () {
    var ELIGIBILITY_NOTIFICATION_ID = "eqp_lab06_eligibility";
    var ALLOWANCE_NOTIFICATION_ID = "eqp_lab06_allowance";

    // Adds `months` calendar months to an ISO 8601 date string (yyyy-mm-dd) and returns a
    // new ISO date string, clamped to the last day of the target month when needed (for
    // example, 2024-02-29 plus 24 months becomes 2026-02-28).
    function addMonthsUTC(isoDate, months) {
        var parts = isoDate.substring(0, 10).split("-");
        var year = Number(parts[0]);
        var month = Number(parts[1]) - 1;
        var day = Number(parts[2]);

        var targetIndex = month + months;
        var targetYear = year + Math.floor(targetIndex / 12);
        var targetMonth = ((targetIndex % 12) + 12) % 12;
        var lastDayOfTargetMonth = new Date(Date.UTC(targetYear, targetMonth + 1, 0)).getUTCDate();
        var targetDay = Math.min(day, lastDayOfTargetMonth);

        return new Date(Date.UTC(targetYear, targetMonth, targetDay)).toISOString().substring(0, 10);
    }

    // Request object for the eqp_IsEligibleForReplacement unbound custom API (F2). This
    // follows the documented pattern for Xrm.WebApi.online.execute: a request object with
    // a getMetadata method attached to its prototype.
    function IsEligibleForReplacementRequest(deviceId) {
        this.DeviceId = deviceId;
    }
    IsEligibleForReplacementRequest.prototype.getMetadata = function () {
        return {
            boundParameter: null,
            parameterTypes: {
                "DeviceId": { "typeName": "Edm.Guid", "structuralProperty": 1 }
            },
            operationType: 0, // Action
            operationName: "eqp_IsEligibleForReplacement"
        };
    };

    function onFormLoad(executionContext) {
        var formContext = executionContext.getFormContext();
        formContext.ui.clearFormNotification(ELIGIBILITY_NOTIFICATION_ID);
        formContext.ui.clearFormNotification(ALLOWANCE_NOTIFICATION_ID);

        var device = formContext.getAttribute("eqp_currentdevice").getValue();
        if (device && device.length > 0) {
            var deviceId = device[0].id.replace(/[{}]/g, "");
            var eligibilityRequest = new IsEligibleForReplacementRequest(deviceId);

            Xrm.WebApi.online.execute(eligibilityRequest).then(function (response) {
                return response.json();
            }).then(function (result) {
                console.log("eqp_IsEligibleForReplacement returned IsEligible=" + result.IsEligible);
                if (result.IsEligible) {
                    formContext.ui.clearFormNotification(ELIGIBILITY_NOTIFICATION_ID);
                    return;
                }
                return Xrm.WebApi.retrieveRecord("eqp_device", deviceId, "?$select=eqp_issuedon").then(function (deviceRow) {
                    var issuedOn = deviceRow.eqp_issuedon.substring(0, 10);
                    var eligibleFrom = addMonthsUTC(issuedOn, 24);
                    var message = "This device was issued on " + issuedOn + ". It can be replaced from " +
                        eligibleFrom + ", or earlier if it's flagged as damaged beyond repair.";
                    formContext.ui.setFormNotification(message, "WARNING", ELIGIBILITY_NOTIFICATION_ID);
                });
            }).catch(function (error) {
                console.log(error.message);
            });
        }

        var employee = formContext.getAttribute("eqp_employee").getValue();
        if (employee && employee.length > 0) {
            var employeeId = employee[0].id.replace(/[{}]/g, "");
            var employeeName = employee[0].name;
            var options = "?$select=eqp_remaining&$filter=_eqp_employee_value eq " + employeeId;

            Xrm.WebApi.retrieveMultipleRecords("eqp_allowance", options).then(function (result) {
                if (result.entities.length === 0) {
                    formContext.ui.clearFormNotification(ALLOWANCE_NOTIFICATION_ID);
                    return;
                }
                var remaining = result.entities[0].eqp_remaining;
                console.log(employeeName + " has " + remaining + " remaining in the current allowance period.");
                var message = employeeName + " has " + remaining +
                    " replacement device(s) remaining in the current allowance period.";
                formContext.ui.setFormNotification(message, "INFO", ALLOWANCE_NOTIFICATION_ID);
            }).catch(function (error) {
                console.log(error.message);
            });
        }
    }

    function confirmProvisioning(primaryControl) {
        var formContext = primaryControl;
        var requestNumber = formContext.getAttribute("eqp_name").getValue();
        var confirmStrings = {
            title: "Send to fulfillment",
            text: "Send " + requestNumber + " to the fulfillment team?"
        };
        var confirmOptions = { height: 200, width: 450 };

        Xrm.Navigation.openConfirmDialog(confirmStrings, confirmOptions).then(function (result) {
            if (result.confirmed) {
                console.log(requestNumber + ": coordinator confirmed sending to fulfillment.");
            } else {
                console.log(requestNumber + ": coordinator canceled.");
            }
        });
    }

    return {
        onFormLoad: onFormLoad,
        confirmProvisioning: confirmProvisioning,
        addMonthsUTC: addMonthsUTC
    };
})();

window.EquipmentProvisioning = EquipmentProvisioning;
