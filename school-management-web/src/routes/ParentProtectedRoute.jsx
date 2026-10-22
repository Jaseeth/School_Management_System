import PortalHeader from "../components/layout/PortalHeader";
import { getPortalToken } from "../services/portalSession";
import { Navigate } from "react-router-dom";

export default function ParentProtectedRoute({ children }) {
    const token = getPortalToken("parent");

    try {
        const parent = JSON.parse(
            localStorage.getItem("parentUser") || "null"
        );

        if (token && parent?.roles?.includes("Parent")) {
            return <div className="min-h-screen bg-slate-50"><PortalHeader portal="parent" />{children}</div>;
        }
    } catch {
        localStorage.removeItem("parentUser");
    }

    return <Navigate to="/parent/login" replace />;
}