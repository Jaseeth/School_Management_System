import api from "../api/axios";

const ACCESS_TOKEN_KEY = "accessToken";
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
                mustChangePassword:
                    data.mustChangePassword,
                expiresAt: data.expiresAt,
            })
        );

        return data;
    },

    logout() {
        localStorage.removeItem(
            ACCESS_TOKEN_KEY
        );

        localStorage.removeItem(
            USER_KEY
        );
    },

    getToken() {
        return localStorage.getItem(
            ACCESS_TOKEN_KEY
        );
    },

    getUser() {
        const user =
            localStorage.getItem(
                USER_KEY
            );

        if (!user) {
            return null;
        }

        try {
            return JSON.parse(user);
        } catch {
            return null;
        }
    },

    isAuthenticated() {
        const token =
            localStorage.getItem(
                ACCESS_TOKEN_KEY
            );

        return !!token;
    },

    hasRole(role) {
        const user =
            this.getUser();

        if (!user?.roles) {
            return false;
        }

        return user.roles.includes(role);
    },
};