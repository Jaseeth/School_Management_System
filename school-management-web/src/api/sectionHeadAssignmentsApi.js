import api from "./axios";

export const sectionHeadAssignmentsApi = {
    getAssignments() {
        return api.get("/section-head-assignments");
    },
    getStaff() {
        return api.get("/Staff");
    },
    assign(data) {
        return api.post("/section-head-assignments", data);
    },
};
