import api from "./axios";

export const studentAuthApi = {

    login(data) {
        return api.post(
            "/Auth/student-login",
            data
        );

        return api.post("/Auth/student-login", data);
    },

    changePassword(data) {
        return api.post("/Auth/change-password", data);
    },

    requestPasswordResetOtp(data) {
        return api.post("/student-registration/forgot-password/request-otp", data);
    },

    verifyPasswordResetOtp(data) {
        return api.post("/student-registration/forgot-password/verify-otp", data);
    },

    resetStudentPassword(data) {
        return api.post("/student-registration/forgot-password/reset-password", data);
    },

};