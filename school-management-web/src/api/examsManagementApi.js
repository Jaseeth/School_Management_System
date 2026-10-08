import api from "./axios";

export const examsManagementApi = {
    getTerms(academicYearId) {
        return api.get(`/exams/terms/${academicYearId}`);
    },

    createTerm(payload) {
        return api.post("/exams/terms", payload);
    },

    getExams(termId) {
        return api.get(`/exams/${termId}`);
    },

    createExam(payload) {
        return api.post("/exams", payload);
    },
};