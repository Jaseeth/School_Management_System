import api from "./axios";

export const studentPortalApi = {

    getProfile() {
        return api.get(
            "/student-portal/profile"
        );
    },

    getSubjects() {
        return api.get(
            "/student-portal/subjects"
        );
    },

    getAttendance() {
        return api.get(
            "/student-portal/attendance"
        );
    },

    getResults() {
        return api.get(
            "/student-results/my/summary"
        );
    },

    getTimetable() {
        return api.get(
            "/student-portal/timetable"
        );
    },

    getAnnouncements() {
        return api.get(
            "/school-announcements/my/student"
        );
    },

    getNotifications() {
        return api.get(
            "/student-portal/notifications"
        );
    },

    markNotificationRead(notificationId) {
        return api.post(
            `/student-portal/notifications/${notificationId}/read`
        );
    },

    markAllNotificationsRead() {
        return api.post(
            "/student-portal/notifications/read-all"
        );
    },

    getSpecialClasses(upcomingOnly = true) {
        return api.get(
            "/student-schedule/special-classes",
            { params: { upcomingOnly } }
        );
    },

    getDailySchedule(academicYearId, academicTermId, date) {
        return api.get(
            "/unified-schedule/date",
            { params: { academicYearId, academicTermId, date } }
        );
    },

    getEnrollmentHistory() {
        return api.get(
            "/student-portal/enrollment-history"
        );
    },

    getGuardians() {
        return api.get(
            "/student-portal/guardians"
        );
    },

    updateProfile(payload) {
        return api.put("/student-portal/profile", payload);
    },

};