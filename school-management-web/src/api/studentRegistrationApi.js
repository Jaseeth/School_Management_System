import api from "./axios";

export const studentRegistrationApi = {

    generateCode(data) {
        return api.post(
            "/student-registration-codes/generate",
            data
        );
    },

    validateCode(data) {
        return api.post(
            "/student-registration-codes/validate",
            data
        );
    },

    requestEmailOtp(data) {
        return api.post(
            "/student-registration-codes/request-email-otp",
            data
        );
    },

    verifyEmailOtp(data) {
        return api.post(
            "/student-registration-codes/verify-email-otp",
            data
        );
    },

    completeRegistration(data) {
        return api.post(
            "/student-registration-codes/complete",
            data
        );
    },

    requestPasswordResetOtp(data) {
        return api.post(
            "/student-registration/forgot-password/request-otp",
            data
        );
    },
};