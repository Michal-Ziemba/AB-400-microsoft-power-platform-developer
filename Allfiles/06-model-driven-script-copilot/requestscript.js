var EquipmentProvisioning = window.EquipmentProvisioning || {};

// Lab 06: client-side logic for the Request main form in the Equipment Provisioning app.
// Everything in this file only reads Dataverse data - it never writes anything back.
//
// Give the comment block above each function below to GitHub Copilot, one at a time, and
// ask it to implement the function it describes.
EquipmentProvisioning.Lab06 = (function () {
    var ELIGIBILITY_NOTIFICATION_ID = "eqp_lab06_eligibility";
    var ALLOWANCE_NOTIFICATION_ID = "eqp_lab06_allowance";

    /*
     * Requirements for onFormLoad(executionContext):
     * - Get the form context from the execution context, and clear both notifications
     *   below (unique IDs ELIGIBILITY_NOTIFICATION_ID and ALLOWANCE_NOTIFICATION_ID) as
     *   a first step.
     * - Read the lookup value of the eqp_currentdevice column. If a device is selected:
     *     - Call the eqp_IsEligibleForReplacement custom API with
     *       Xrm.WebApi.online.execute, passing the device's ID as the DeviceId request
     *       parameter. (The documented way to call a custom API like this is a small
     *       request object with a getMetadata method, as described for
     *       Xrm.WebApi.online.execute.)
     *     - If the response's IsEligible value is false, retrieve the device's
     *       eqp_issuedon column with Xrm.WebApi.retrieveRecord, and show a WARNING form
     *       notification (unique ID ELIGIBILITY_NOTIFICATION_ID) naming the issue date
     *       and the date the device becomes eligible (the issue date plus 24 calendar
     *       months). If IsEligible is true, clear that notification instead.
     * - Read the lookup value of the eqp_employee column. If an employee is selected,
     *   query the eqp_allowance table with Xrm.WebApi.retrieveMultipleRecords, filtered
     *   to that employee's records, selecting eqp_remaining. Show an INFO form
     *   notification (unique ID ALLOWANCE_NOTIFICATION_ID) naming the employee and
     *   their eqp_remaining value. If no allowance row is found, clear the notification
     *   instead.
     * - Log the eligibility result and the remaining allowance to the console with
     *   console.log, so they're visible while debugging.
     */
    function onFormLoad(executionContext) {
        // TODO: implement using the requirements above
    }

    /*
     * Requirements for confirmProvisioning(primaryControl):
     * - primaryControl is the form context, passed in by the command's Primary Control
     *   parameter.
     * - Read the request's own eqp_name value (the request number).
     * - Use Xrm.Navigation.openConfirmDialog to ask the coordinator to confirm sending
     *   that request to the fulfillment team. Give the dialog a title and a message
     *   that includes the request number.
     * - When the dialog closes, log to the console whether the coordinator confirmed
     *   or canceled. Don't update any Dataverse records - this command is read-only.
     */
    function confirmProvisioning(primaryControl) {
        // TODO: implement using the requirements above
    }

    return {
        onFormLoad: onFormLoad,
        confirmProvisioning: confirmProvisioning
    };
})();

window.EquipmentProvisioning = EquipmentProvisioning;
