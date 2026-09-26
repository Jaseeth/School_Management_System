import api from "./axios";

export const studentEnrollmentsApi = {

    assignSubject(data) {
        return api.post(
            "/student-enrollments/subjects/assign",
            data
        );
    },
};