function restService() {
    async function safeExecute(fn) {
        try {
            return await fn();
        } catch (error) {
            console.error("REST call failed:", error.message);
            throw error;
        }
    };

    return {
        getAll: async function (entityName, query) {
            return safeExecute(async () => {
                const result = await Xrm.WebApi.retrieveMultipleRecords(entityName, query || "");
                return result.entities;
            });
        },

        getById: async function (entityName, id, columns) {
            id = id.replace(/[{}]/g, "");
            return safeExecute(async () => {
                return await Xrm.WebApi.retrieveRecord(
                    entityName,
                    id,
                    columns ? `?$select=${columns}` : ""
                );
            });
        },

        updateById: async function (entityName, id, data) {
            id = id.replace(/[{}]/g, "");
            return safeExecute(async () => {
                await Xrm.WebApi.updateRecord(entityName, id, data);
                console.log(`Record ${entityName}(${id}) updated successfully`);
            });
        },
        create: async function (entityName, data) {
            return safeExecute(async () => {
                const response = await Xrm.WebApi.createRecord(entityName, data);
                return response;
                // setNotification("Record created successfully", "INFO", "randemId");
            });
        }
    };
}
