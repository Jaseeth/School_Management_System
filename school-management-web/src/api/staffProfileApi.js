import api from "./axios";

export const staffProfileApi = {
    getProfile() {
        return api.get("/Staff/my/profile");
    },

    updateProfile(payload) {
        return api.put("/Staff/my/profile", payload);
    },
};