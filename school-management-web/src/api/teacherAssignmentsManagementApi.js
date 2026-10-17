import api from "./axios";

export const teacherAssignmentsManagementApi = {
    getSubjectAssignments() {
        return api.get("/teacher-assignments", { params: { includeInactive: true } });
    },
    assignSubject(data) {
        return api.post("/teacher-assignments", data);
    },
    deactivateSubject(id) {
        return api.delete(`/teacher-assignments/${id}`);
    },
    getClassTeachers() {
        return api.get("/class-teacher", { params: { includeInactive: true } });
    },
    assignClassTeacher(data) {
        return api.post("/class-teacher/assign", data);
    },
};
