import {
    ArrowLeft,
    BookOpen,
    CalendarDays,
    Clock3,
    GraduationCap,
    MapPin,
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


const dayOrder = [
    "Sunday",
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
];


export default function StudentTimetablePage() {

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

        loadTimetable();

    }, []);


    const loadTimetable =
        async () => {

            try {

                setLoading(true);
                setError("");

                const response =
                    await studentPortalApi
                        .getTimetable();

                setData(
                    response.data
                );

            }
            catch (err) {

                console.error(
                    "Unable to load timetable:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load your timetable."
                );

            }
            finally {

                setLoading(false);

            }

        };


    const grouped =
        dayOrder.map(
            (day) => ({
                day,

                entries:
                    data
                        ?.timetable
                        ?.filter(
                            (entry) =>
                                entry.day === day
                        ) ?? [],
            })
        );


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
                        Academic
                    </p>

                    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                        My Timetable
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        View your weekly class schedule.
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

                            <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">

                                <InfoCard
                                    icon={
                                        <CalendarDays className="h-5 w-5" />
                                    }
                                    label="Academic Year"
                                    value={
                                        data
                                            ?.currentEnrollment
                                            ?.academicYear ||
                                        "—"
                                    }
                                />

                                <InfoCard
                                    icon={
                                        <BookOpen className="h-5 w-5" />
                                    }
                                    label="Academic Term"
                                    value={
                                        data
                                            ?.academicTerm
                                            ?.name ||
                                        "—"
                                    }
                                />

                                <InfoCard
                                    icon={
                                        <GraduationCap className="h-5 w-5" />
                                    }
                                    label="Class"
                                    value={
                                        data
                                            ?.currentEnrollment
                                            ?.class ||
                                        "—"
                                    }
                                />

                                <InfoCard
                                    icon={
                                        <Clock3 className="h-5 w-5" />
                                    }
                                    label="Weekly Classes"
                                    value={
                                        data
                                            ?.totalEntries ??
                                        0
                                    }
                                />

                            </div>


                            {data.timetable?.length > 0 ? (

                                <div className="mt-6 space-y-5">

                                    {grouped
                                        .filter(
                                            (group) =>
                                                group
                                                    .entries
                                                    .length > 0
                                        )
                                        .map(
                                            (group) => (

                                                <DayCard
                                                    key={
                                                        group.day
                                                    }
                                                    day={
                                                        group.day
                                                    }
                                                    entries={
                                                        group.entries
                                                    }
                                                />

                                            )
                                        )}

                                </div>

                            ) : (

                                <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">

                                    <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-xl bg-slate-100 text-slate-400">
                                        <CalendarDays className="h-7 w-7" />
                                    </div>

                                    <h2 className="mt-4 font-semibold text-slate-950">
                                        No timetable available
                                    </h2>

                                    <p className="mt-2 text-sm text-slate-500">
                                        No active timetable entries are available for your current class and academic term.
                                    </p>

                                </div>

                            )}

                        </>

                    )}

            </main>

        </div>

    );

}


function DayCard({
    day,
    entries,
}) {

    return (

        <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">

            <div className="border-b border-slate-100 px-6 py-4">

                <h2 className="font-semibold text-slate-950">
                    {day}
                </h2>

                <p className="mt-1 text-xs text-slate-500">
                    {entries.length} class{entries.length === 1 ? "" : "es"}
                </p>

            </div>


            <div className="divide-y divide-slate-100">

                {entries.map(
                    (entry) => (

                        <div
                            key={
                                entry
                                    .timetableEntryId
                            }
                            className="grid gap-4 px-6 py-5 md:grid-cols-[160px_1fr_220px_180px] md:items-center"
                        >

                            <div className="flex items-center gap-2 text-sm font-semibold text-slate-900">

                                <Clock3 className="h-4 w-4 text-blue-600" />

                                {formatTime(
                                    entry.startTime
                                )}
                                {" - "}
                                {formatTime(
                                    entry.endTime
                                )}

                            </div>


                            <div>

                                <p className="font-semibold text-slate-950">
                                    {
                                        entry
                                            ?.subject
                                            ?.name
                                    }
                                </p>

                                <p className="mt-1 text-xs text-slate-500">
                                    {
                                        entry
                                            ?.subject
                                            ?.code ||
                                        "No subject code"
                                    }
                                </p>

                            </div>


                            <div className="flex items-center gap-2 text-sm text-slate-600">

                                <UserRound className="h-4 w-4 text-slate-400" />

                                {
                                    entry
                                        ?.teacher
                                        ?.name ||
                                    "—"
                                }

                            </div>


                            <div className="flex items-center gap-2 text-sm text-slate-600">

                                <MapPin className="h-4 w-4 text-slate-400" />

                                {
                                    entry
                                        ?.room ||
                                    "No room assigned"
                                }

                            </div>

                        </div>

                    )
                )}

            </div>

        </section>

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

            <p className="mt-1 font-semibold text-slate-950">
                {value}
            </p>

        </div>

    );

}


function formatTime(
    value
) {

    if (!value) {
        return "—";
    }

    return value
        .toString()
        .slice(
            0,
            5
        );

}