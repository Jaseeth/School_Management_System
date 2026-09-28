import { Navigate } from "react-router-dom";

export default function ParentProtectedRoute({ children }) {
    const token = localStorage.getItem("accessToken");

    try {
        const parent = JSON.parse(
            localStorage.getItem("parentUser") || "null"
        );

        if (token && parent?.roles?.includes("Parent")) {
            return children;
        }
    } catch {
        localStorage.removeItem("parentUser");
    }

    return <Navigate to="/parent/login" replace />;
}