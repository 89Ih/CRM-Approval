const APPROVE_STATUS_FIELDS = ["pr_m1decision", "pr_m2decision"];
const api = restService();

function onLoad(executionContext) {
    const formContext = executionContext?.getFormContext?.();
    if (!formContext) return;
    generalValidation(formContext);
    const isValid = generalValidation(formContext);

    if (isValid) {
        handleAgreement(formContext);

    } else {


    }
}
function onChange(executionContext) {
    const formContext = executionContext?.getFormContext?.();
    if (!formContext) return;
    const isValid = generalValidation(formContext);
}
function generalValidation(formContext) {
    const isInProcessing = APPROVE_STATUS_FIELDS.some((field) => {
        return formContext.getAttribute(field)?.getValue() !== null; // true
    });
    const entryCompleted =
        formContext.getAttribute("statecode")?.getValue() === 1; // entry completed
    return !isInProcessing && entryCompleted ? true : false;
}
function handleAgreement(formContext) {
    const agreementTypeAttr = formContext.getAttribute("pr_customeragreement");
    const agreementType = agreementTypeAttr.getValue();
    if (agreementType) {
        switch (agreementType) {
            case 125620000:
                objectCondition(formContext)
                console.log("Payment");
                break;
            case 125620001:
                console.log("Delivery");
                break;
            case 125620002:
                console.log("Discount");
                break;
        }
    } else {
        console.log("agreement not defined");
    }
}
function objectCondition(formContext) {
    const approver = "8dcd51ee-c93b-ec11-8c61-00224813d3d5";
    createSecondApprove(formContext, approver, approver)
}
async function getApproval(formContext) {
    const id = getRecordId(formContext);

    const data = await api.getAll("pr_approvement", `?$filter=pr_entitytragetguid eq '${id}'`);

    return (data && data.length > 0)
        ? data
        : []
}
async function createApproval(formContext, approver) {
    const OPEN = 125620000;
    const id = getRecordId(formContext);
    const entityName = formContext.data.entity.getEntityName();
    const jobTitle = formContext.getAttribute("pr_jobtitle")?.getValue();

    const data = {
        pr_name: jobTitle,
        pr_entitytraget: entityName,
        pr_entitytragetguid: id,
        "ownerid@odata.bind": `/systemusers(${approver})`

    };

    await api.create("pr_approvement", data);

}
async function updateReleaseStatus(formContext, stateKey) {

    const releaseMap = {
        125620000: "pr_mdecision",
        125620001: "pr_m1decision",
        125620002: "pr_m2decision",
    };

    const release = formContext.getAttribute("pr_requiredrelease")?.getValue();

    if (release && releaseMap[release]) {
        formContext.getAttribute(releaseMap[release]).setValue(stateKey);
    }
}
function createSecondApprove(formContext, approver1, approver2) {
    const currentRecordId = getRecordId(formContext);
    const entityName = formContext.data.entity.getEntityName();
    const jobTitle = formContext.getAttribute("pr_jobtitle")?.getValue();

    const record1 = {
        pr_name: "Erste Freigabe - " + jobTitle,
        pr_entitytraget: entityName,
        pr_entitytragetguid: currentRecordId,
        pr_twostageapproval: true,
        "ownerid@odata.bind": `/systemusers(${approver1})`
    };
    api.create("pr_approvement", record1)
        .then(({ id, entityType }) => {
            if (id) {
                return api.create(entityType, {
                    pr_name: "Zweite Freigabe - " + jobTitle,
                    pr_entitytraget: entityName,
                    pr_entitytragetguid: currentRecordId,
                    "pr_Approvel@odata.bind": `/pr_approvements(${id})`,
                    "ownerid@odata.bind": `/systemusers(${approver2})`
                });
            }
        })
        .catch(err => {
            console.error("Error creating record:", err);
        });



}


function getRecordId(formContext) {
    return formContext.data.entity.getId().replace(/[{}]/g, "").toLowerCase();
}
function getLookupId(formContext, fieldName) {
    const lookup = formContext.getAttribute(fieldName)?.getValue();
    return lookup && lookup.length > 0
        ? lookup[0].id.replace(/[{}]/g, "").toLowerCase()
        : null;
}


// const isOneRejected = ["pr_m1decision", "pr_m2decision"].some((field) => {
//     return formContext.getAttribute(field)?.getValue() === 125620002;
// });