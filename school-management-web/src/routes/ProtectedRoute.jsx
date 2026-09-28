import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function ProtectedRoute({ children }) {
    const location = useLocation();
    const { isAuthenticated, loading, user } = useAuth();

    if (loading) {
        return (
            <div className="flex min-h-screen items-center justify-center bg-slate-50">
                <div className="h-10 w-10 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />
            </div>
        );
    }

    if (!isAuthenticated) {
        return <Navigate to="/login" replace />;
    }

    if (
        user?.mustChangePassword &&
        location.pathname !== "/change-password"
    ) {
        return <Navigate to="/change-password" replace />;
    }

    if (
        !user?.mustChangePassword &&
        location.pathname === "/change-password"
    ) {
        return <Navigate to="/dashboard" replace />;
    }

    return children;
}