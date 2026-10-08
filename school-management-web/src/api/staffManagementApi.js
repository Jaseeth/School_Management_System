import api from "./axios";

export const staffManagementApi = {
    getStaff() { return api.get("/Staff"); },
    getRoles() { return api.get("/Roles"); },
    createStaff(data) { return api.post("/Staff", data); },
};

export function staffErrorMessage(error, fallback) {
    if (error?.response?.status === 401) return "Your session has expired. Sign in again.";
    if (error?.response?.status === 403) return "Your account does not have permission for this staff action.";
    const data = error?.response?.data;
    const errors = Array.isArray(data?.errors) ? data.errors : data?.errors ? Object.values(data.errors).flat() : [];
    return errors.length ? errors.join(" ") : data?.message || fallback;
}
