import { getPortalToken } from "../services/portalSession";
import { Navigate } from "react-router-dom";

export default function ParentProtectedRoute({ children }) {
    const token = getPortalToken("parent");

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