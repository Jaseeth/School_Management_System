import {
    Navigate,
    Route,
    Routes,
} from "react-router-dom";

import LoginPage from "../pages/auth/LoginPage";
import DashboardPage from "../pages/dashboard/DashboardPage";
import StudentsPage from "../pages/students/StudentsPage";

import ProtectedRoute from "./ProtectedRoute";
import DashboardLayout from "../components/layout/DashboardLayout";
import StudentDetailsPage from "../pages/students/StudentDetailsPage";
import AddStudentPage from "../pages/students/AddStudentPage";

export default function AppRoutes() {
    return (
        <Routes>

            {/* ================================================
                PUBLIC ROUTES
            ================================================ */}

            <Route
                path="/login"
                element={<LoginPage />}
            />

            {/* ================================================
                AUTHENTICATED APPLICATION
            ================================================ */}

            <Route
                element={
                    <ProtectedRoute>
                        <DashboardLayout />
                    </ProtectedRoute>
                }
            >

                <Route
                    path="/dashboard"
                    element={<DashboardPage />}
                />

                <Route
                    path="/students"
                    element={<StudentsPage />}
                />

                <Route
                    path="/students/add"
                    element={<AddStudentPage />}
                />

                <Route
                    path="/students/:id"
                    element={<StudentDetailsPage />}
                />

                {/*
                    FUTURE PAGES MUST ALSO GO HERE

                    Example:

                    <Route
                        path="/parents"
                        element={<ParentsPage />}
                    />

                    <Route
                        path="/staff"
                        element={<StaffPage />}
                    />

                    <Route
                        path="/attendance"
                        element={<AttendancePage />}
                    />

                    <Route
                        path="/results"
                        element={<ResultsPage />}
                    />

                    <Route
                        path="/timetable"
                        element={<TimetablePage />}
                    />

                    <Route
                        path="/announcements"
                        element={<AnnouncementsPage />}
                    />

                    <Route
                        path="/reports"
                        element={<ReportsPage />}
                    />

                    <Route
                        path="/audit-logs"
                        element={<AuditLogsPage />}
                    />
                */}

            </Route>

            {/* ================================================
                DEFAULT ROUTES
            ================================================ */}

            <Route
                path="/"
                element={
                    <Navigate
                        to="/dashboard"
                        replace
                    />
                }
            />

            <Route
                path="*"
                element={
                    <Navigate
                        to="/dashboard"
                        replace
                    />
                }
            />

        </Routes>
    );
}