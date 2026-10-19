import { disableBrowserPush } from "./browserPush";
import api from "../api/axios";
import { getPortalToken } from "./portalSession";

const ACCESS_TOKEN_KEY = "staffAccessToken";
const USER_KEY = "authUser";

export const authService = {
    async login(email, password) {
        const response = await api.post("/Auth/login", {
            email,
            password,
        });

        const data = response.data;


        localStorage.setItem(
            ACCESS_TOKEN_KEY,
            data.token
        );

        localStorage.setItem(
            USER_KEY,
            JSON.stringify({
                fullName: data.fullName,
                email: data.email,
                roles: data.roles,
                mustChangePassword: data.mustChangePassword,
                expiresAt: data.expiresAt,
            })
        );

        window.dispatchEvent(new Event("auth-session-changed"));
        return data;
    },

    async logout() {
        await disableBrowserPush("staff").catch(() => {});
        localStorage.removeItem(ACCESS_TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
        window.dispatchEvent(new Event("auth-session-changed"));
    },

    getToken() {
        return getPortalToken("staff");
    },

    getUser() {
        const user = localStorage.getItem(USER_KEY);

        if (!user || !getPortalToken("staff")) {
            return null;
        }

        try {
            return JSON.parse(user);
        } catch {
            return null;
        }
    },

    isAuthenticated() {
        return !!getPortalToken("staff");
    },

    hasRole(role) {
        const user = this.getUser();
        return user?.roles?.includes(role) ?? false;
    },
};
