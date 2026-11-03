// Run in the Equipment Provisioning app's browser console only after deleting
// and publishing the Lab 06 command and removing its form handler and library.
(async function () {
    const base = Xrm.Utility.getGlobalContext().getClientUrl() + "/api/data/v9.2/";
    const headers = { Accept: "application/json", "OData-Version": "4.0", "OData-MaxVersion": "4.0" };

    async function read(path) {
        const response = await fetch(base + path, { credentials: "same-origin", headers: headers });
        if (!response.ok) {
            throw new Error("Cleanup read failed (" + response.status + "): " + await response.text());
        }
        return response.json();
    }

    function onlyRow(result, description) {
        if (!Array.isArray(result.value) || result.value.length !== 1 || result["@odata.nextLink"]) {
            throw new Error("Expected exactly one " + description + ". No action was deleted.");
        }
        return result.value[0];
    }

    const app = onlyRow(await read(
        "appmodules?$select=appmoduleid&$filter=" + encodeURIComponent("uniquename eq 'eqp_EquipmentProvisioning'")
    ), "Equipment Provisioning app");
    const resource = onlyRow(await read(
        "webresourceset?$select=webresourceid&$filter=" + encodeURIComponent("name eq 'eqp_requestscript'")
    ), "Lab 06 web resource");
    const filter = "_appmoduleid_value eq " + app.appmoduleid +
        " and _onclickeventjavascriptwebresourceid_value eq " + resource.webresourceid;
    const rows = await read(
        "appactions?$select=appactionid,uniquename,ismanaged,onclickeventjavascriptfunctionname," +
        "_appmoduleid_value,_onclickeventjavascriptwebresourceid_value&$filter=" + encodeURIComponent(filter)
    );
    const candidate = onlyRow(rows, "App Action linked to this app and web resource");
    const action = await read("appactions(" + candidate.appactionid + ")?$select=appactionid,uniquename," +
        "ismanaged,onclickeventjavascriptfunctionname,_appmoduleid_value,_onclickeventjavascriptwebresourceid_value");

    if (action.appactionid !== candidate.appactionid ||
        action.ismanaged !== false ||
        action._appmoduleid_value !== app.appmoduleid ||
        action._onclickeventjavascriptwebresourceid_value !== resource.webresourceid ||
        typeof action.uniquename !== "string" ||
        !action.uniquename.toLowerCase().includes("sendtofulfillment") ||
        typeof action.onclickeventjavascriptfunctionname !== "string" ||
        action.onclickeventjavascriptfunctionname.toLowerCase() !== "equipmentprovisioning.lab06.confirmprovisioning" ||
        typeof action["@odata.etag"] !== "string" ||
        !/^W\/"\d+"$/.test(action["@odata.etag"])) {
        throw new Error("The remaining action doesn't match all Lab 06 ownership checks. No action was deleted.");
    }

    console.table([{
        actionId: action.appactionid,
        uniqueName: action.uniquename,
        functionName: action.onclickeventjavascriptfunctionname,
        appId: app.appmoduleid,
        webResourceId: resource.webresourceid
    }]);
    const confirmation = await Xrm.Navigation.openConfirmDialog({
        title: "Remove the remaining Lab 06 command dependency?",
        text: "Continue only if you already deleted and published your lab command, removed its form handler " +
            "and library, and the dependency details identify this exact App Action:\n" + action.appactionid +
            "\n\nThis deletes only that App Action. Cancel if its ownership is uncertain."
    });
    if (!confirmation.confirmed) {
        console.log("Cleanup cancelled. No action was deleted.");
        return;
    }

    const path = "appactions(" + action.appactionid + ")";
    const deleted = await fetch(base + path, {
        method: "DELETE",
        credentials: "same-origin",
        headers: Object.assign({}, headers, { "If-Match": action["@odata.etag"] })
    });
    if (deleted.status !== 204) {
        throw new Error("App Action deletion failed (" + deleted.status + "): " + await deleted.text());
    }
    const verification = await fetch(base + path, { credentials: "same-origin", headers: headers });
    if (verification.status !== 404) {
        throw new Error("Deletion returned 204, but the verification didn't return 404 (" + verification.status + ").");
    }
    console.log("The selected Lab 06 App Action was deleted and its absence verified. " +
        "Return to the maker portal to delete the lab web resource.");
})().catch(function (error) {
    console.error("Lab 06 cleanup stopped: " + error.message);
});
