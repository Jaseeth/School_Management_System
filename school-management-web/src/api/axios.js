import { getPortalToken } from "../services/portalSession";
import axios from "axios";
const api = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL || "/api", headers: { "Content-Type": "application/json" } });
const refreshing = new Map();
const portalFor = () => window.location.pathname.startsWith("/student/") ? "student" : window.location.pathname.startsWith("/parent/") ? "parent" : "staff";
const publicAuth = url => /\/auth\/(login|student-login|staff-forgot-password|parent-forgot-password)/i.test(url || "") || /\/student-registration\//i.test(url || "");
function saveSession(portal, oldToken, data) {
    if (getPortalToken(portal) !== oldToken || !data.token) return;
    localStorage.setItem(`${portal}AccessToken`, data.token);
    const userKey = portal === "staff" ? "authUser" : portal === "student" ? "studentUser" : "parentUser";
    try { const user = JSON.parse(localStorage.getItem(userKey)); if (user) localStorage.setItem(userKey, JSON.stringify({ ...user, expiresAt: data.expiresAt })); } catch { /* The portal guard handles malformed cached profiles. */ }
}
api.interceptors.request.use(async config => {
    const portal = ["staff", "student", "parent"].includes(config.portalOverride) ? config.portalOverride : portalFor(); config.portal = portal;
    if (publicAuth(config.url)) return config;
    let token = getPortalToken(portal);
    if (token && !config.url?.toLowerCase().endsWith("/auth/change-password")) {
        try {
            const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g,"+").replace(/_/g,"/")));
            const left = payload.exp * 1000 - Date.now();
            if (left > 0 && left < 5 * 60 * 1000) {
                if (!refreshing.has(portal)) refreshing.set(portal, axios.post(`${api.defaults.baseURL}/Auth/refresh`, {}, { headers:{Authorization:`Bearer ${token}`} }).then(r=>saveSession(portal, token, r.data)).finally(()=>refreshing.delete(portal)));
                await refreshing.get(portal); token = getPortalToken(portal);
            }
        } catch { /* The original API request provides the authoritative session error. */ }
    }
    if (token) config.headers.Authorization = `Bearer ${token}`;
    config.sessionToken = token;
    return config;
});
api.interceptors.response.use(response => {
    if (response.config.url?.toLowerCase().endsWith("/auth/change-password")) saveSession(response.config.portal, response.config.sessionToken, response.data);
    return response;
}, error => {
    if (error.response?.status === 401 && !publicAuth(error.config?.url) && error.config?.sessionToken)
        window.dispatchEvent(new CustomEvent("portal-session-expired", { detail:{ portal:error.config.portal, message:error.response?.data?.message || "Your session has ended. Sign in again to continue." } }));
    return Promise.reject(error);
});
export default api;
