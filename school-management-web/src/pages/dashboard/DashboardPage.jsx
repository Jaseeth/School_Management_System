import {
    ArrowUpRight,
    BookOpen,
    CalendarDays,
    ClipboardCheck,
    GraduationCap,
    Layers3,
    School,
    Users,
} from "lucide-react";

import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../../context/AuthContext";
import { dashboardApi } from "../../api/dashboardApi";

export default function DashboardPage() {
    const { user } = useAuth();

    const [dashboardData, setDashboardData] =
        useState(null);

    const [loading, setLoading] =
        useState(true);

    const [error, setError] =
        useState("");

    useEffect(() => {
        const loadDashboard = async () => {
            try {
                setLoading(true);
                setError("");

                let response;

                if (user?.roles?.includes("Admin")) {
                    response =
                        await dashboardApi.getAdminSummary();
                } else if (
                    user?.roles?.includes("Principal") ||
                    user?.roles?.includes(
                        "Deputy Principal"
                    )
                ) {
                    response =
                        await dashboardApi.getManagementSummary();
                } else if (
                    user?.roles?.includes("Section Head")
                ) {
                    response =
                        await dashboardApi.getSectionHeadSummary();
                } else if (
                    user?.roles?.includes("Teacher")
                ) {
                    response =
                        await dashboardApi.getTeacherSummary();
                } else if (
                    user?.roles?.includes("Student")
                ) {
                    response =
                        await dashboardApi.getStudentSummary();
                } else {
                    throw new Error(
                        "No dashboard is configured for this role."
                    );
                }

                setDashboardData(
                    response.data
                );
            } catch (err) {
                console.error(
                    "DASHBOARD ERROR:",
                    err
                );

                setError(
                    err?.response?.data?.message ||
                    err?.message ||
                    "Unable to load dashboard."
                );
            } finally {
                setLoading(false);
            }
        };

        loadDashboard();
    }, [user]);

    const cards = useMemo(() => {
        if (!dashboardData) {
            return [];
        }

        // ========================================================
        // ADMIN
        // ========================================================

        if (user?.roles?.includes("Admin")) {
            return [
                {
                    title: "Students",
                    value:
                        dashboardData.totalStudents ??
                        0,
                    subtitle:
                        "Total active students",
                    icon: GraduationCap,
                },
                {
                    title: "Staff",
                    value:
                        dashboardData.totalStaff ??
                        0,
                    subtitle:
                        "Active staff members",
                    icon: Users,
                },
                {
                    title: "Classes",
                    value:
                        dashboardData.totalClasses ??
                        0,
                    subtitle:
                        "Active classes",
                    icon: School,
                },
                {
                    title: "Subjects",
                    value:
                        dashboardData.totalSubjects ??
                        0,
                    subtitle:
                        "Active subjects",
                    icon: BookOpen,
                },
            ];
        }

        // ========================================================
        // PRINCIPAL / DEPUTY PRINCIPAL
        // ========================================================

        if (
            user?.roles?.includes("Principal") ||
            user?.roles?.includes(
                "Deputy Principal"
            )
        ) {
            return [
                {
                    title: "Students",
                    value:
                        dashboardData.totalStudents ??
                        0,
                    subtitle:
                        "Total active students",
                    icon: GraduationCap,
                },
                {
                    title: "Staff",
                    value:
                        dashboardData.totalStaff ??
                        0,
                    subtitle:
                        "Active staff members",
                    icon: Users,
                },
                {
                    title: "Classes",
                    value:
                        dashboardData.totalClasses ??
                        0,
                    subtitle:
                        "Active classes",
                    icon: School,
                },
                {
                    title: "Pending Marks",
                    value:
                        dashboardData.pendingMarksReviewCount ??
                        0,
                    subtitle:
                        "Waiting for review",
                    icon: ClipboardCheck,
                },
            ];
        }

        // ========================================================
        // SECTION HEAD
        // ========================================================

        if (
            user?.roles?.includes(
                "Section Head"
            )
        ) {
            return [
                {
                    title: "Students",
                    value:
                        dashboardData.totalStudents ??
                        0,
                    subtitle:
                        "Students in section",
                    icon: GraduationCap,
                },
                {
                    title: "Teachers",
                    value:
                        dashboardData.totalTeachers ??
                        0,
                    subtitle:
                        "Teachers in section",
                    icon: Users,
                },
                {
                    title: "Classes",
                    value:
                        dashboardData.totalClasses ??
                        0,
                    subtitle:
                        "Classes in section",
                    icon: School,
                },
                {
                    title: "Today's Schedule",
                    value:
                        dashboardData.todayTimetableCount ??
                        0,
                    subtitle:
                        "Timetable sessions today",
                    icon: CalendarDays,
                },
            ];
        }

        // ========================================================
        // TEACHER
        // ========================================================

        if (
            user?.roles?.includes("Teacher")
        ) {
            return [
                {
                    title: "Classes",
                    value:
                        dashboardData.assignedClassCount ??
                        0,
                    subtitle:
                        "Assigned classes",
                    icon: School,
                },
                {
                    title: "Subjects",
                    value:
                        dashboardData.assignedSubjectCount ??
                        0,
                    subtitle:
                        "Assigned subjects",
                    icon: BookOpen,
                },
                {
                    title: "Today's Schedule",
                    value:
                        dashboardData.todayTimetableCount ??
                        0,
                    subtitle:
                        "Timetable sessions today",
                    icon: CalendarDays,
                },
                {
                    title: "Notifications",
                    value:
                        dashboardData.unreadNotificationCount ??
                        0,
                    subtitle:
                        "Unread notifications",
                    icon: Layers3,
                },
            ];
        }

        // ========================================================
        // STUDENT
        // ========================================================

        if (
            user?.roles?.includes("Student")
        ) {
            return [
                {
                    title: "Today's Classes",
                    value:
                        dashboardData.todayTimetableCount ??
                        0,
                    subtitle:
                        "Scheduled classes today",
                    icon: CalendarDays,
                },
                {
                    title: "Special Classes",
                    value:
                        dashboardData.todaySpecialClassCount ??
                        0,
                    subtitle:
                        "Special classes today",
                    icon: School,
                },
                {
                    title: "Results",
                    value:
                        dashboardData.publishedResultCount ??
                        0,
                    subtitle:
                        "Published results",
                    icon: BookOpen,
                },
                {
                    title: "Attendance",
                    value:
                        `${dashboardData.attendancePercentage ??
                        0
                        }%`,
                    subtitle:
                        "Current attendance",
                    icon: ClipboardCheck,
                },
            ];
        }

        return [];
    }, [
        dashboardData,
        user,
    ]);

    // ============================================================
    // LOADING
    // ============================================================

    if (loading) {
        return (
            <div className="flex min-h-[420px] items-center justify-center">
                <div className="h-10 w-10 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />
            </div>
        );
    }

    return (
        <div>
            {/* ====================================================
                WELCOME
            ==================================================== */}

            <div className="mb-8">
                <p className="text-sm font-medium text-blue-600">
                    Dashboard
                </p>

                <h2 className="mt-1 text-2xl font-bold tracking-tight text-slate-950 sm:text-3xl">
                    Welcome back,{" "}
                    {user?.fullName}
                </h2>

                <p className="mt-2 text-sm text-slate-500">
                    Here's an overview of your school today.
                </p>
            </div>

            {/* ====================================================
                ERROR
            ==================================================== */}

            {error && (
                <div className="mb-6 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                    {error}
                </div>
            )}

            {/* ====================================================
                TOP CARDS
            ==================================================== */}

            <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
                {cards.map(
                    ({
                        title,
                        value,
                        subtitle,
                        icon: Icon,
                    }) => (
                        <div
                            key={title}
                            className="group rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md"
                        >
                            <div className="flex items-start justify-between">
                                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                                    <Icon className="h-5 w-5" />
                                </div>

                                <ArrowUpRight className="h-4 w-4 text-slate-300 transition group-hover:text-blue-600" />
                            </div>

                            <p className="mt-5 text-sm font-medium text-slate-500">
                                {title}
                            </p>

                            <p className="mt-1 text-3xl font-bold tracking-tight text-slate-950">
                                {value}
                            </p>

                            <p className="mt-2 text-xs text-slate-400">
                                {subtitle}
                            </p>
                        </div>
                    )
                )}
            </div>

            {/* ====================================================
                ADMIN / MANAGEMENT LOWER SECTION
            ==================================================== */}

            {(user?.roles?.includes("Admin") ||
                user?.roles?.includes("Principal") ||
                user?.roles?.includes(
                    "Deputy Principal"
                )) && (
                    <div className="mt-8 grid gap-6 xl:grid-cols-3">

                        {/* ============================================
                        ACADEMIC PERIOD
                    ============================================ */}

                        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm xl:col-span-2">
                            <div>
                                <h3 className="font-semibold text-slate-950">
                                    Academic Period
                                </h3>

                                <p className="mt-1 text-sm text-slate-500">
                                    Current academic year and term
                                </p>
                            </div>

                            <div className="mt-6 grid gap-4 sm:grid-cols-2">

                                {/* ACADEMIC YEAR */}

                                <div className="rounded-xl border border-slate-100 bg-slate-50 p-5">
                                    <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-400">
                                        Academic Year
                                    </p>

                                    <p className="mt-3 text-xl font-bold text-slate-950">
                                        {dashboardData
                                            ?.activeAcademicYearName ??
                                            "Not available"}
                                    </p>
                                </div>

                                {/* ACADEMIC TERM */}

                                <div className="rounded-xl border border-slate-100 bg-slate-50 p-5">
                                    <p className="text-xs font-semibold uppercase tracking-[0.08em] text-slate-400">
                                        Academic Term
                                    </p>

                                    <p className="mt-3 text-xl font-bold text-slate-950">
                                        {dashboardData
                                            ?.activeAcademicTermName ??
                                            "Not available"}
                                    </p>
                                </div>
                            </div>
                        </div>

                        {/* ============================================
                        ATTENTION NEEDED
                    ============================================ */}

                        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                            <div className="flex items-start justify-between">
                                <div>
                                    <h3 className="font-semibold text-slate-950">
                                        Attention Needed
                                    </h3>

                                    <p className="mt-1 text-sm text-slate-500">
                                        Items requiring review
                                    </p>
                                </div>

                                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-50 text-amber-600">
                                    <ClipboardCheck className="h-5 w-5" />
                                </div>
                            </div>

                            <div className="mt-6 space-y-3">

                                {/* PENDING LEAVE */}

                                <div className="flex items-center justify-between rounded-xl border border-slate-200 px-4 py-4 transition hover:bg-slate-50">
                                    <div>
                                        <p className="text-sm font-medium text-slate-800">
                                            Pending Leave Requests
                                        </p>

                                        <p className="mt-1 text-xs text-slate-400">
                                            Waiting for review
                                        </p>
                                    </div>

                                    <span className="min-w-10 rounded-lg bg-amber-50 px-3 py-1.5 text-center text-sm font-semibold text-amber-700">
                                        {dashboardData
                                            ?.pendingLeaveRequestCount ??
                                            0}
                                    </span>
                                </div>

                                {/* ABSENT STAFF */}

                                <div className="flex items-center justify-between rounded-xl border border-slate-200 px-4 py-4 transition hover:bg-slate-50">
                                    <div>
                                        <p className="text-sm font-medium text-slate-800">
                                            Absent Staff Today
                                        </p>

                                        <p className="mt-1 text-xs text-slate-400">
                                            Current staff absences
                                        </p>
                                    </div>

                                    <span className="min-w-10 rounded-lg bg-red-50 px-3 py-1.5 text-center text-sm font-semibold text-red-700">
                                        —
                                    </span>
                                </div>

                                {/* SPECIAL CLASSES TODAY */}

                                <div className="flex items-center justify-between rounded-xl border border-slate-200 px-4 py-4 transition hover:bg-slate-50">
                                    <div>
                                        <p className="text-sm font-medium text-slate-800">
                                            Special Classes Today
                                        </p>

                                        <p className="mt-1 text-xs text-slate-400">
                                            Approved sessions
                                        </p>
                                    </div>

                                    <span className="min-w-10 rounded-lg bg-blue-50 px-3 py-1.5 text-center text-sm font-semibold text-blue-700">
                                        {dashboardData
                                            ?.todaySpecialClassCount ??
                                            0}
                                    </span>
                                </div>

                                {/* UPCOMING SPECIAL CLASSES */}

                                <div className="flex items-center justify-between rounded-xl border border-slate-200 px-4 py-4 transition hover:bg-slate-50">
                                    <div>
                                        <p className="text-sm font-medium text-slate-800">
                                            Upcoming Special Classes
                                        </p>

                                        <p className="mt-1 text-xs text-slate-400">
                                            Scheduled ahead
                                        </p>
                                    </div>

                                    <span className="min-w-10 rounded-lg bg-indigo-50 px-3 py-1.5 text-center text-sm font-semibold text-indigo-700">
                                        {dashboardData
                                            ?.upcomingSpecialClassCount ??
                                            0}
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                )}

            {/* ====================================================
                NON-ADMIN SUMMARY AREA
            ==================================================== */}

            {!user?.roles?.includes("Admin") &&
                !user?.roles?.includes("Principal") &&
                !user?.roles?.includes(
                    "Deputy Principal"
                ) && (
                    <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                        <h3 className="font-semibold text-slate-950">
                            Current Academic Period
                        </h3>

                        <p className="mt-1 text-sm text-slate-500">
                            Current academic information
                        </p>

                        <div className="mt-6 grid gap-4 sm:grid-cols-2">
                            <div className="rounded-xl bg-slate-50 p-5">
                                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                                    Academic Year
                                </p>

                                <p className="mt-3 font-semibold text-slate-950">
                                    {dashboardData
                                        ?.activeAcademicYearName ??
                                        "Not available"}
                                </p>
                            </div>

                            <div className="rounded-xl bg-slate-50 p-5">
                                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                                    Academic Term
                                </p>

                                <p className="mt-3 font-semibold text-slate-950">
                                    {dashboardData
                                        ?.activeAcademicTermName ??
                                        "Not available"}
                                </p>
                            </div>
                        </div>
                    </div>
                )}
        </div>
    );
}