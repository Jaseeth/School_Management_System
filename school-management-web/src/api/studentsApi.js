import api from "./axios";

export const studentsApi = {
    getManagementStudents(params = {}) {
        return api.get(
            "/Students/management",
            {
                params,
            }
        );
    },

    getManagementSummary() {
        return api.get(
            "/Students/management-summary"
        );
    },

    getStudentDetails(id) {
        return api.get(
            `/Students/${id}/details`
        );
    },

    createStudent(data) {
        return api.post(
            "/Students",
            data
        );
    },
};