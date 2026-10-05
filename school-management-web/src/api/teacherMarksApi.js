import api from "./axios";

export const teacherMarksApi = {
    getMyAssignments() {
        return api.get("/teacher-assignments/my");
    },

    getTerms(academicYearId) {
        return api.get(`/exams/terms/${academicYearId}`);
    },

    getExams(termId) {
        return api.get(`/exams/${termId}`);
    },

    getEntry(examId, teacherAssignmentId) {
        return api.get("/marks/entry", {
            params: { examId, teacherAssignmentId },
        });
    },

    saveDraft(payload) {
        return api.post("/marks/draft", payload);
    },

    submit(examId, teacherAssignmentId) {
        return api.post("/marks/submit", null, {
            params: { examId, teacherAssignmentId },
        });
    },
};