import {
    ArrowLeft,
    BellRing,
    CalendarDays,
    GraduationCap,
    Megaphone,
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


export default function StudentAnnouncementsPage() {

    const navigate =
        useNavigate();


    const [
        data,
        setData,
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

        loadAnnouncements();

    }, []);


    const loadAnnouncements =
        async () => {

            try {

                setLoading(true);
                setError("");

                const response =
                    await studentPortalApi
                        .getAnnouncements();

                setData(
                    response.data
                );

            }
            catch (err) {

                console.error(
                    "Unable to load announcements:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load announcements."
                );

            }
            finally {

                setLoading(false);

            }

        };


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


            <main className="mx-auto w-full max-w-7xl px-5 py-8 sm:px-7 lg:px-8 lg:py-10">

                <button
                    type="button"
                    onClick={() =>
                        navigate(
                            "/student/dashboard"
                        )
                    }
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>


                <div className="mt-6">

                    <p className="text-sm font-semibold text-blue-600">
                        School Updates
                    </p>

                    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                        Announcements
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        View announcements shared with you by the school.
                    </p>

                </div>


                {loading && (

                    <div className="mt-10 flex justify-center">

                        <div className="h-8 w-8 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />

                    </div>

                )}


                {error && (

                    <div className="mt-8 rounded-xl border border-red-100 bg-red-50 p-4 text-sm text-red-700">
                        {error}
                    </div>

                )}


                {!loading &&
                    !error &&
                    data && (

                        <>

                            <div className="mt-8 grid gap-4 sm:grid-cols-2">

                                <InfoCard
                                    icon={
                                        <BellRing className="h-5 w-5" />
                                    }
                                    label="Available Announcements"
                                    value={
                                        data
                                            ?.totalAnnouncements ??
                                        0
                                    }
                                />

                                <InfoCard
                                    icon={
                                        <UserRound className="h-5 w-5" />
                                    }
                                    label="Student"
                                    value={
                                        data
                                            ?.student
                                            ?.fullName ||
                                        "—"
                                    }
                                />

                            </div>


                            {data.announcements?.length > 0 ? (

                                <div className="mt-6 space-y-4">

                                    {data.announcements.map(
                                        (
                                            announcement
                                        ) => (

                                            <AnnouncementCard
                                                key={
                                                    announcement.id
                                                }
                                                announcement={
                                                    announcement
                                                }
                                            />

                                        )
                                    )}

                                </div>

                            ) : (

                                <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">

                                    <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-xl bg-slate-100 text-slate-400">
                                        <Megaphone className="h-7 w-7" />
                                    </div>

                                    <h2 className="mt-4 font-semibold text-slate-950">
                                        No announcements
                                    </h2>

                                    <p className="mt-2 text-sm text-slate-500">
                                        There are no active announcements for you at the moment.
                                    </p>

                                </div>

                            )}

                        </>

                    )}

            </main>

        </div>

    );

}


function AnnouncementCard({
    announcement,
}) {

    return (

        <article className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

            <div className="flex flex-col justify-between gap-5 sm:flex-row">

                <div className="min-w-0">

                    <div className="flex items-start gap-4">

                        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                            <Megaphone className="h-5 w-5" />
                        </div>

                        <div>

                            <div className="flex flex-wrap items-center gap-2">

                                <h2 className="text-lg font-bold text-slate-950">
                                    {
                                        announcement
                                            .title
                                    }
                                </h2>

                                <AudienceBadge
                                    type={
                                        announcement
                                            .audienceType
                                    }
                                />

                            </div>

                            <p className="mt-3 whitespace-pre-line text-sm leading-6 text-slate-600">
                                {
                                    announcement
                                        .message
                                }
                            </p>

                        </div>

                    </div>

                </div>


                <div className="shrink-0 text-sm text-slate-500 sm:text-right">

                    <div className="inline-flex items-center gap-2">

                        <CalendarDays className="h-4 w-4" />

                        {
                            formatDate(
                                announcement
                                    .publishAt
                            )
                        }

                    </div>

                    {announcement
                        ?.createdBy
                        ?.fullName && (

                            <p className="mt-2 text-xs">
                                By{" "}
                                <span className="font-semibold text-slate-700">
                                    {
                                        announcement
                                            .createdBy
                                            .fullName
                                    }
                                </span>
                            </p>

                        )}

                </div>

            </div>

        </article>

    );

}


function AudienceBadge({
    type,
}) {

    const label =
        type === "AllStudents"
            ? "All Students"
            : type || "Student";


    return (

        <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
            {label}
        </span>

    );

}


function InfoCard({
    icon,
    label,
    value,
}) {

    return (

        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">

            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                {icon}
            </div>

            <p className="mt-4 text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>

            <p className="mt-1 text-lg font-bold text-slate-950">
                {value}
            </p>

        </div>

    );

}


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

    return date.toLocaleString(
        undefined,
        {
            year: "numeric",
            month: "short",
            day: "numeric",
            hour: "numeric",
            minute: "2-digit",
        }
    );

}