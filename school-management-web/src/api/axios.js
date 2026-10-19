import { getPortalToken } from "../services/portalSession";
import axios from "axios";

const api = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL || (import.meta.env.DEV ? "/api" : "https://localhost:7063/api"),
    headers: {
        "Content-Type": "application/json",
    },
});

api.interceptors.request.use(
    (config) => {
        const portal = window.location.pathname.startsWith("/student/") ? "student" : window.location.pathname.startsWith("/parent/") ? "parent" : "staff";
        const token = getPortalToken(portal);

        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }

        return config;
    },
    (error) => Promise.reject(error)
);

export default api;
