const APPROVE_STATUS_FIELDS = ["pr_worktimemodel_set", "pr_entry_set"];
const api = restService();

function onLoad(executionContext) {
    const formContext = executionContext?.getFormContext?.();
    if (!formContext) return;
    generalValidation(formContext);
    const isValid = generalValidation(formContext);

    if (isValid) {
        handleAgreement(formContext);
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
        getApproval(formContext);
        switch (agreementType) {
            case 125620000:
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
async function getApproval(formContext) {
    const id = getRecordId(formContext);

    const data = await api.getAll(
        "pr_approvement",
        `?$filter=pr_entitytragetguid eq '${id}'`
    );

    return (data && data.length > 0)
        ? data
        : await createApproval(formContext);
}
async function createApproval(formContext) {
    const id = getRecordId(formContext);
    const entityName = formContext.data.entity.getEntityName();
    const jobTitle = formContext.getAttribute("pr_jobtitle")?.getValue();

    const data = {
        pr_name: jobTitle,
        pr_entitytraget: entityName,
        pr_entitytragetguid: id,
    };

    await api.create("pr_approvement", data);
    await updateReleaseStatus(formContext);
}
async function updateReleaseStatus(formContext) {
    const OPEN = 125620000;
    const releaseMap = {
        125620000: "pr_mdecision",
        125620001: "pr_m1decision",
        125620002: "pr_m2decision",
    };

    const release = formContext.getAttribute("pr_requiredrelease")?.getValue();

    if (release && releaseMap[release]) {
        formContext.getAttribute(releaseMap[release]).setValue(OPEN);
    }
}
function getRecordId(formContext) {
    return formContext.data.entity.getId().replace(/[{}]/g, "").toLowerCase();
}
