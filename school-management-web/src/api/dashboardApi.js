import api from "./axios";

export const dashboardApi = {
    getAdminSummary() {
        return api.get("/dashboard/admin-summary");
    },

    getManagementSummary() {
        return api.get("/dashboard/management-summary");
    },

    getSectionHeadSummary() {
        return api.get("/dashboard/section-head-summary");
    },

    getTeacherSummary() {
        return api.get("/dashboard/teacher-summary");
    },

    getStudentSummary() {
        return api.get("/dashboard/student-summary");
    },
};