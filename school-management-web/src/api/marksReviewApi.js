import api from "./axios";

export const marksReviewApi = {
    getPending() {
        return api.get("/marks-review/pending");
    },

    getSubmission(submissionId) {
        return api.get(`/marks-review/${submissionId}`);
    },

    approve(submissionId, comment) {
        return api.post(`/marks-review/${submissionId}/approve`, {
            comment: comment.trim() || null,
        });
    },

    reject(submissionId, comment) {
        return api.post(`/marks-review/${submissionId}/reject`, {
            comment: comment.trim(),
        });
    },
};