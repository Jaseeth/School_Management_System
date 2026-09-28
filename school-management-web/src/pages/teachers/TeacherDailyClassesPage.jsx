import {
    CalendarClock,
    Clock3,
    MapPin,
    RefreshCw,
    Sparkles,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import api from "../../api/axios";
import { dashboardApi } from "../../api/dashboardApi";
import { useAuth } from "../../context/AuthContext";

function atTime(date, time) {
    const [hours, minutes] = String(time || "00:00")
        .split(":")
        .map(Number);

    const result = new Date(date);
    result.setHours(hours || 0, minutes || 0, 0, 0);
    return result;
}

function buildOccurrences(items, today) {
    const occurrences = [];

    for (let offset = 0; offset < 7; offset += 1) {
        const date = new Date(today);
        date.setDate(date.getDate() + offset);

        const dateKey = [
            date.getFullYear(),
            String(date.getMonth() + 1).padStart(2, "0"),
            String(date.getDate()).padStart(2, "0"),
        ].join("-");

        for (const item of items) {
            const matches = item.isSpecialClass
                ? String(item.scheduleDate).slice(0, 10) === dateKey
                : Number(item.day) === date.getDay();

            if (!matches) continue;

            occurrences.push({
                ...item,
                date: dateKey,
                startsAt: atTime(date, item.startTime),
                endsAt: atTime(date, item.endTime),
            });
        }
    }

    return occurrences.sort((a, b) => a.startsAt - b.startsAt);
}

function ClassCard({ item }) {
    if (!item) {
        return (
            <p className="text-sm text-slate-600">
                No class scheduled.
            </p>
        );
    }

    return (
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                    <p className="font-bold text-slate-950">
                        {item.subjectName || "Subject"}
                    </p>
                    <p className="mt-1 text-sm text-slate-600">
                        {[
                            item.sectionName,
                            item.gradeName,
                            item.className,
                        ]
                            .filter(Boolean)
                            .join(" · ")}
                    </p>
                </div>

                {item.isSpecialClass && (
                    <span className="inline-flex items-center gap-1 rounded-full bg-violet-100 px-2.5 py-1 text-xs font-semibold text-violet-700">
                        <Sparkles className="h-3.5 w-3.5" />
                        Special class
                    </span>
                )}
            </div>

            <p className="mt-3 flex items-center gap-2 text-sm text-slate-700">
                <Clock3 className="h-4 w-4 text-blue-600" />
                {String(item.startTime).slice(0, 5)} –{" "}
                {String(item.endTime).slice(0, 5)}
            </p>

            {item.room && (
                <p className="mt-2 flex items-center gap-2 text-sm text-slate-700">
                    <MapPin className="h-4 w-4 text-blue-600" />
                    {item.room}
                </p>
            )}

            {item.reason && (
                <p className="mt-2 text-sm text-slate-600">
                    {item.reason}
                </p>
            )}
        </div>
    );
}

export default function TeacherDailyClassesPage() {
    const { user } = useAuth();
    const isTeacher = user?.roles?.includes("Teacher");

    const [summary, setSummary] = useState(null);
    const [items, setItems] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [refreshKey, setRefreshKey] = useState(0);
    const [now, setNow] = useState(() => new Date());

    useEffect(() => {
        const timer = window.setInterval(
            () => setNow(new Date()),
            60_000
        );

        return () => window.clearInterval(timer);
    }, []);

    useEffect(() => {
        if (!isTeacher) return;

        let active = true;

        async function load() {
            setLoading(true);
            setError("");

            try {
                const summaryResponse =
                    await dashboardApi.getTeacherSummary();

                const details = summaryResponse.data;

                if (active) setSummary(details);

                if (
                    details.activeAcademicYearId &&
                    details.activeAcademicTermId
                ) {
                    const scheduleResponse = await api.get(
                        "/unified-schedule",
                        {
                            params: {
                                academicYearId:
                                    details.activeAcademicYearId,
                                academicTermId:
                                    details.activeAcademicTermId,
                            },
                        }
                    );

                    if (active) {
                        setItems(
                            Array.isArray(scheduleResponse.data?.schedule)
                                ? scheduleResponse.data.schedule
                                : []
                        );
                    }
                } else if (active) {
                    setItems([]);
                }

                if (active) setNow(new Date());
            } catch (err) {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load classes."
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

    const today = new Date(
        now.getFullYear(),
        now.getMonth(),
        now.getDate()
    );

    const occurrences = useMemo(
        () => buildOccurrences(items, today),
        [items, now]
    );

    const tomorrow = new Date(
        today.getFullYear(),
        today.getMonth(),
        today.getDate() + 1
    );

    const todaysClasses = occurrences.filter(
        (item) => item.startsAt >= today && item.startsAt < tomorrow
    );

    const current = todaysClasses.find(
        (item) => item.startsAt <= now && item.endsAt > now
    );

    const next = occurrences.find(
        (item) => item.startsAt > now
    );

    if (!isTeacher) {
        return (
            <p
                role="alert"
                className="rounded-2xl bg-amber-50 p-5 text-amber-900"
            >
                This page is available to teachers only.
            </p>
        );
    }

    return (
        <div className="mx-auto max-w-6xl">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
                <div>
                    <p className="text-sm font-semibold text-blue-600">
                        Teacher Portal
                    </p>
                    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                        Today & Next Class
                    </h1>
                    <p className="mt-2 text-sm text-slate-600">
                        {now.toLocaleDateString(undefined, {
                            weekday: "long",
                            day: "numeric",
                            month: "long",
                            year: "numeric",
                        })}
                    </p>

                    {summary?.activeAcademicYearName && (
                        <p className="mt-1 text-sm text-slate-500">
                            {summary.activeAcademicYearName}
                            {summary.activeAcademicTermName
                                ? ` · ${summary.activeAcademicTermName}`
                                : ""}
                        </p>
                    )}
                </div>

                <button
                    type="button"
                    onClick={() =>
                        setRefreshKey((value) => value + 1)
                    }
                    disabled={loading}
                    className="inline-flex w-fit cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                >
                    <RefreshCw className="h-4 w-4" />
                    Refresh
                </button>
            </div>

            {loading ? (
                <p
                    role="status"
                    className="mt-8 rounded-2xl bg-white p-6 text-slate-600"
                >
                    Loading classes...
                </p>
            ) : error ? (
                <p
                    role="alert"
                    className="mt-8 rounded-2xl border border-red-200 bg-red-50 p-6 text-red-700"
                >
                    {error}
                </p>
            ) : !summary?.activeAcademicTermId ? (
                <p className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                    No active academic term is available yet.
                </p>
            ) : (
                <>
                    <div className="mt-8 grid gap-5 md:grid-cols-2">
                        <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                            <h2 className="mb-5 flex items-center gap-2 text-lg font-bold text-slate-950">
                                <Clock3 className="h-5 w-5 text-blue-600" />
                                Current class
                            </h2>
                            <ClassCard item={current} />
                        </section>

                        <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                            <h2 className="mb-5 flex items-center gap-2 text-lg font-bold text-slate-950">
                                <CalendarClock className="h-5 w-5 text-blue-600" />
                                Next class
                            </h2>

                            {next && (
                                <p className="mb-3 text-sm font-semibold text-blue-700">
                                    {next.startsAt.toLocaleDateString(
                                        undefined,
                                        {
                                            weekday: "long",
                                            day: "numeric",
                                            month: "long",
                                        }
                                    )}
                                </p>
                            )}

                            <ClassCard item={next} />

                            {!next && (
                                <p className="mt-2 text-xs text-slate-500">
                                    No upcoming class in the next seven days.
                                </p>
                            )}
                        </section>
                    </div>

                    <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                        <h2 className="text-lg font-bold text-slate-950">
                            Today’s classes ({todaysClasses.length})
                        </h2>

                        {todaysClasses.length === 0 ? (
                            <p className="mt-4 text-sm text-slate-600">
                                No classes scheduled today.
                            </p>
                        ) : (
                            <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                                {todaysClasses.map((item) => (
                                    <ClassCard
                                        key={`${item.scheduleType}-${item.id}-${item.date}`}
                                        item={item}
                                    />
                                ))}
                            </div>
                        )}
                    </section>
                </>
            )}
        </div>
    );
}