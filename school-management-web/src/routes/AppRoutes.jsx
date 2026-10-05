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

import ParentLoginPage from "../pages/parents/ParentLoginPage";
import ParentDashboardPage from "../pages/parents/ParentDashboardPage";
import ParentProtectedRoute from "./ParentProtectedRoute";
import ParentAttendancePage from "../pages/parents/ParentAttendancePage";
import ParentResultsPage from "../pages/parents/ParentResultsPage";
import ParentAcademicProfilePage from "../pages/parents/ParentAcademicProfilePage";
import ParentNotificationsPage from "../pages/parents/ParentNotificationsPage";
import ParentChangePasswordPage from "../pages/parents/ParentChangePasswordPage";
import ParentProfilePage from "../pages/parents/ParentProfilePage";

import StaffProfilePage from "../pages/staff/StaffProfilePage";
import StaffForgotPasswordPage from "../pages/auth/StaffForgotPasswordPage";
import StaffForcedChangePasswordPage from "../pages/auth/StaffForcedChangePasswordPage";

import PortalLandingPage from "../pages/public/PortalLandingPage";

import TeacherTimetablePage from "../pages/teachers/TeacherTimetablePage";
import TeacherDailyClassesPage from "../pages/teachers/TeacherDailyClassesPage";
import TeacherAttendancePage from "../pages/teachers/TeacherAttendancePage";
import TeacherMarksPage from "../pages/teachers/TeacherMarksPage";

import SectionHeadAttendanceApprovalsPage from "../pages/sectionHeads/SectionHeadAttendanceApprovalsPage";
import SectionHeadMarksReviewPage from "../pages/sectionHeads/SectionHeadMarksReviewPage";

import ResultsPublishingPage from "../pages/results/ResultsPublishingPage";

export default function AppRoutes() {
    return (
        <Routes>

            {/* ================================================
                PUBLIC ROUTES
            ================================================ */}

            <Route path="/" element={<PortalLandingPage />} />

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

                <Route path="/staff/my-profile" element={<StaffProfilePage />} />

                <Route
                    path="/teacher/timetable"
                    element={<TeacherTimetablePage />}
                />

                <Route
                    path="/teacher/today"
                    element={<TeacherDailyClassesPage />}
                />

                <Route
                    path="/teacher/attendance"
                    element={<TeacherAttendancePage />}
                />

                <Route
                    path="/section-head/attendance-approvals"
                    element={<SectionHeadAttendanceApprovalsPage />}
                />

                <Route
                    path="/teacher/marks"
                    element={<TeacherMarksPage />}
                />

                <Route
                    path="/section-head/marks-review"
                    element={<SectionHeadMarksReviewPage />}
                />

                <Route
                    path="/results/publishing"
                    element={<ResultsPublishingPage />}
                />

                {/*
                    FUTURE PAGES MUST ALSO GO HERE
                */}

            </Route>

            {/* ================================================
                DEFAULT ROUTES
            ================================================ */}



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

            <Route path="/parent/login" element={<ParentLoginPage />} />

            <Route
                path="/change-password"
                element={
                    <ProtectedRoute>
                        <StaffForcedChangePasswordPage />
                    </ProtectedRoute>
                }
            />

            <Route
                path="/parent/dashboard"
                element={
                    <ParentProtectedRoute>
                        <ParentDashboardPage />
                    </ParentProtectedRoute>
                }
            />

            <Route
                path="/parent/children/:studentId/attendance"
                element={
                    <ParentProtectedRoute>
                        <ParentAttendancePage />
                    </ParentProtectedRoute>
                }
            />

            <Route
                path="/parent/children/:studentId/results"
                element={
                    <ParentProtectedRoute>
                        <ParentResultsPage />
                    </ParentProtectedRoute>
                }
            />

            <Route
                path="/parent/children/:studentId/academic-profile"
                element={
                    <ParentProtectedRoute>
                        <ParentAcademicProfilePage />
                    </ParentProtectedRoute>
                }
            />

            <Route
                path="/parent/notifications"
                element={
                    <ParentProtectedRoute>
                        <ParentNotificationsPage />
                    </ParentProtectedRoute>
                }
            />

            <Route
                path="/parent/change-password"
                element={
                    <ParentProtectedRoute>
                        <ParentChangePasswordPage />
                    </ParentProtectedRoute>
                }
            />

            <Route
                path="/parent/profile"
                element={
                    <ParentProtectedRoute>
                        <ParentProfilePage />
                    </ParentProtectedRoute>
                }
            />

            <Route
                path="/staff/forgot-password"
                element={<StaffForgotPasswordPage />}
            />

        </Routes>
    );
}