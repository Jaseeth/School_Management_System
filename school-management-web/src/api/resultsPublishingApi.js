import api from "./axios";

export const resultsPublishingApi = {
    getApproved() {
        return api.get("/marks-review/approved");
    },

    getPublished() {
        return api.get("/marks-review/published");
    },

    getSubmission(id) {
        return api.get(`/marks-review/${id}`);
    },

    publish(id) {
        return api.post(`/marks-review/${id}/publish`);
    },
};