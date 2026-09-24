import { createContext, useContext, useEffect, useState } from "react";
import { authService } from "../services/authService";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const savedUser = authService.getUser();

        setUser(savedUser);
        setLoading(false);
    }, []);

    const login = async (email, password) => {
        const result = await authService.login(
            email,
            password
        );

        setUser({
            fullName: result.fullName,
            email: result.email,
            roles: result.roles,
            mustChangePassword:
                result.mustChangePassword,
            expiresAt:
                result.expiresAt,
        });

        return result;
    };

    const logout = () => {
        authService.logout();
        setUser(null);
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
        throw new Error(
            "useAuth must be used inside AuthProvider"
        );
    }

    return context;
}