import {
    ArrowLeft,
    CalendarDays,
    Clock3,
    GraduationCap,
    MapPin,
    UserRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { studentPortalApi } from "../../api/studentPortalApi";

function localToday() {
    const now = new Date();
    const local = new Date(
        now.getTime() - now.getTimezoneOffset() * 60000
    );

    return local.toISOString().slice(0, 10);
}

export default function StudentDailySchedulePage() {
    const navigate = useNavigate();

    const [date, setDate] = useState(localToday);
    const [data, setData] = useState(null);
    const [placement, setPlacement] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [retry, setRetry] = useState(0);

    useEffect(() => {
        let active = true;

        async function load() {
            try {
                const timetableResponse =
                    await studentPortalApi.getTimetable();

                if (!active) return;

                const timetable = timetableResponse.data;
                setPlacement(timetable);

                const yearId =
                    timetable.currentEnrollment?.academicYearId;

                const termId =
                    timetable.academicTerm?.id;

                if (!yearId || !termId) {
                    setData(null);
                    setError("");
                    return;
                }

                if (!date) {
                    setData(null);
                    setError(
                        "Select a date to view your schedule."
                    );
                    return;
                }

                const response =
                    await studentPortalApi.getDailySchedule(
                        yearId,
                        termId,
                        date
                    );

                if (active) {
                    setData(response.data);
                    setError("");
                }
            } catch (err) {
                if (active) {
                    setData(null);
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load your daily schedule."
                    );
                }
            } finally {
                if (active) {
                    setLoading(false);
                }
            }
        }

        load();

        return () => {
            active = false;
        };
    }, [date, retry]);

    const changeDate = (event) => {
        setDate(event.target.value);
        setLoading(true);
        setData(null);
        setError("");
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
                    onClick={() =>
                        navigate("/student/dashboard")
                    }
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>

                <div className="mt-6 flex flex-wrap items-end justify-between gap-5">
                    <div>
                        <p className="text-sm font-semibold text-blue-600">
                            Academic
                        </p>

                        <h1 className="mt-2 text-3xl font-bold text-slate-950">
                            Daily Schedule
                        </h1>

                        <p className="mt-2 text-sm text-slate-500">
                            View regular classes and approved
                            special classes for a date.
                        </p>
                    </div>

                    <label className="text-sm font-semibold text-slate-700">
                        Select date

                        <input
                            type="date"
                            value={date}
                            onChange={changeDate}
                            className="mt-2 block cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-slate-900 shadow-sm"
                        />
                    </label>
                </div>

                {loading && (
                    <div
                        className="mt-10 flex justify-center"
                        role="status"
                        aria-label="Loading daily schedule"
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

                {!loading &&
                    !error &&
                    !placement?.currentEnrollment && (
                        <EmptyState
                            text="No current academic enrollment is available for your account."
                        />
                    )}

                {!loading &&
                    !error &&
                    placement?.currentEnrollment &&
                    !placement?.academicTerm && (
                        <EmptyState
                            text="No active academic term is available for your academic year."
                        />
                    )}

                {!loading && !error && data && (
                    <>
                        <div className="mt-8 grid gap-4 sm:grid-cols-3">
                            <InfoCard
                                label="Academic Year"
                                value={
                                    placement.currentEnrollment
                                        .academicYear
                                }
                            />

                            <InfoCard
                                label="Term"
                                value={
                                    placement.academicTerm.name
                                }
                            />

                            <InfoCard
                                label="Classes on This Date"
                                value={data.totalCount ?? 0}
                            />
                        </div>

                        {data.schedule?.length ? (
                            <div className="mt-6 space-y-4">
                                {data.schedule.map((entry) => (
                                    <section
                                        key={`${entry.scheduleType}-${entry.id}`}
                                        className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
                                    >
                                        <div className="flex flex-wrap items-start justify-between gap-3">
                                            <div>
                                                <h2 className="text-lg font-semibold text-slate-950">
                                                    {entry.subjectName}
                                                </h2>

                                                <p className="mt-1 text-sm text-slate-500">
                                                    {entry.gradeName}
                                                    {" · "}
                                                    {entry.className}
                                                </p>
                                            </div>

                                            <span
                                                className={`rounded-full px-3 py-1 text-xs font-semibold ${entry.isSpecialClass
                                                        ? "bg-violet-50 text-violet-700"
                                                        : "bg-blue-50 text-blue-700"
                                                    }`}
                                            >
                                                {entry.isSpecialClass
                                                    ? "Special Class"
                                                    : "Regular Class"}
                                            </span>
                                        </div>

                                        <div className="mt-5 grid gap-3 text-sm text-slate-700 sm:grid-cols-3">
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
                                                    entry.teacherName ||
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
                                            <p className="mt-4 text-sm text-slate-600">
                                                Reason: {entry.reason}
                                            </p>
                                        )}
                                    </section>
                                ))}
                            </div>
                        ) : (
                            <EmptyState
                                text="No regular or approved special classes are scheduled for this date."
                            />
                        )}
                    </>
                )}
            </main>
        </div>
    );
}

function InfoCard({ label, value }) {
    return (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>

            <p className="mt-2 text-xl font-bold text-slate-950">
                {value ?? "—"}
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

function EmptyState({ text }) {
    return (
        <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
            <CalendarDays className="mx-auto h-8 w-8 text-slate-400" />

            <h2 className="mt-4 font-semibold text-slate-950">
                No schedule available
            </h2>

            <p className="mt-2 text-sm text-slate-500">
                {text}
            </p>
        </div>
    );
}

function formatTime(value) {
    if (!value) return "—";

    const [hours, minutes] = value
        .split(":")
        .map(Number);

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