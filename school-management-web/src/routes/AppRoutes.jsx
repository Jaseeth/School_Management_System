import { ArrowLeft } from "lucide-react";
import { lazy, Suspense } from "react";
const SchoolSettingsPage = lazy(() => import("../pages/management/SchoolSettingsPage"));
const ParentSchedulePage = lazy(() => import("../pages/parents/ParentSchedulePage"));
const AttendanceOverviewPage = lazy(() => import("../pages/management/AttendanceOverviewPage"));
const AuditLogsPage = lazy(() => import("../pages/management/AuditLogsPage"));
const PromotionPage = lazy(() => import("../pages/academic/PromotionPage"));
const StaffLeavePage = lazy(() => import("../pages/staff/StaffLeavePage"));
const StudentLeadershipPage = lazy(() => import("../pages/students/StudentLeadershipPage"));
const StaffAttendancePage = lazy(() => import("../pages/staff/StaffAttendancePage"));
const StaffLeavePolicy = lazy(() => import("../pages/staff/StaffLeavePolicy"));
const SchoolCalendarPage = lazy(() => import("../pages/academic/SchoolCalendarPage"));
import {
    Navigate,
    Link,
    Route,
    Routes,
} from "react-router-dom";

const LoginPage = lazy(() => import("../pages/auth/LoginPage"));
const StudentRegistrationPage = lazy(() => import("../pages/students/StudentRegistrationPage"));

const DashboardPage = lazy(() => import("../pages/dashboard/DashboardPage"));
const StudentsPage = lazy(() => import("../pages/students/StudentsPage"));
const StudentDetailsPage = lazy(() => import("../pages/students/StudentDetailsPage"));
const AddStudentPage = lazy(() => import("../pages/students/AddStudentPage"));

import ProtectedRoute from "./ProtectedRoute";
import DashboardLayout from "../components/layout/DashboardLayout";

const StudentLoginPage = lazy(() => import("../pages/students/StudentLoginPage"));
const StudentDashboardPage = lazy(() => import("../pages/students/StudentDashboardPage"));
import StudentProtectedRoute from "./StudentProtectedRoute";

const StudentProfilePage = lazy(() => import("../pages/students/StudentProfilePage"));
const StudentSubjectsPage = lazy(() => import("../pages/students/StudentSubjectsPage"));
const StudentAttendancePage = lazy(() => import("../pages/students/StudentAttendancePage"));
const StudentResultsPage = lazy(() => import("../pages/students/StudentResultsPage"));
const StudentTimetablePage = lazy(() => import("../pages/students/StudentTimetablePage"));
const StudentAnnouncementsPage = lazy(() => import("../pages/students/StudentAnnouncementsPage"));
const StudentNotificationsPage = lazy(() => import("../pages/students/StudentNotificationsPage"));
const StudentSpecialClassesPage = lazy(() => import("../pages/students/StudentSpecialClassesPage"));
const StudentDailySchedulePage = lazy(() => import("../pages/students/StudentDailySchedulePage"));
const StudentEnrollmentHistoryPage = lazy(() => import("../pages/students/StudentEnrollmentHistoryPage"));
const StudentGuardiansPage = lazy(() => import("../pages/students/StudentGuardiansPage"));
const StudentChangePasswordPage = lazy(() => import("../pages/students/StudentChangePasswordPage"));
const StudentForgotPasswordPage = lazy(() => import("../pages/students/StudentForgotPasswordPage"));
const AdminResetStudentPasswordPage = lazy(() => import("../pages/students/AdminResetStudentPasswordPage"));

const ParentLoginPage = lazy(() => import("../pages/parents/ParentLoginPage"));
const ParentForgotPasswordPage = lazy(() => import("../pages/parents/ParentForgotPasswordPage"));
const ParentDashboardPage = lazy(() => import("../pages/parents/ParentDashboardPage"));
import ParentProtectedRoute from "./ParentProtectedRoute";
const ParentAttendancePage = lazy(() => import("../pages/parents/ParentAttendancePage"));
const ParentResultsPage = lazy(() => import("../pages/parents/ParentResultsPage"));
const ParentAcademicProfilePage = lazy(() => import("../pages/parents/ParentAcademicProfilePage"));
const ParentNotificationsPage = lazy(() => import("../pages/parents/ParentNotificationsPage"));
const ParentChangePasswordPage = lazy(() => import("../pages/parents/ParentChangePasswordPage"));
const ParentProfilePage = lazy(() => import("../pages/parents/ParentProfilePage"));
const ParentsManagementPage = lazy(() => import("../pages/parents/ParentsManagementPage"));

const StaffProfilePage = lazy(() => import("../pages/staff/StaffProfilePage"));
const StaffManagementPage = lazy(() => import("../pages/staff/StaffManagementPage"));
const StaffForgotPasswordPage = lazy(() => import("../pages/auth/StaffForgotPasswordPage"));
const StaffForcedChangePasswordPage = lazy(() => import("../pages/auth/StaffForcedChangePasswordPage"));

const PortalLandingPage = lazy(() => import("../pages/public/PortalLandingPage"));

const TeacherTimetablePage = lazy(() => import("../pages/teachers/TeacherTimetablePage"));
const TeacherDailyClassesPage = lazy(() => import("../pages/teachers/TeacherDailyClassesPage"));
const TeacherAttendancePage = lazy(() => import("../pages/teachers/TeacherAttendancePage"));
const TeacherMarksPage = lazy(() => import("../pages/teachers/TeacherMarksPage"));

const SectionHeadAttendanceApprovalsPage = lazy(() => import("../pages/sectionHeads/SectionHeadAttendanceApprovalsPage"));
const SectionHeadMarksReviewPage = lazy(() => import("../pages/sectionHeads/SectionHeadMarksReviewPage"));
const SectionHeadAssignmentsPage = lazy(() => import("../pages/sectionHeads/SectionHeadAssignmentsPage"));

const ResultsPublishingPage = lazy(() => import("../pages/results/ResultsPublishingPage"));
const AnnouncementsPage = lazy(() => import("../pages/announcements/AnnouncementsPage"));
const ParentAnnouncementsPage = lazy(() => import("../pages/parents/ParentAnnouncementsPage"));
const SpecialClassesPage = lazy(() => import("../pages/specialClasses/SpecialClassesPage"));
const StaffNotificationsPage = lazy(() => import("../pages/notifications/StaffNotificationsPage"));
const ReportsPage = lazy(() => import("../pages/reports/ReportsPage"));

const ExamsTestsPage = lazy(() => import("../pages/exams/ExamsTestsPage"));
const AcademicSetupPage = lazy(() => import("../pages/academic/AcademicSetupPage"));
const TeacherAssignmentsManagementPage = lazy(() => import("../pages/teachers/TeacherAssignmentsManagementPage"));
const TimetableManagementPage = lazy(() => import("../pages/timetable/TimetableManagementPage"));

export default function AppRoutes() {
    return (
        <Suspense fallback={<div role="status" className="p-8 text-sm text-slate-600">Loading page…</div>}><Routes>

            {/* ================================================
                PUBLIC ROUTES
            ================================================ */}

            <Route path="/" element={<PortalLandingPage />} />
            <Route path="/parent/forgot-password" element={<ParentForgotPasswordPage />} />

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
                <Route path="/staff" element={<StaffManagementPage />} />
                <Route path="/academic-setup" element={<AcademicSetupPage />} />
                <Route path="/promotion" element={<PromotionPage />} />
                <Route path="/staff-leave" element={<StaffLeavePage />} />
                <Route path="/student-leadership" element={<StudentLeadershipPage />} />
                <Route path="/staff-attendance" element={<StaffAttendancePage />} />
                <Route path="/staff-leave-allowance" element={<StaffLeavePolicy manage />} />
                <Route path="/attendance" element={<AttendanceOverviewPage />} />
                <Route path="/audit-logs" element={<AuditLogsPage />} />
                <Route path="/school-calendar" element={<SchoolCalendarPage />} />
                <Route path="/school-settings" element={<SchoolSettingsPage />} />
                <Route path="/backups" element={<Navigate to="/school-settings?tab=backups" replace />} />

                <Route path="/teacher-assignments" element={<TeacherAssignmentsManagementPage />} />
                <Route path="/timetable" element={<TimetableManagementPage />} />

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
                <Route path="/marks-review" element={<SectionHeadMarksReviewPage />} />

                <Route
                    path="/section-head-assignments"
                    element={<SectionHeadAssignmentsPage />}
                />

                <Route
                    path="/results/publishing"
                    element={<ResultsPublishingPage />}
                />
                <Route path="/announcements" element={<AnnouncementsPage />} />
                <Route path="/special-classes" element={<SpecialClassesPage />} />
                <Route path="/notifications" element={<StaffNotificationsPage />} />
                <Route path="/reports" element={<ReportsPage />} />

                <Route
                    path="/parents"
                    element={<ParentsManagementPage />}
                />

                <Route
                    path="/exams"
                    element={<ExamsTestsPage />}
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
                    <div className="mx-auto max-w-xl p-10"><h1 className="text-2xl font-bold">Page not found</h1><p className="mt-3">This page is unavailable. Use your portal navigation to continue.</p><a className="mt-4 inline-block text-blue-700" href="/">Go to portal selection</a></div>
                }
            />

            <Route path="/student/school-calendar" element={<StudentProtectedRoute><div className="mx-auto max-w-7xl px-5 py-10"><Link to="/student/dashboard" className="portal-back-button mb-6"><ArrowLeft className="h-4 w-4" aria-hidden="true" />Back to Dashboard</Link><SchoolCalendarPage student /></div></StudentProtectedRoute>} />

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

            <Route path="/parent/children/:studentId/schedule" element={<ParentProtectedRoute><ParentSchedulePage /></ParentProtectedRoute>} />
            <Route path="/parent/school-calendar" element={<ParentProtectedRoute><div className="mx-auto max-w-7xl px-5 py-10"><Link to="/parent/dashboard" className="portal-back-button mb-6"><ArrowLeft className="h-4 w-4" aria-hidden="true" />Back to My Children</Link><SchoolCalendarPage parent /></div></ParentProtectedRoute>} />

            <Route path="/parent/login" element={<ParentLoginPage />} />
            <Route path="/parent/announcements" element={<ParentProtectedRoute><ParentAnnouncementsPage /></ParentProtectedRoute>} />

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

        </Routes></Suspense>
    );
}
