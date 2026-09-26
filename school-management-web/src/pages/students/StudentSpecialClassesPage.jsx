import {
    ArrowLeft,
    CalendarDays,
    CalendarPlus,
    Clock3,
    GraduationCap,
    MapPin,
    UserRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { studentPortalApi } from "../../api/studentPortalApi";

export default function StudentSpecialClassesPage() {
    const navigate = useNavigate();

    const [upcomingOnly, setUpcomingOnly] = useState(true);
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [retry, setRetry] = useState(0);

    useEffect(() => {
        let active = true;

        studentPortalApi
            .getSpecialClasses(upcomingOnly)
            .then((response) => {
                if (active) {
                    setData(response.data);
                    setError("");
                }
            })
            .catch((err) => {
                if (active) {
                    setData(null);
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load special classes."
                    );
                }
            })
            .finally(() => {
                if (active) setLoading(false);
            });

        return () => {
            active = false;
        };
    }, [upcomingOnly, retry]);

    const selectFilter = (value) => {
        if (value === upcomingOnly) return;

        setLoading(true);
        setData(null);
        setError("");
        setUpcomingOnly(value);
    };

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex min-h-20 max-w-7xl items-center gap-3 px-5 sm:px-7 lg:px-8">
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
            </header>

            <main className="mx-auto w-full max-w-7xl px-5 py-8 sm:px-7 lg:px-8 lg:py-10">
                <button
                    type="button"
                    onClick={() => navigate("/student/dashboard")}
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>

                <div className="mt-6 flex flex-wrap items-end justify-between gap-5">
                    <div>
                        <p className="text-sm font-semibold text-blue-600">
                            Academic
                        </p>

                        <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                            Special Classes
                        </h1>

                        <p className="mt-2 text-sm text-slate-500">
                            Approved extra classes scheduled for your class.
                        </p>
                    </div>

                    <div
                        className="inline-flex rounded-xl border border-slate-200 bg-white p-1 shadow-sm"
                        aria-label="Special class date filter"
                    >
                        <FilterButton
                            selected={upcomingOnly}
                            onClick={() => selectFilter(true)}
                        >
                            Upcoming
                        </FilterButton>

                        <FilterButton
                            selected={!upcomingOnly}
                            onClick={() => selectFilter(false)}
                        >
                            All Approved
                        </FilterButton>
                    </div>
                </div>

                {loading && (
                    <div
                        className="mt-10 flex justify-center"
                        role="status"
                        aria-label="Loading special classes"
                    >
                        <div className="h-8 w-8 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />
                    </div>
                )}

                {!loading && error && (
                    <div
                        className="mt-8 rounded-xl border border-red-100 bg-red-50 p-5 text-sm text-red-700"
                        role="alert"
                    >
                        <p>{error}</p>

                        <button
                            type="button"
                            onClick={() => {
                                setLoading(true);
                                setRetry((value) => value + 1);
                            }}
                            className="mt-3 cursor-pointer font-semibold underline"
                        >
                            Try Again
                        </button>
                    </div>
                )}

                {!loading && !error && data && (
                    <>
                        <div className="mt-8 grid gap-4 sm:grid-cols-2">
                            <InfoCard
                                label="Class"
                                value={
                                    [
                                        data.schoolClass?.grade,
                                        data.schoolClass?.name,
                                    ]
                                        .filter(Boolean)
                                        .join(" · ") || "—"
                                }
                            />

                            <InfoCard
                                label={
                                    upcomingOnly
                                        ? "Upcoming Classes"
                                        : "Approved Classes"
                                }
                                value={data.count ?? 0}
                            />
                        </div>

                        {data.specialClasses?.length ? (
                            <div className="mt-6 space-y-4">
                                {data.specialClasses.map((entry) => (
                                    <section
                                        key={entry.id}
                                        className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
                                    >
                                        <div className="flex flex-wrap items-start justify-between gap-3">
                                            <div>
                                                <h2 className="text-lg font-semibold text-slate-950">
                                                    {entry.subject?.name ||
                                                        "Special Class"}
                                                </h2>

                                                <p className="mt-1 text-sm text-slate-500">
                                                    {[
                                                        entry.academicYear?.name,
                                                        entry.academicTerm?.name,
                                                    ]
                                                        .filter(Boolean)
                                                        .join(" · ")}
                                                </p>
                                            </div>

                                            <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                                                Approved
                                            </span>
                                        </div>

                                        <div className="mt-5 grid gap-3 text-sm text-slate-700 sm:grid-cols-2 lg:grid-cols-4">
                                            <Detail
                                                icon={CalendarDays}
                                                value={formatDate(
                                                    entry.classDate
                                                )}
                                            />

                                            <Detail
                                                icon={Clock3}
                                                value={`${formatTime(
                                                    entry.startTime
                                                )} – ${formatTime(
                                                    entry.endTime
                                                )}`}
                                            />

                                            <Detail
                                                icon={UserRound}
                                                value={
                                                    entry.teacher?.fullName ||
                                                    "Teacher not listed"
                                                }
                                            />

                                            <Detail
                                                icon={MapPin}
                                                value={
                                                    entry.room ||
                                                    "Room not listed"
                                                }
                                            />
                                        </div>

                                        {entry.reason && (
                                            <p className="mt-5 text-sm text-slate-600">
                                                <span className="font-semibold text-slate-800">
                                                    Reason:{" "}
                                                </span>
                                                {entry.reason}
                                            </p>
                                        )}

                                        {entry.remarks && (
                                            <p className="mt-2 text-sm text-slate-600">
                                                <span className="font-semibold text-slate-800">
                                                    Remarks:{" "}
                                                </span>
                                                {entry.remarks}
                                            </p>
                                        )}
                                    </section>
                                ))}
                            </div>
                        ) : (
                            <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
                                <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-xl bg-slate-100 text-slate-400">
                                    <CalendarPlus className="h-7 w-7" />
                                </div>

                                <h2 className="mt-4 font-semibold text-slate-950">
                                    No special classes
                                </h2>

                                <p className="mt-2 text-sm text-slate-500">
                                    {upcomingOnly
                                        ? "No upcoming approved special classes are scheduled for your class."
                                        : "No approved special classes are available for your class."}
                                </p>
                            </div>
                        )}
                    </>
                )}
            </main>
        </div>
    );
}

function FilterButton({ selected, onClick, children }) {
    return (
        <button
            type="button"
            onClick={onClick}
            aria-pressed={selected}
            className={`cursor-pointer rounded-lg px-4 py-2 text-sm font-semibold transition ${selected
                    ? "bg-blue-600 text-white"
                    : "text-slate-600 hover:bg-slate-50"
                }`}
        >
            {children}
        </button>
    );
}

function InfoCard({ label, value }) {
    return (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>
            <p className="mt-2 text-xl font-bold text-slate-950">
                {value}
            </p>
        </div>
    );
}

function Detail({ icon: Icon, value }) {
    return (
        <div className="flex items-center gap-2">
            <Icon className="h-4 w-4 shrink-0 text-blue-600" />
            <span>{value}</span>
        </div>
    );
}

function formatDate(value) {
    if (!value) return "—";

    const [year, month, day] = value
        .slice(0, 10)
        .split("-")
        .map(Number);

    if (!year || !month || !day) return value;

    return new Date(year, month - 1, day).toLocaleDateString(
        undefined,
        {
            weekday: "short",
            year: "numeric",
            month: "short",
            day: "numeric",
        }
    );
}

function formatTime(value) {
    if (!value) return "—";

    const [hours, minutes] = value.split(":").map(Number);

    if (
        !Number.isFinite(hours) ||
        !Number.isFinite(minutes)
    ) {
        return value;
    }

    return new Date(
        2000,
        0,
        1,
        hours,
        minutes
    ).toLocaleTimeString(undefined, {
        hour: "numeric",
        minute: "2-digit",
    });
}