import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { authService } from "../services/authService";

const AuthContext = createContext(null);
const STAFF_WORKSPACE_ROLES = ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"];

function workspaceValueForRole(role) {
    return role === "Teacher" ? "teacher" : role;
}

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);
    const [storedWorkspaceRole, setWorkspaceRole] = useState(() => localStorage.getItem("staffWorkspaceRole") || "teacher");

    const setActiveWorkspace = (role) => {
        localStorage.setItem("staffWorkspaceRole", role);
        setWorkspaceRole(role);
    };

    const baseRoles = user?.baseRoles || user?.roles || [];
    const workspaceOptions = useMemo(() => {
        const roles = STAFF_WORKSPACE_ROLES.filter(role => baseRoles.includes(role));
        const options = roles.map(role => ({ value: workspaceValueForRole(role), role, label: role }));
        if (!options.length && user) options.push({ value: "staff", role: null, label: "Staff" });
        return options;
    }, [baseRoles, user]);
    const selectedWorkspace = workspaceOptions.find(option => option.value === storedWorkspaceRole)
        || workspaceOptions.find(option => option.value === "teacher")
        || workspaceOptions[0]
        || { value: "staff", role: null, label: "Staff" };
    const workspaceUser = useMemo(() => {
        if (!user) return null;
        return {
            ...user,
            baseRoles,
            roles: selectedWorkspace.role ? [selectedWorkspace.role] : [],
        };
    }, [user, baseRoles, selectedWorkspace]);

    useEffect(() => {
        const syncSession = () => {
            const saved = authService.getUser();
            const baseUser = saved ? { ...saved, baseRoles: saved.baseRoles || saved.roles || [] } : null;
            setUser(baseUser);
            if (baseUser) {
                const roles = baseUser.baseRoles || baseUser.roles || [];
                const choices = STAFF_WORKSPACE_ROLES.filter(role => roles.includes(role)).map(workspaceValueForRole);
                const savedWorkspace = localStorage.getItem("staffWorkspaceRole");
                if (!choices.includes(savedWorkspace)) {
                    setActiveWorkspace(choices.includes("teacher") ? "teacher" : choices[0] || "staff");
                }
            }
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
            baseRoles: result.roles,
            mustChangePassword: result.mustChangePassword,
            expiresAt: result.expiresAt,
        });
        const availableWorkspaces = STAFF_WORKSPACE_ROLES.filter(role => result.roles?.includes(role)).map(workspaceValueForRole);
        const savedWorkspace = localStorage.getItem("staffWorkspaceRole");
        if (!availableWorkspaces.includes(savedWorkspace)) {
            setActiveWorkspace(availableWorkspaces.includes("teacher") ? "teacher" : availableWorkspaces[0] || "staff");
        }
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
        return workspaceUser?.roles?.includes(role) ?? false;
    };

    const isAuthenticated = !!user;

    return (
        <AuthContext.Provider
            value={{
                user: workspaceUser,
                activeWorkspaceRole: selectedWorkspace.value,
                workspaceOptions,
                setActiveWorkspace,
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
