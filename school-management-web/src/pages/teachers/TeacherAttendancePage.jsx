import { ClipboardCheck, RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import api from "../../api/axios";
import { useAuth } from "../../context/AuthContext";

const statusOptions = [
    { value: "1", label: "Present" },
    { value: "2", label: "Absent" },
    { value: "3", label: "Late" },
    { value: "4", label: "Excused" },
];

function localDate() {
    const now = new Date();

    return [
        now.getFullYear(),
        String(now.getMonth() + 1).padStart(2, "0"),
        String(now.getDate()).padStart(2, "0"),
    ].join("-");
}

function dateForYear(year) {
    const today = localDate();
    const first = year.academicYearStartDate?.slice(0, 10);
    const last = year.academicYearEndDate?.slice(0, 10);

    if (first && today < first) return "";
    if (last && today > last) return "";

    return today;
}

function classKey(item) {
    return `${item.academicYearId}:${item.schoolClassId}`;
}

export default function TeacherAttendancePage() {
    const { user } = useAuth();
    const isTeacher = user?.roles?.includes("Teacher");

    const [classes, setClasses] = useState([]);
    const [selectedKey, setSelectedKey] = useState("");
    const [date, setDate] = useState(localDate);
    const [students, setStudents] = useState([]);
    const [draft, setDraft] = useState({});
    const [loadingClasses, setLoadingClasses] = useState(true);
    const [loadingRoster, setLoadingRoster] = useState(false);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [reloadKey, setReloadKey] = useState(0);

    const selected = classes.find(
        (item) => classKey(item) === selectedKey
    );

    useEffect(() => {
        if (!isTeacher) return;

        let active = true;

        async function loadClasses() {
            setLoadingClasses(true);
            setError("");

            try {
                const response = await api.get(
                    "/attendance/my/classes"
                );

                if (!active) return;

                const list = Array.isArray(response.data?.classes)
                    ? response.data.classes
                    : [];

                setClasses(list);

                if (list.length) {
                    setSelectedKey(classKey(list[0]));
                    setDate(dateForYear(list[0]));
                }
            } catch (err) {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load your attendance classes."
                    );
                }
            } finally {
                if (active) setLoadingClasses(false);
            }
        }

        loadClasses();

        return () => {
            active = false;
        };
    }, [isTeacher]);

    useEffect(() => {
        if (!selected || !date) return;

        const first =
            selected.academicYearStartDate?.slice(0, 10);
        const last =
            selected.academicYearEndDate?.slice(0, 10);

        if (
            (first && date < first) ||
            (last && date > last)
        ) {
            setStudents([]);
            setDraft({});
            setError(
                "Choose a date within the selected academic year."
            );
            return;
        }

        let active = true;

        async function loadRoster() {
            setLoadingRoster(true);
            setError("");
            setMessage("");
            setStudents([]);
            setDraft({});

            try {
                const response = await api.get(
                    `/attendance/class/${selected.schoolClassId}/students`,
                    {
                        params: {
                            academicYearId:
                                selected.academicYearId,
                            attendanceDate: date,
                        },
                    }
                );

                if (!active) return;

                const list = Array.isArray(
                    response.data?.students
                )
                    ? response.data.students
                    : [];

                const initial = {};

                for (const student of list) {
                    const named = statusOptions.find(
                        (option) =>
                            option.label === student.statusName
                    );

                    initial[student.id] = {
                        status:
                            named?.value ||
                            (student.status
                                ? String(student.status)
                                : ""),
                        remarks: student.remarks || "",
                    };
                }

                setStudents(list);
                setDraft(initial);
            } catch (err) {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load the class roster."
                    );
                }
            } finally {
                if (active) setLoadingRoster(false);
            }
        }

        loadRoster();

        return () => {
            active = false;
        };
    }, [selectedKey, date, reloadKey, classes]);

    function chooseClass(event) {
        const next = classes.find(
            (item) =>
                classKey(item) === event.target.value
        );

        if (!next) return;

        setSelectedKey(classKey(next));
        setDate(dateForYear(next));
    }

    function changeStudent(studentId, field, value) {
        setDraft((current) => ({
            ...current,
            [studentId]: {
                ...current[studentId],
                [field]: value,
            },
        }));

        setMessage("");
    }

    function markAllPresent() {
        setDraft((current) => {
            const updated = { ...current };

            for (const student of students) {
                updated[student.id] = {
                    ...updated[student.id],
                    status: "1",
                };
            }

            return updated;
        });

        setMessage("");
    }

    async function save(event) {
        event.preventDefault();

        if (!selected || students.length === 0 || saving) {
            return;
        }

        const first =
            selected.academicYearStartDate?.slice(0, 10);
        const last =
            selected.academicYearEndDate?.slice(0, 10);

        if (
            (first && date < first) ||
            (last && date > last)
        ) {
            setError(
                "Choose a date within the selected academic year."
            );
            return;
        }

        if (
            students.some(
                (student) => !draft[student.id]?.status
            )
        ) {
            setError(
                "Select a status for every student before saving."
            );
            return;
        }

        setSaving(true);
        setError("");
        setMessage("");

        try {
            const response = await api.post(
                "/attendance/mark",
                {
                    academicYearId:
                        selected.academicYearId,
                    schoolClassId:
                        selected.schoolClassId,
                    attendanceDate:
                        `${date}T00:00:00`,
                    students: students.map((student) => ({
                        studentId: student.id,
                        status: Number(
                            draft[student.id].status
                        ),
                        remarks:
                            draft[student.id].remarks
                                ?.trim() || null,
                    })),
                }
            );

            setMessage(
                response.data?.message ||
                "Attendance saved successfully."
            );
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to save attendance."
            );
        } finally {
            setSaving(false);
        }
    }

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
        <div className="mx-auto max-w-5xl">
            <p className="text-sm font-semibold text-blue-600">
                Teacher Portal
            </p>
            <h1 className="mt-2 text-3xl font-bold text-slate-950">
                Class Attendance
            </h1>
            <p className="mt-2 text-sm text-slate-600">
                Mark attendance for classes where you are the
                class teacher or have temporary class-teacher access.
            </p>

            {loadingClasses ? (
                <p
                    role="status"
                    className="mt-8 rounded-2xl bg-white p-6 text-slate-600"
                >
                    Loading classes...
                </p>
            ) : classes.length === 0 && !error ? (
                <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-8 text-center">
                    <ClipboardCheck className="mx-auto h-10 w-10 text-slate-400" />
                    <h2 className="mt-4 text-lg font-bold text-slate-900">
                        No attendance classes assigned
                    </h2>
                    <p className="mt-2 text-sm text-slate-600">
                        A class-teacher assignment or active temporary
                        access is required to mark attendance.
                    </p>
                </div>
            ) : classes.length > 0 ? (
                <>
                    <div className="mt-8 grid gap-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:grid-cols-[1fr_auto_auto] sm:items-end">
                        <label className="block text-sm font-semibold text-slate-700">
                            Class
                            <select
                                value={selectedKey}
                                onChange={chooseClass}
                                className="mt-2 w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-3 text-slate-900"
                            >
                                {classes.map((item) => (
                                    <option
                                        key={classKey(item)}
                                        value={classKey(item)}
                                    >
                                        {item.academicYearName} ·{" "}
                                        {item.sectionName} ·{" "}
                                        {item.gradeName} ·{" "}
                                        {item.className}
                                    </option>
                                ))}
                            </select>
                        </label>

                        <label className="block text-sm font-semibold text-slate-700">
                            Date
                            <input
                                type="date"
                                value={date}
                                min={selected?.academicYearStartDate?.slice(
                                    0,
                                    10
                                )}
                                max={selected?.academicYearEndDate?.slice(
                                    0,
                                    10
                                )}
                                onChange={(event) =>
                                    setDate(event.target.value)
                                }
                                className="mt-2 w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-3 text-slate-900"
                            />
                        </label>

                        <button
                            type="button"
                            onClick={() =>
                                setReloadKey((value) => value + 1)
                            }
                            disabled={loadingRoster || saving}
                            className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                            <RefreshCw className="h-4 w-4" />
                            Refresh
                        </button>
                    </div>

                    {!date ? (
                        <p className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                            Choose an attendance date within the selected
                            academic year.
                        </p>
                    ) : loadingRoster ? (
                        <p
                            role="status"
                            className="mt-6 rounded-2xl bg-white p-6 text-slate-600"
                        >
                            Loading students...
                        </p>
                    ) : !error && students.length === 0 ? (
                        <p className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                            No active students are assigned to this class.
                        </p>
                    ) : students.length > 0 ? (
                        <form
                            onSubmit={save}
                            className="mt-6"
                        >
                            <div className="flex flex-wrap items-center justify-between gap-3">
                                <h2 className="text-lg font-bold text-slate-950">
                                    Students ({students.length})
                                </h2>
                                <button
                                    type="button"
                                    onClick={markAllPresent}
                                    className="cursor-pointer rounded-xl border border-blue-200 bg-blue-50 px-4 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-100"
                                >
                                    Mark all present
                                </button>
                            </div>

                            <div className="mt-4 space-y-3">
                                {students.map((student) => (
                                    <div
                                        key={student.id}
                                        className="grid gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:grid-cols-[minmax(0,1fr)_160px_minmax(0,1fr)] sm:items-center"
                                    >
                                        <div className="min-w-0">
                                            <p className="font-semibold text-slate-900">
                                                {student.fullName}
                                            </p>
                                            <p className="text-sm text-slate-500">
                                                {student.indexNumber}
                                            </p>
                                        </div>

                                        <label className="text-xs font-semibold text-slate-600">
                                            Status
                                            <select
                                                value={
                                                    draft[student.id]
                                                        ?.status || ""
                                                }
                                                onChange={(event) =>
                                                    changeStudent(
                                                        student.id,
                                                        "status",
                                                        event.target.value
                                                    )
                                                }
                                                required
                                                className="mt-1 block w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-900"
                                            >
                                                <option value="">
                                                    Select status
                                                </option>
                                                {statusOptions.map(
                                                    (option) => (
                                                        <option
                                                            key={
                                                                option.value
                                                            }
                                                            value={
                                                                option.value
                                                            }
                                                        >
                                                            {option.label}
                                                        </option>
                                                    )
                                                )}
                                            </select>
                                        </label>

                                        <label className="text-xs font-semibold text-slate-600">
                                            Remarks (optional)
                                            <input
                                                value={
                                                    draft[student.id]
                                                        ?.remarks || ""
                                                }
                                                onChange={(event) =>
                                                    changeStudent(
                                                        student.id,
                                                        "remarks",
                                                        event.target.value
                                                    )
                                                }
                                                maxLength={500}
                                                placeholder="Add a note"
                                                className="mt-1 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900"
                                            />
                                        </label>
                                    </div>
                                ))}
                            </div>

                            <button
                                type="submit"
                                disabled={saving || loadingRoster}
                                className="mt-5 w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                            >
                                {saving
                                    ? "Saving..."
                                    : "Save attendance"}
                            </button>
                        </form>
                    ) : null}
                </>
            ) : null}

            {error && (
                <p
                    role="alert"
                    className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700"
                >
                    {error}
                </p>
            )}

            {message && (
                <p
                    role="status"
                    className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700"
                >
                    {message}
                </p>
            )}
        </div>
    );
}