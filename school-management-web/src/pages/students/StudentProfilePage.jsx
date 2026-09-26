import {
    ArrowLeft,
    CalendarDays,
    GraduationCap,
    Mail,
    Phone,
    School,
    UserRound,
} from "lucide-react";

import {
    useEffect,
    useState,
} from "react";

import {
    useNavigate,
} from "react-router-dom";

import {
    studentPortalApi,
} from "../../api/studentPortalApi";


export default function StudentProfilePage() {

    const navigate =
        useNavigate();


    const [
        profile,
        setProfile,
    ] = useState(null);


    const [
        loading,
        setLoading,
    ] = useState(true);


    const [
        error,
        setError,
    ] = useState("");


    useEffect(() => {

        loadProfile();

    }, []);


    const loadProfile =
        async () => {

            try {

                setLoading(true);
                setError("");

                const response =
                    await studentPortalApi
                        .getProfile();

                setProfile(
                    response.data
                );

            }
            catch (err) {

                console.error(
                    "Unable to load student profile:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load your profile."
                );

            }
            finally {

                setLoading(false);

            }

        };


    if (loading) {

        return (

            <StudentPageShell>

                <div className="flex min-h-[400px] items-center justify-center">

                    <div className="text-center">

                        <div className="mx-auto h-8 w-8 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />

                        <p className="mt-4 text-sm text-slate-500">
                            Loading profile...
                        </p>

                    </div>

                </div>

            </StudentPageShell>

        );

    }


    if (error) {

        return (

            <StudentPageShell>

                <div className="mx-auto max-w-xl rounded-2xl border border-red-100 bg-red-50 p-6 text-center">

                    <p className="font-semibold text-red-700">
                        Unable to load profile
                    </p>

                    <p className="mt-2 text-sm text-red-600">
                        {error}
                    </p>

                    <button
                        type="button"
                        onClick={
                            loadProfile
                        }
                        className="mt-5 rounded-xl bg-red-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-red-700"
                    >
                        Try Again
                    </button>

                </div>

            </StudentPageShell>

        );

    }


    const enrollment =
        profile?.currentEnrollment;


    return (

        <StudentPageShell>

            <div className="mx-auto w-full max-w-6xl">


                {/* ================================================
                    BACK
                ================================================ */}

                <button
                    type="button"
                    onClick={() =>
                        navigate(
                            "/student/dashboard"
                        )
                    }
                    className="inline-flex items-center cursor-pointer gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>


                {/* ================================================
                    HEADER
                ================================================ */}

                <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">

                    <div className="flex flex-col gap-5 sm:flex-row sm:items-center">

                        <div className="flex h-20 w-20 shrink-0 items-center justify-center rounded-2xl bg-blue-50 text-blue-700">
                            <UserRound className="h-9 w-9" />
                        </div>

                        <div className="min-w-0">

                            <div className="flex flex-wrap items-center gap-3">

                                <h1 className="text-2xl font-bold tracking-tight text-slate-950 sm:text-3xl">
                                    {
                                        profile
                                            ?.fullName
                                    }
                                </h1>

                                {profile?.isActive && (

                                    <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                                        Active
                                    </span>

                                )}

                            </div>

                            <p className="mt-2 text-sm text-slate-500">
                                Index Number:{" "}

                                <span className="font-semibold text-slate-700">
                                    {
                                        profile
                                            ?.indexNumber
                                    }
                                </span>
                            </p>

                        </div>

                    </div>

                </div>


                {/* ================================================
                    GRID
                ================================================ */}

                <div className="mt-6 grid gap-6 lg:grid-cols-2">


                    {/* ============================================
                        PERSONAL INFORMATION
                    ============================================ */}

                    <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">

                        <div className="border-b border-slate-100 px-6 py-5">

                            <div className="flex items-center gap-3">

                                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                                    <UserRound className="h-5 w-5" />
                                </div>

                                <div>

                                    <h2 className="font-semibold text-slate-950">
                                        Personal Information
                                    </h2>

                                    <p className="text-xs text-slate-500">
                                        Your registered student details
                                    </p>

                                </div>

                            </div>

                        </div>


                        <div className="divide-y divide-slate-100 px-6">

                            <ProfileRow
                                icon={
                                    <UserRound className="h-4 w-4" />
                                }
                                label="Full Name"
                                value={
                                    profile
                                        ?.fullName
                                }
                            />

                            <ProfileRow
                                icon={
                                    <GraduationCap className="h-4 w-4" />
                                }
                                label="Index Number"
                                value={
                                    profile
                                        ?.indexNumber
                                }
                            />

                            <ProfileRow
                                icon={
                                    <CalendarDays className="h-4 w-4" />
                                }
                                label="Date of Birth"
                                value={
                                    formatDate(
                                        profile
                                            ?.dateOfBirth
                                    )
                                }
                            />

                            <ProfileRow
                                icon={
                                    <Mail className="h-4 w-4" />
                                }
                                label="Email"
                                value={
                                    profile
                                        ?.email ||
                                    "Not available"
                                }
                            />

                            <ProfileRow
                                icon={
                                    <Phone className="h-4 w-4" />
                                }
                                label="Mobile"
                                value={
                                    profile
                                        ?.mobile ||
                                    "Not available"
                                }
                            />

                        </div>

                    </section>


                    {/* ============================================
                        ACADEMIC PLACEMENT
                    ============================================ */}

                    <section className="rounded-2xl border border-slate-200 bg-white shadow-sm">

                        <div className="border-b border-slate-100 px-6 py-5">

                            <div className="flex items-center gap-3">

                                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                                    <School className="h-5 w-5" />
                                </div>

                                <div>

                                    <h2 className="font-semibold text-slate-950">
                                        Academic Placement
                                    </h2>

                                    <p className="text-xs text-slate-500">
                                        Your current academic enrollment
                                    </p>

                                </div>

                            </div>

                        </div>


                        {enrollment ? (

                            <div className="divide-y divide-slate-100 px-6">

                                <ProfileRow
                                    label="Academic Year"
                                    value={
                                        enrollment
                                            .academicYear
                                    }
                                />

                                <ProfileRow
                                    label="Section"
                                    value={
                                        enrollment
                                            .section
                                    }
                                />

                                <ProfileRow
                                    label="Grade"
                                    value={
                                        enrollment
                                            .grade
                                    }
                                />

                                <ProfileRow
                                    label="Class"
                                    value={
                                        enrollment
                                            .class
                                    }
                                />

                                <ProfileRow
                                    label="Enrollment Date"
                                    value={
                                        formatDate(
                                            enrollment
                                                .enrollmentDate
                                        )
                                    }
                                />

                            </div>

                        ) : (

                            <div className="p-6">

                                <div className="rounded-xl border border-amber-100 bg-amber-50 p-4 text-sm text-amber-700">
                                    No current academic enrollment is available.
                                </div>

                            </div>

                        )}

                    </section>

                </div>


                {/* ================================================
                    STATUS
                ================================================ */}

                <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                    <h2 className="font-semibold text-slate-950">
                        Student Status
                    </h2>

                    <div className="mt-5 grid gap-4 sm:grid-cols-2">

                        <StatusCard
                            label="Account Status"
                            value={
                                profile?.isActive
                                    ? "Active"
                                    : "Inactive"
                            }
                        />

                        <StatusCard
                            label="Graduation Status"
                            value={
                                profile?.isGraduated
                                    ? "Graduated"
                                    : "Not Graduated"
                            }
                        />

                    </div>

                </section>

            </div>

        </StudentPageShell>

    );

}


// ============================================================
// PAGE SHELL
// ============================================================

function StudentPageShell({
    children,
}) {

    return (

        <div className="min-h-screen bg-slate-50">

            <header className="border-b border-slate-200 bg-white">

                <div className="mx-auto flex min-h-20 max-w-7xl items-center px-5 sm:px-7 lg:px-8">

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

                </div>

            </header>


            <main className="px-5 py-8 sm:px-7 lg:px-8 lg:py-10">
                {children}
            </main>

        </div>

    );

}


// ============================================================
// PROFILE ROW
// ============================================================

function ProfileRow({
    icon,
    label,
    value,
}) {

    return (

        <div className="flex items-center justify-between gap-6 py-4">

            <div className="flex min-w-0 items-center gap-3">

                {icon && (

                    <div className="text-slate-400">
                        {icon}
                    </div>

                )}

                <span className="text-sm text-slate-500">
                    {label}
                </span>

            </div>

            <span className="max-w-[60%] text-right text-sm font-semibold text-slate-900">
                {value || "—"}
            </span>

        </div>

    );

}


// ============================================================
// STATUS CARD
// ============================================================

function StatusCard({
    label,
    value,
}) {

    return (

        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">

            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>

            <p className="mt-2 font-semibold text-slate-950">
                {value}
            </p>

        </div>

    );

}


// ============================================================
// DATE
// ============================================================

function formatDate(
    value
) {

    if (!value) {
        return "—";
    }

    const date =
        new Date(value);

    if (
        Number.isNaN(
            date.getTime()
        )
    ) {
        return value;
    }

    return date.toLocaleDateString(
        undefined,
        {
            year: "numeric",
            month: "short",
            day: "numeric",
        }
    );

}