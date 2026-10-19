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

    const roles = user?.roles || [];
    if (roles.includes("Student") || roles.includes("Parent")) return <Navigate to="/" replace />;
    const leaders = ["Admin", "Principal", "Deputy Principal"];
    const path = location.pathname;
    let allowed = null;
    if (path === "/backups") allowed = ["Admin"];
    if (path === "/school-settings") allowed = leaders;
    if (path === "/students/add" || ["/staff", "/parents", "/academic-setup", "/teacher-assignments", "/timetable", "/promotion", "/audit-logs"].includes(path)) allowed = leaders;
    if (path === "/attendance" || path === "/reports" || path === "/staff-attendance") allowed = [...leaders, "Section Head"];
    if (path === "/section-head-assignments" || path === "/staff-leave-allowance" || path.endsWith("/reset-password")) allowed = ["Admin"];
    if (path.startsWith("/teacher/")) allowed = ["Teacher"];
    if (path.startsWith("/section-head/")) allowed = ["Section Head"];
    if (![...leaders, "Section Head", "Teacher"].some(role => roles.includes(role)) && !["/dashboard", "/staff/my-profile", "/staff-leave", "/school-calendar", "/notifications", "/change-password"].includes(path)) allowed = [];
    if (allowed && !allowed.some(role => roles.includes(role))) return <div className="p-8"><h1 className="text-xl font-bold">Access restricted</h1><p className="mt-3">Your account does not have access to this page.</p><a className="mt-4 inline-block text-blue-700" href="/dashboard">Back to dashboard</a></div>;
    return children;
}
