import api from "./axios";

export const academicApi = {
    getAcademicYears() {
        return api.get(
            "/academic/academic-years"
        );
    },

    getSections() {
        return api.get(
            "/academic/sections"
        );
    },

    getGrades(sectionId) {
        return api.get(
            `/academic/grades/${sectionId}`
        );
    },

    getClasses(gradeId) {
        return api.get(
            `/academic/classes/${gradeId}`
        );
    },
};