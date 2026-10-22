import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import "./index.css";
import App from "./App.jsx";
import { AuthProvider } from "./context/AuthContext.jsx";

const router = createBrowserRouter([{ path: "*", element: <AuthProvider><App /></AuthProvider> }]);

createRoot(
    document.getElementById("root")
).render(
    <StrictMode>
        <RouterProvider router={router} />
    </StrictMode>
);
