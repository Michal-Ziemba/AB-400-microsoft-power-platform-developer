var EquipmentProvisioning = window.EquipmentProvisioning || {};

// Runs the eqp_ResetLabData custom API from the Reset lab data command on the Requests and
// Employees views of the Equipment Provisioning app.
EquipmentProvisioning.resetLabData = async function () {
    Xrm.Utility.showProgressIndicator("Resetting lab data…");
    try {
        var request = {
            getMetadata: function () {
                return { boundParameter: null, parameterTypes: {}, operationType: 0, operationName: "eqp_ResetLabData" };
            }
        };
        var response = await Xrm.WebApi.online.execute(request);
        var result = await response.json();
        Xrm.Utility.closeProgressIndicator();
        await Xrm.Navigation.openAlertDialog({
            title: "Lab data reset",
            text: result.Summary + " Select Refresh to see the changes."
        });
    } catch (error) {
        Xrm.Utility.closeProgressIndicator();
        await Xrm.Navigation.openErrorDialog({ message: error.message });
    }
};

window.EquipmentProvisioning = EquipmentProvisioning;
