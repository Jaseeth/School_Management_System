import api from "./axios";

export const parentManagementApi = {
    getParents(params = {}) {
        return api.get("/parent-guardians", { params });
    },

    createParent(payload) {
        return api.post("/parent-guardians", payload);
    },

    getParentStudents(parentId) {
        return api.get(`/parent-guardians/${parentId}/students`);
    },

    updateParent(parentId, payload) {
        return api.put(`/parent-guardians/${parentId}`, payload);
    },

    createAccount(payload) {
        return api.post("/parent-guardians/create-account", payload);
    },

    linkStudent(payload) {
        return api.post("/parent-guardians/link-student", payload);
    },

    updateRelationship(relationshipId, payload) {
        return api.put(
            `/parent-guardians/relationships/${relationshipId}`,
            payload
        );
    },

    disableRelationship(relationshipId) {
        return api.patch(
            `/parent-guardians/relationships/${relationshipId}/disable`
        );
    },
};