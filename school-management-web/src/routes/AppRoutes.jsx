import {
    Navigate,
    Route,
    Routes,
} from "react-router-dom";

import LoginPage from "../pages/auth/LoginPage";
import StudentRegistrationPage from "../pages/students/StudentRegistrationPage";

import DashboardPage from "../pages/dashboard/DashboardPage";
import StudentsPage from "../pages/students/StudentsPage";
import StudentDetailsPage from "../pages/students/StudentDetailsPage";
import AddStudentPage from "../pages/students/AddStudentPage";

import ProtectedRoute from "./ProtectedRoute";
import DashboardLayout from "../components/layout/DashboardLayout";

import StudentLoginPage from "../pages/students/StudentLoginPage";
import StudentDashboardPage from "../pages/students/StudentDashboardPage";
import StudentProtectedRoute from "./StudentProtectedRoute";

import StudentProfilePage from "../pages/students/StudentProfilePage";
import StudentSubjectsPage from "../pages/students/StudentSubjectsPage";
import StudentAttendancePage from "../pages/students/StudentAttendancePage";
import StudentResultsPage from "../pages/students/StudentResultsPage";
import StudentTimetablePage from "../pages/students/StudentTimetablePage";
import StudentAnnouncementsPage from "../pages/students/StudentAnnouncementsPage";
import StudentNotificationsPage from "../pages/students/StudentNotificationsPage";
import StudentSpecialClassesPage from "../pages/students/StudentSpecialClassesPage";
import StudentDailySchedulePage from "../pages/students/StudentDailySchedulePage";
import StudentEnrollmentHistoryPage from "../pages/students/StudentEnrollmentHistoryPage";
import StudentGuardiansPage from "../pages/students/StudentGuardiansPage";
import StudentChangePasswordPage from "../pages/students/StudentChangePasswordPage";
import StudentForgotPasswordPage from "../pages/students/StudentForgotPasswordPage";
import AdminResetStudentPasswordPage from "../pages/students/AdminResetStudentPasswordPage";

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

            <Route
                path="/student/register"
                element={<StudentRegistrationPage />}
            />

            <Route
                path="/student/login"
                element={<StudentLoginPage />}
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

                <Route
                    path="/students/:id/reset-password"
                    element={<AdminResetStudentPasswordPage />}
                />

                {/*
                    FUTURE PAGES MUST ALSO GO HERE
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

            <Route
                path="/student/dashboard"
                element={
                    <StudentProtectedRoute>
                        <StudentDashboardPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/profile"
                element={
                    <StudentProtectedRoute>
                        <StudentProfilePage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/subjects"
                element={
                    <StudentProtectedRoute>
                        <StudentSubjectsPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/attendance"
                element={
                    <StudentProtectedRoute>
                        <StudentAttendancePage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/results"
                element={
                    <StudentProtectedRoute>
                        <StudentResultsPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/timetable"
                element={
                    <StudentProtectedRoute>
                        <StudentTimetablePage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/announcements"
                element={
                    <StudentProtectedRoute>
                        <StudentAnnouncementsPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/notifications"
                element={
                    <StudentProtectedRoute>
                        <StudentNotificationsPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/special-classes"
                element={
                    <StudentProtectedRoute>
                        <StudentSpecialClassesPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/daily-schedule"
                element={
                    <StudentProtectedRoute>
                        <StudentDailySchedulePage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/enrollment-history"
                element={
                    <StudentProtectedRoute>
                        <StudentEnrollmentHistoryPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/guardians"
                element={
                    <StudentProtectedRoute>
                        <StudentGuardiansPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/change-password"
                element={
                    <StudentProtectedRoute>
                        <StudentChangePasswordPage />
                    </StudentProtectedRoute>
                }
            />

            <Route
                path="/student/forgot-password"
                element={<StudentForgotPasswordPage />}
            />

        </Routes>
    );
}