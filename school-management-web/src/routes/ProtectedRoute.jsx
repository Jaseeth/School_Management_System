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
    const sectionLeaders = [...leaders, "Section Head"];
    const teachingStaff = ["Section Head", "Teacher"];
    const allStaff = [...leaders, "Section Head", "Teacher"];
    const path = location.pathname;
    // Keep route access aligned with the selected workspace. The account token can
    // contain several staff roles, but the active workspace intentionally exposes
    // only one role to the UI and its route guards.
    let allowed = [];
    if (path === "/dashboard" || path === "/staff/my-profile" || path === "/change-password") allowed = allStaff;
    else if (path === "/backups" || path.endsWith("/reset-password")) allowed = ["Admin"];
    else if (["/school-settings", "/staff", "/parents", "/academic-setup", "/promotion", "/teacher-assignments", "/timetable", "/audit-logs", "/staff-leave-allowance", "/section-head-assignments"].includes(path)) {
        allowed = path === "/section-head-assignments" || path === "/staff-leave-allowance" ? ["Admin", "Principal"] : path === "/backups" || path === "/audit-logs" ? ["Admin"] : leaders;
    }
    else if (path === "/students/add") allowed = leaders;
    else if (path === "/students" || /^\/students\/[^/]+$/.test(path)) allowed = allStaff;
    else if (["/attendance", "/reports", "/staff-attendance"].includes(path)) allowed = sectionLeaders;
    else if (path === "/marks-review" || path === "/section-head/marks-review") allowed = teachingStaff;
    else if (path.startsWith("/teacher/")) allowed = ["Teacher"];
    else if (path.startsWith("/section-head/")) allowed = ["Section Head"];
    else if (["/staff-leave", "/school-calendar", "/notifications", "/announcements", "/special-classes", "/student-leadership", "/exams", "/results/publishing"].includes(path)) allowed = allStaff;
    if (allowed && !allowed.some(role => roles.includes(role))) return <div className="p-8"><h1 className="text-xl font-bold">Access restricted</h1><p className="mt-3">Your account does not have access to this page.</p><a className="mt-4 inline-block text-blue-700" href="/dashboard">Back to dashboard</a></div>;
    return children;
}
