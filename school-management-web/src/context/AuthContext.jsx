import { createContext, useContext, useEffect, useState } from "react";
import { authService } from "../services/authService";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const syncSession = () => {
            setUser(authService.getUser());
            setLoading(false);
        };
        syncSession();
        window.addEventListener("storage", syncSession);
        window.addEventListener("auth-session-changed", syncSession);
        window.addEventListener("focus", syncSession);
        return () => {
            window.removeEventListener("storage", syncSession);
            window.removeEventListener("auth-session-changed", syncSession);
            window.removeEventListener("focus", syncSession);
        };
    }, []);

    const login = async (email, password) => {
        const result = await authService.login(email, password);

        setUser({
            fullName: result.fullName,
            email: result.email,
            roles: result.roles,
            mustChangePassword: result.mustChangePassword,
            expiresAt: result.expiresAt,
        });

        return result;
    };

    const logout = async () => {
        await authService.logout();
        setUser(null);
    };

    const updateAccountDetails = (details) => {
        setUser((current) => {
            if (!current) return current;

            const updated = {
                ...current,
                fullName: details.fullName,
                email: details.email,
            };

            localStorage.setItem("authUser", JSON.stringify(updated));
            return updated;
        });
    };

    const clearPasswordChangeRequirement = () => {
        setUser((current) => {
            if (!current) return current;

            const updated = {
                ...current,
                mustChangePassword: false,
            };

            localStorage.setItem("authUser", JSON.stringify(updated));
            return updated;
        });
    };

    const hasRole = (role) => {
        return user?.roles?.includes(role) ?? false;
    };

    const isAuthenticated = !!user;

    return (
        <AuthContext.Provider
            value={{
                user,
                loading,
                isAuthenticated,
                login,
                logout,
                updateAccountDetails,
                clearPasswordChangeRequirement,
                hasRole,
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    const context = useContext(AuthContext);

    if (!context) {
        throw new Error("useAuth must be used inside AuthProvider");
    }

    return context;
}
