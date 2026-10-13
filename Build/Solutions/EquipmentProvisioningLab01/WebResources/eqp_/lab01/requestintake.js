var EquipmentProvisioning = window.EquipmentProvisioning || {};

// Lab 01 (F7): the client-side eligibility check on the Request intake form. It stops a coordinator from
// approving an ineligible request in the form. It runs in the browser only, so a direct Web API call
// bypasses it; that gap is what the lab's plug-in closes on the server.
EquipmentProvisioning.Lab01 = (function () {
    var APPROVED = 100000001;
    var NOTIFICATION_ID = "eqp_eligibility";
    var originalStatus = null;
    var check = null; // { eligible, issuedOn } for the current device, or null while unknown

    // Same rule as the server: eligible if damaged beyond repair, if there's no issue date,
    // or if issued on or before today minus 24 calendar months (UTC dates, yyyy-mm-dd).
    function isEligible(issuedOn, damagedBeyondRepair, today) {
        if (damagedBeyondRepair) { return true; }
        if (!issuedOn) { return true; }
        var year = today.getUTCFullYear() - 2;
        var month = today.getUTCMonth();
        var lastDay = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
        var day = Math.min(today.getUTCDate(), lastDay);
        var cutoff = new Date(Date.UTC(year, month, day)).toISOString().substring(0, 10);
        return issuedOn.substring(0, 10) <= cutoff;
    }

    function refresh(formContext) {
        check = null;
        var device = formContext.getAttribute("eqp_currentdevice").getValue();
        if (!device || device.length === 0) { return; }
        var id = device[0].id.replace(/[{}]/g, "");
        Xrm.WebApi.retrieveRecord("eqp_device", id, "?$select=eqp_issuedon,eqp_damagedbeyondrepair").then(function (row) {
            check = {
                eligible: isEligible(row.eqp_issuedon, row.eqp_damagedbeyondrepair === true, new Date()),
                issuedOn: row.eqp_issuedon ? row.eqp_issuedon.substring(0, 10) : null
            };
        });
    }

    function onLoad(executionContext) {
        var formContext = executionContext.getFormContext();
        originalStatus = formContext.getAttribute("eqp_requeststatus").getValue();
        formContext.ui.clearFormNotification(NOTIFICATION_ID);
        refresh(formContext);
    }

    function onDeviceChange(executionContext) {
        refresh(executionContext.getFormContext());
    }

    function onSave(executionContext) {
        var formContext = executionContext.getFormContext();
        var status = formContext.getAttribute("eqp_requeststatus").getValue();
        if (status !== APPROVED || originalStatus === APPROVED || (check && check.eligible)) {
            formContext.ui.clearFormNotification(NOTIFICATION_ID);
            return;
        }
        executionContext.getEventArgs().preventDefault();
        var message = check
            ? "This device was issued on " + check.issuedOn + ", less than 24 months ago, and isn't damaged beyond repair. The request can't be approved yet."
            : "Eligibility is still being checked. Try saving again in a moment.";
        formContext.ui.setFormNotification(message, "ERROR", NOTIFICATION_ID);
    }

    return { onLoad: onLoad, onDeviceChange: onDeviceChange, onSave: onSave, isEligible: isEligible };
})();

window.EquipmentProvisioning = EquipmentProvisioning;
