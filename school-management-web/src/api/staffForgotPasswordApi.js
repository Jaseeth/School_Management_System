import api from "./axios";

export const staffForgotPasswordApi = {
    requestOtp(staffNumber, email) {
        return api.post("/Auth/staff-forgot-password/request-otp", {
            staffNumber,
            email,
        });
    },

    verifyOtp(staffNumber, email, otp) {
        return api.post("/Auth/staff-forgot-password/verify-otp", {
            staffNumber,
            email,
            otp,
        });
    },

    resetPassword(staffNumber, email, newPassword, confirmPassword) {
        return api.post("/Auth/staff-forgot-password/reset-password", {
            staffNumber,
            email,
            newPassword,
            confirmPassword,
        });
    },
};