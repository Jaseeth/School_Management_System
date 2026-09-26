import {
    Award,
    BookOpen,
    Bell,
    CalendarClock,
    CalendarDays,
    CalendarPlus,
    CalendarRange,
    GraduationCap,
    History,
    LockKeyhole,
    LogOut,
    Megaphone,
    UserRound,
    UsersRound,
} from "lucide-react";

import {
    useNavigate,
} from "react-router-dom";


export default function StudentDashboardPage() {

    const navigate =
        useNavigate();


    let student =
        null;


    try {

        student =
            JSON.parse(
                localStorage.getItem(
                    "studentUser"
                )
            );

    }
    catch {

        student =
            null;

    }


    const handleLogout =
        () => {

            localStorage.removeItem(
                "accessToken"
            );

            localStorage.removeItem(
                "studentUser"
            );


            navigate(
                "/student/login",
                {
                    replace: true,
                }
            );

        };


    return (

        <div className="min-h-screen bg-slate-50">

            {/* ====================================================
                HEADER
            ==================================================== */}

            <header className="border-b border-slate-200 bg-white">

                <div className="mx-auto flex min-h-20 max-w-7xl items-center justify-between gap-6 px-5 sm:px-7 lg:px-8">

                    <div className="flex items-center gap-3">

                        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-600 text-white">
                            <GraduationCap className="h-5 w-5" />
                        </div>

                        <div>

                            <p className="font-bold text-slate-950">
                                School Management
                            </p>

                            <p className="text-xs text-slate-500">
                                Student Portal
                            </p>

                        </div>

                    </div>


                    <button
                        type="button"
                        onClick={
                            handleLogout
                        }
                        className="inline-flex h-10 items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 hover:text-slate-900"
                    >
                        <LogOut className="h-4 w-4" />
                        Sign Out
                    </button>

                </div>

            </header>


            {/* ====================================================
                BODY
            ==================================================== */}

            <main className="mx-auto w-full max-w-7xl px-5 py-10 sm:px-7 lg:px-8">

                <div>

                    <p className="text-sm font-semibold text-blue-600">
                        Student Dashboard
                    </p>

                    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                        Welcome, {
                            student?.fullName ??
                            "Student"
                        }
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        Index Number: {
                            student?.indexNumber ??
                            "—"
                        }
                    </p>

                </div>


                <div className="mt-8 grid gap-5 md:grid-cols-3">


                    <DashboardCard
                        icon={
                            <UserRound className="h-5 w-5" />
                        }
                        title="My Profile"
                        description="View your student information and academic placement."
                        onClick={() =>
                            navigate(
                                "/student/profile"
                            )
                        }
                    />


                    <DashboardCard
                        icon={
                            <BookOpen className="h-5 w-5" />
                        }
                        title="My Subjects"
                        description="View subjects assigned for the current academic year."
                        onClick={() =>
                            navigate(
                                "/student/subjects"
                            )
                        }
                    />


                    <DashboardCard
                        icon={
                            <CalendarDays className="h-5 w-5" />
                        }
                        title="Attendance"
                        description="View your attendance records."
                        onClick={() =>
                            navigate(
                                "/student/attendance"
                            )
                        }
                    />

                    <DashboardCard
                        icon={
                            <Award className="h-5 w-5" />
                        }
                        title="Results"
                        description="View your published examination results."
                        onClick={() =>
                            navigate(
                                "/student/results"
                            )
                        }
                    />

                    <DashboardCard
                        icon={
                            <CalendarClock className="h-5 w-5" />
                        }
                        title="Timetable"
                        description="View your weekly class schedule."
                        onClick={() =>
                            navigate(
                                "/student/timetable"
                            )
                        }
                    />

                    <DashboardCard
                        icon={
                            <Megaphone className="h-5 w-5" />
                        }
                        title="Announcements"
                        description="View school announcements and important updates."
                        onClick={() =>
                            navigate(
                                "/student/announcements"
                            )
                        }
                    />

                    <DashboardCard
                        icon={
                            <Bell className="h-5 w-5" />
                        }
                        title="Notifications"
                        description="View alerts and updates sent to your account."
                        onClick={() =>
                            navigate(
                                "/student/notifications"
                            )
                        }
                    />

                    <DashboardCard
                        icon={<CalendarPlus className="h-5 w-5" />}
                        title="Special Classes"
                        description="View approved extra classes for your class."
                        onClick={() => navigate("/student/special-classes")}
                    />

                    <DashboardCard
                        icon={<CalendarRange className="h-5 w-5" />}
                        title="Daily Schedule"
                        description="See regular and special classes for a selected date."
                        onClick={() =>
                            navigate("/student/daily-schedule")
                        }
                    />

                    <DashboardCard
                        icon={<History className="h-5 w-5" />}
                        title="Enrollment History"
                        description="View your current and previous academic placements."
                        onClick={() =>
                            navigate("/student/enrollment-history")
                        }
                    />

                    <DashboardCard
                        icon={<UsersRound className="h-5 w-5" />}
                        title="My Guardians"
                        description="View guardians linked to your student record."
                        onClick={() =>
                            navigate("/student/guardians")
                        }
                    />

                    <DashboardCard
                        icon={<LockKeyhole className="h-5 w-5" />}
                        title="Change Password"
                        description="Update the password for your student account."
                        onClick={() => navigate("/student/change-password")}
                    />


                </div>

            </main>

        </div>

    );

}


function DashboardCard({
    icon,
    title,
    description,
    onClick,
}) {

    return (

        <button type="button"
            onClick={onClick}
            className="w-full rounded-2xl cursor-pointer border border-slate-200 bg-white p-6 text-left shadow-sm transition hover:-translate-y-0.5 hover:border-blue-200 hover:shadow-md">

            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                {icon}
            </div>

            <h2 className="mt-5 font-semibold text-slate-950">
                {title}
            </h2>

            <p className="mt-2 text-sm leading-6 text-slate-500">
                {description}
            </p>

        </button>

    );

}