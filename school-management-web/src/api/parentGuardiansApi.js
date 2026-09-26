import api from "./axios";

export const parentGuardiansApi = {

    createParent(data) {
        return api.post(
            "/parent-guardians",
            data
        );
    },

    linkStudent(data) {
        return api.post(
            "/parent-guardians/link-student",
            data
        );
    },
};