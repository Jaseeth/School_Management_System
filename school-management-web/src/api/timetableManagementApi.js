import api from "./axios";

export const timetableManagementApi = {
    getIntervals() { return api.get("/timetable/intervals"); },
    createInterval(data) { return api.post("/timetable/intervals", data); },
    updateInterval(id, data) { return api.put(`/timetable/intervals/${id}`, data); },
    removeInterval(id) { return api.delete(`/timetable/intervals/${id}`); },
    getEntries() { return api.get("/timetable"); },
    create(data) { return api.post("/timetable", data); },
    update(id, data) { return api.put(`/timetable/${id}`, data); },
    deactivate(id) { return api.delete(`/timetable/${id}`); },
};
