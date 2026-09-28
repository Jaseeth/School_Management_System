import api from "./axios";

export const parentPortalApi = {
    login(email, password) {
        return api.post("/Auth/login", { email, password });
    },

    getChildren() {
        return api.get("/parent-guardians/my/children");
    },

    updateMyProfile(payload) {
        return api.put("/parent-guardians/my/profile", payload);
    },

    getChildAcademicProfile(studentId) {
        return api.get(
            `/parent-guardians/my/children/${studentId}/academic-profile`
        );
    },

    getChildAttendance(studentId, academicYearId) {
        return api.get(
            `/parent-guardians/my/children/${studentId}/attendance`,
            { params: { academicYearId } }
        );
    },

    getChildResults(studentId, academicYearId, academicTermId) {
        return api.get(
            `/parent-guardians/my/children/${studentId}/results`,
            { params: { academicYearId, academicTermId } }
        );
    },

    getNotifications() {
        return api.get("/parent-guardians/my/notifications");
    },

    markNotificationRead(notificationId) {
        return api.patch(
            `/parent-guardians/my/notifications/${notificationId}/read`
        );
    },

    changePassword(payload) {
        return api.post("/Auth/change-password", payload);
    },
};