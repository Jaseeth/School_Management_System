import { CalendarDays, Clock3, MapPin, RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import api from "../../api/axios";
import { useAuth } from "../../context/AuthContext";

const days = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];

function timeLabel(value) {
    if (!value) return "—";
    return String(value).slice(0, 5);
}

export default function TeacherTimetablePage() {
    const { user } = useAuth();
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [refreshKey, setRefreshKey] = useState(0);
    const isTeacher = user?.roles?.includes("Teacher");

    useEffect(() => {
        if (!isTeacher) return;

        let active = true;

        async function load() {
            setLoading(true);
            setError("");

            try {
                const response = await api.get("/timetable/my");
                if (active) setData(response.data);
            } catch (err) {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load your timetable."
                    );
                }
            } finally {
                if (active) setLoading(false);
            }
        }

        load();

        return () => {
            active = false;
        };
    }, [isTeacher, refreshKey]);

    if (!isTeacher) {
        return (
            <p
                role="alert"
                className="rounded-2xl border border-amber-200 bg-amber-50 p-5 text-amber-900"
            >
                This timetable is available to teachers only.
            </p>
        );
    }

    const entries = Array.isArray(data?.timetable) ? data.timetable : [];

    return (
        <div className="mx-auto max-w-7xl">
            <div className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <p className="text-sm font-semibold text-blue-600">
                        Teacher Portal
                    </p>
                    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                        My Timetable
                    </h1>
                    <p className="mt-2 text-sm text-slate-600">
                        Your scheduled classes from the school timetable.
                    </p>

                    {data?.staff && (
                        <p className="mt-2 text-sm text-slate-500">
                            {data.staff.fullName} · {data.staff.staffNumber}
                        </p>
                    )}
                </div>

                <button
                    type="button"
                    onClick={() => setRefreshKey((value) => value + 1)}
                    disabled={loading}
                    className="inline-flex w-fit cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                >
                    <RefreshCw className="h-4 w-4" />
                    Refresh
                </button>
            </div>

            {loading ? (
                <p
                    role="status"
                    className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600"
                >
                    Loading timetable...
                </p>
            ) : error ? (
                <p
                    role="alert"
                    className="mt-8 rounded-2xl border border-red-200 bg-red-50 p-6 text-red-700"
                >
                    {error}
                </p>
            ) : entries.length === 0 ? (
                <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm">
                    <CalendarDays className="mx-auto h-10 w-10 text-slate-400" />
                    <h2 className="mt-4 text-lg font-bold text-slate-900">
                        No classes scheduled yet
                    </h2>
                    <p className="mt-2 text-sm text-slate-600">
                        Your timetable will appear when classes are assigned.
                    </p>
                </div>
            ) : (
                <>
                    <p className="mt-8 text-sm font-medium text-slate-600">
                        {data?.count ?? entries.length} scheduled{" "}
                        {entries.length === 1 ? "class" : "classes"}
                    </p>

                    <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-5">
                        {days.map((day) => {
                            const classes = entries
                                .filter((entry) => entry.day === day)
                                .sort((a, b) =>
                                    String(a.startTime).localeCompare(
                                        String(b.startTime)
                                    )
                                );

                            return (
                                <section
                                    key={day}
                                    className="min-w-0 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5"
                                >
                                    <div className="flex items-center justify-between gap-2 border-b border-slate-100 pb-4">
                                        <h2 className="text-base font-bold text-slate-950">
                                            {day}
                                        </h2>
                                        <span className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-blue-700">
                                            {classes.length}
                                        </span>
                                    </div>

                                    {classes.length === 0 ? (
                                        <p className="py-6 text-sm text-slate-500">
                                            No classes scheduled
                                        </p>
                                    ) : (
                                        <div className="space-y-3 pt-4">
                                            {classes.map((entry) => (
                                                <article
                                                    key={entry.id}
                                                    className="rounded-xl border border-slate-200 bg-slate-50 p-4"
                                                >
                                                    <p className="font-bold text-slate-950">
                                                        {entry.subject?.name ||
                                                            "Subject"}
                                                    </p>
                                                    <p className="mt-1 text-sm text-slate-600">
                                                        {[
                                                            entry.schoolClass
                                                                ?.section,
                                                            entry.schoolClass
                                                                ?.grade,
                                                            entry.schoolClass
                                                                ?.name,
                                                        ]
                                                            .filter(Boolean)
                                                            .join(" · ")}
                                                    </p>
                                                    <p className="mt-3 flex items-center gap-2 text-sm text-slate-700">
                                                        <Clock3 className="h-4 w-4 shrink-0 text-blue-600" />
                                                        {timeLabel(
                                                            entry.startTime
                                                        )}{" "}
                                                        –{" "}
                                                        {timeLabel(
                                                            entry.endTime
                                                        )}
                                                    </p>

                                                    {entry.room && (
                                                        <p className="mt-2 flex items-center gap-2 text-sm text-slate-700">
                                                            <MapPin className="h-4 w-4 shrink-0 text-blue-600" />
                                                            {entry.room}
                                                        </p>
                                                    )}
                                                </article>
                                            ))}
                                        </div>
                                    )}
                                </section>
                            );
                        })}
                    </div>
                </>
            )}
        </div>
    );
}