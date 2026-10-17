import api from "./axios";

export const sectionHeadAssignmentsApi = {
    getAssignments() {
        return api.get("/section-head-assignments");
    },
    getStaff() {
        return api.get("/Staff");
    },
    update(id, data) { return api.put(`/section-head-assignments/${id}`, data); },
    remove(id) { return api.delete(`/section-head-assignments/${id}`); },
    assign(data) {
        return api.post("/section-head-assignments", data);
    },
};
