import api from "./axios";

export const academicApi = {

    createSection(data) {
        return api.post("/academic/sections", data);
    },

    createGrade(data) {
        return api.post("/academic/grades", data);
    },

    createClass(data) {
        return api.post("/academic/classes", data);
    },

    createAcademicYear(data) {
        return api.post("/academic/academic-years", data);
    },

    updateAcademicYear(id, data) {
        return api.put(`/academic/academic-years/${id}`, data);
    },

    createSubject(data) {
        return api.post("/academic/subjects", data);
    },

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

    getSubjects() {
        return api.get(
            "/academic/subjects"
        );
    },
};
