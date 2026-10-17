import api from "./axios";

export const studentsApi = {
    registerStudent(data) {
        return api.post("/students/registration", data);
    },
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

    resetStudentPassword(indexNumber) {
        return api.post("/student-password-admin/reset", { indexNumber });
    },
};
