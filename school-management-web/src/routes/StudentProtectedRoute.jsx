import PortalHeader from "../components/layout/PortalHeader";
import { getPortalToken } from "../services/portalSession";
import { Navigate, useLocation } from "react-router-dom";

export default function StudentProtectedRoute({ children }) {
    const location = useLocation();
    const token = getPortalToken("student");
    const storedStudent = localStorage.getItem("studentUser");

    if (!token || !storedStudent) {
        return <Navigate to="/student/login" replace />;
    }

    try {
        const student = JSON.parse(storedStudent);

        if (!student?.roles?.includes("Student")) {
            return <Navigate to="/student/login" replace />;
        }

        if (
            student.mustChangePassword === true &&
            location.pathname !== "/student/change-password"
        ) {
            return <Navigate to="/student/change-password" replace />;
        }
    } catch {
        localStorage.removeItem("studentAccessToken");
        localStorage.removeItem("studentUser");
        return <Navigate to="/student/login" replace />;
    }

    return <div className="min-h-screen bg-slate-50"><PortalHeader portal="student" />{children}</div>;
}