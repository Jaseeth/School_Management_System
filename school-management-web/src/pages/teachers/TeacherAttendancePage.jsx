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

function schoolDate() {
    const parts = new Intl.DateTimeFormat("en-US", {
        timeZone: "Asia/Colombo",
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
    }).formatToParts(new Date());

    const value = (type) => parts.find((part) => part.type === type)?.value;
    return `${value("year")}-${value("month")}-${value("day")}`;
}

function classKey(item) {
    return `${item.academicYearId}:${item.schoolClassId}`;
}

function dateForClass(item) {
    const today = schoolDate();
    const start = item.academicYearStartDate?.slice(0, 10);
    const end = item.academicYearEndDate?.slice(0, 10);

    if ((start && today < start) || (end && today > end)) return "";
    return today;
}

function formatSchoolTime(value) {
    if (!value) return "";
    return new Date(value).toLocaleTimeString("en-LK", {
        timeZone: "Asia/Colombo",
        hour: "2-digit",
        minute: "2-digit",
    });
}

function isPending(request) {
    return request.status === 1 || request.status === "Pending";
}

function requestStatus(request) {
    if (isPending(request)) return "Pending";
    if (request.status === 2 || request.status === "Approved") return "Approved";
    return "Rejected";
}

export default function TeacherAttendancePage() {
    const { user } = useAuth();
    const isTeacher = user?.roles?.includes("Teacher");

    const [classes, setClasses] = useState([]);
    const [selectedKey, setSelectedKey] = useState("");
    const [date, setDate] = useState(schoolDate);
    const [students, setStudents] = useState([]);
    const [draft, setDraft] = useState({});
    const [attendanceWindow, setAttendanceWindow] = useState(null);
    const [requests, setRequests] = useState([]);
    const [reason, setReason] = useState("");
    const [loadingClasses, setLoadingClasses] = useState(true);
    const [loadingRoster, setLoadingRoster] = useState(false);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [reloadKey, setReloadKey] = useState(0);

    const selected = classes.find((item) => classKey(item) === selectedKey);
    const canSaveDirectly = attendanceWindow?.canSaveDirectly === true;
    const pendingRequest = requests.some(isPending);

    useEffect(() => {
        if (!isTeacher) {
            setLoadingClasses(false);
            return;
        }

        let active = true;

        async function loadClasses() {
            setLoadingClasses(true);
            setError("");

            try {
                const response = await api.get("/attendance/my/classes");
                if (!active) return;

                const list = Array.isArray(response.data?.classes)
                    ? response.data.classes
                    : [];

                setClasses(list);

                if (list.length > 0) {
                    setSelectedKey(classKey(list[0]));
                    setDate(dateForClass(list[0]));
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
        return () => { active = false; };
    }, [isTeacher]);

    useEffect(() => {
        if (!selected || !date) return;

        const start = selected.academicYearStartDate?.slice(0, 10);
        const end = selected.academicYearEndDate?.slice(0, 10);

        if ((start && date < start) || (end && date > end)) {
            setStudents([]);
            setDraft({});
            setAttendanceWindow(null);
            setRequests([]);
            setError("Choose a date within the selected academic year.");
            return;
        }

        let active = true;

        async function loadAttendance() {
            setLoadingRoster(true);
            setError("");
            setStudents([]);
            setDraft({});
            setAttendanceWindow(null);
            setRequests([]);

            const params = {
                academicYearId: selected.academicYearId,
                schoolClassId: selected.schoolClassId,
                attendanceDate: date,
            };

            try {
                const [rosterResponse, windowResponse, requestsResponse] =
                    await Promise.all([
                        api.get(
                            `/attendance/class/${selected.schoolClassId}/students`,
                            {
                                params: {
                                    academicYearId: params.academicYearId,
                                    attendanceDate: params.attendanceDate,
                                },
                            }
                        ),
                        api.get("/attendance/windows", { params }),
                        api.get("/attendance/approval/requests/my", { params }),
                    ]);

                if (!active) return;

                const list = Array.isArray(rosterResponse.data?.students)
                    ? rosterResponse.data.students
                    : [];

                const initialDraft = {};

                for (const student of list) {
                    const named = statusOptions.find(
                        (option) => option.label === student.statusName
                    );

                    initialDraft[student.id] = {
                        status: student.status == null
                            ? (named?.value || "")
                            : String(student.status),
                        remarks: student.remarks || "",
                    };
                }

                setStudents(list);
                setDraft(initialDraft);
                setAttendanceWindow(windowResponse.data);
                setRequests(
                    Array.isArray(requestsResponse.data?.requests)
                        ? requestsResponse.data.requests
                        : []
                );
            } catch (err) {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load attendance. Check that the approval API is running."
                    );
                }
            } finally {
                if (active) setLoadingRoster(false);
            }
        }

        loadAttendance();
        return () => { active = false; };
    }, [selectedKey, date, reloadKey, classes]);

    useEffect(() => {
        if (!selected || !date) return;

        let active = true;

        const interval = window.setInterval(async () => {
            try {
                const response = await api.get("/attendance/windows", {
                    params: {
                        academicYearId: selected.academicYearId,
                        schoolClassId: selected.schoolClassId,
                        attendanceDate: date,
                    },
                });

                if (active) setAttendanceWindow(response.data);
            } catch {
                // The Refresh button can retry a failed window request.
            }
        }, 15000);

        return () => {
            active = false;
            window.clearInterval(interval);
        };
    }, [selectedKey, date, classes]);

    function chooseClass(event) {
        const next = classes.find(
            (item) => classKey(item) === event.target.value
        );

        if (!next) return;

        setSelectedKey(classKey(next));
        setDate(dateForClass(next));
        setReason("");
        setMessage("");
    }

    function changeStudent(studentId, field, value) {
        setDraft((current) => ({
            ...current,
            [studentId]: {
                ...current[studentId],
                [field]: value,
            },
        }));
        setError("");
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

        setError("");
        setMessage("");
    }

    async function save(event) {
        event.preventDefault();

        if (!selected || saving || students.length === 0 || !attendanceWindow) {
            return;
        }

        setError("");
        setMessage("");

        // Refresh the server's decision immediately before submitting.
        let latestWindow;

        try {
            const response = await api.get("/attendance/windows", {
                params: {
                    academicYearId: selected.academicYearId,
                    schoolClassId: selected.schoolClassId,
                    attendanceDate: date,
                },
            });

            latestWindow = response.data;
            setAttendanceWindow(latestWindow);
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to check the attendance window. Please try again."
            );
            return;
        }

        const directSave = latestWindow?.canSaveDirectly === true;

        if (directSave && students.some(
            (student) => !draft[student.id]?.status
        )) {
            setError("Select a status for every student before saving.");
            return;
        }

        const changed = students.filter((student) => {
            const savedStatus = student.status == null
                ? ""
                : String(student.status);

            const savedRemarks = (student.remarks || "").trim();
            const newStatus = draft[student.id]?.status || "";
            const newRemarks = (draft[student.id]?.remarks || "").trim();

            return newStatus &&
                (newStatus !== savedStatus || newRemarks !== savedRemarks);
        });

        if (!directSave) {
            if (pendingRequest) {
                setError(
                    "An attendance request is already pending for this class and date."
                );
                return;
            }

            if (changed.length === 0) {
                setError("Change at least one student's attendance before submitting.");
                return;
            }

            // if (!reason.trim()) {
            //     setError("Enter a reason for section head approval.");
            //     return;
            // }

            if (
                changed.length < students.length &&
                changed.some(
                    (student) => !draft[student.id]?.remarks?.trim()
                )
            ) {
                setError(
                    "Add an individual remark for each changed student."
                );
                return;
            }
        }

        const payload = {
            academicYearId: selected.academicYearId,
            schoolClassId: selected.schoolClassId,
            attendanceDate: `${date}T00:00:00`,
            students: (directSave ? students : changed).map((student) => ({
                studentId: student.id,
                status: Number(draft[student.id].status),
                remarks: draft[student.id].remarks?.trim() || null,
            })),
        };

        setSaving(true);

        try {
            const response = directSave
                ? await api.post("/attendance/mark", payload)
                : await api.post("/attendance/approval/requests", {
                    ...payload,
                    reason: reason.trim(),
                });

            setMessage(
                response.data?.message ||
                (directSave
                    ? "Attendance saved."
                    : "Request sent for section head approval.")
            );
            setReason("");
            setReloadKey((current) => current + 1);
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to submit attendance. Please try again."
            );

            if (err?.response?.status === 409) {
                setReloadKey((current) => current + 1);
            }
        } finally {
            setSaving(false);
        }
    }

    if (!isTeacher) {
        return (
            <p role="alert" className="rounded-2xl bg-amber-50 p-5 text-amber-900">
                This page is available to teachers only.
            </p>
        );
    }

    return (
        <div className="mx-auto max-w-5xl">
            <p className="text-sm font-semibold text-blue-600">Teacher Portal</p>
            <h1 className="mt-2 text-3xl font-bold text-slate-950">
                Class Attendance
            </h1>
            <p className="mt-2 text-sm text-slate-600">
                Mark attendance for classes where you are the class teacher
                or have temporary class-teacher access.
            </p>

            {loadingClasses ? (
                <p role="status" className="mt-8 rounded-2xl bg-white p-6 text-slate-600">
                    Loading classes...
                </p>
            ) : classes.length === 0 && !error ? (
                <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-8 text-center">
                    <ClipboardCheck className="mx-auto h-10 w-10 text-slate-400" />
                    <h2 className="mt-4 text-lg font-bold text-slate-900">
                        No attendance classes assigned
                    </h2>
                    <p className="mt-2 text-sm text-slate-600">
                        A class-teacher assignment or active temporary access is required.
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
                                    <option key={classKey(item)} value={classKey(item)}>
                                        {item.academicYearName} · {item.sectionName} ·{" "}
                                        {item.gradeName} · {item.className}
                                    </option>
                                ))}
                            </select>
                        </label>

                        <label className="block text-sm font-semibold text-slate-700">
                            Date
                            <input
                                type="date"
                                value={date}
                                min={selected?.academicYearStartDate?.slice(0, 10)}
                                max={selected?.academicYearEndDate?.slice(0, 10)}
                                onChange={(event) => {
                                    setDate(event.target.value);
                                    setMessage("");
                                }}
                                className="mt-2 w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-3 text-slate-900"
                            />
                        </label>

                        <button
                            type="button"
                            onClick={() => setReloadKey((current) => current + 1)}
                            disabled={loadingRoster || saving}
                            className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                            <RefreshCw className="h-4 w-4" />
                            Refresh
                        </button>
                    </div>

                    {!date ? (
                        <p className="mt-6 rounded-2xl bg-white p-6 text-slate-600">
                            Choose a date within the selected academic year.
                        </p>
                    ) : loadingRoster ? (
                        <p role="status" className="mt-6 rounded-2xl bg-white p-6 text-slate-600">
                            Loading students...
                        </p>
                    ) : students.length > 0 ? (
                        <form onSubmit={save} className="mt-6">
                            {attendanceWindow && (
                                <p className="rounded-xl border border-blue-100 bg-blue-50 p-4 text-sm text-blue-900">
                                    {attendanceWindow.firstPeriodStartsAt
                                        ? `First period: ${formatSchoolTime(
                                            attendanceWindow.firstPeriodStartsAt
                                        )}. Direct save cutoff: ${formatSchoolTime(
                                            attendanceWindow.extendedUntil ||
                                            attendanceWindow.regularClosesAt
                                        )} Sri Lanka time.`
                                        : "No first period is configured for this date."}
                                    {" "}
                                    {canSaveDirectly
                                        ? "Changes save immediately."
                                        : "Changes require section head approval."}
                                </p>
                            )}

                            {requests.length > 0 && (
                                <div className="mt-4 rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-700">
                                    {requests.map((request) => (
                                        <p key={request.id} className="py-1">
                                            Request #{request.id}: {requestStatus(request)}
                                            {request.studentCount != null &&
                                                ` · ${request.studentCount} student(s)`}
                                            {request.reviewRemarks &&
                                                ` · ${request.reviewRemarks}`}
                                        </p>
                                    ))}
                                </div>
                            )}

                            <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
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
                                                value={draft[student.id]?.status || ""}
                                                onChange={(event) => changeStudent(
                                                    student.id,
                                                    "status",
                                                    event.target.value
                                                )}
                                                required={canSaveDirectly}
                                                className="mt-1 block w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-900"
                                            >
                                                <option value="">Select status</option>
                                                {statusOptions.map((option) => (
                                                    <option
                                                        key={option.value}
                                                        value={option.value}
                                                    >
                                                        {option.label}
                                                    </option>
                                                ))}
                                            </select>
                                        </label>

                                        <label className="text-xs font-semibold text-slate-600">
                                            Remarks
                                            <input
                                                value={draft[student.id]?.remarks || ""}
                                                onChange={(event) => changeStudent(
                                                    student.id,
                                                    "remarks",
                                                    event.target.value
                                                )}
                                                maxLength={500}
                                                placeholder={
                                                    canSaveDirectly
                                                        ? "Optional note"
                                                        : "Required for a partial request"
                                                }
                                                className="mt-1 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900"
                                            />
                                        </label>
                                    </div>
                                ))}
                            </div>

                            {!canSaveDirectly && attendanceWindow && (
                                <label className="mt-5 block text-sm font-semibold text-slate-700">
                                    Reason for section head approval (optional)
                                    <textarea
                                        value={reason}
                                        onChange={(event) => setReason(event.target.value)}
                                        maxLength={1000}
                                        rows={3}
                                        placeholder="Explain the late entry or correction"
                                        className="mt-2 block w-full rounded-xl border border-slate-200 p-3 text-slate-900"
                                    />
                                </label>
                            )}

                            <button
                                type="submit"
                                disabled={
                                    saving ||
                                    loadingRoster ||
                                    !attendanceWindow ||
                                    (!canSaveDirectly && pendingRequest)
                                }
                                className="mt-5 w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                            >
                                {saving
                                    ? "Submitting..."
                                    : canSaveDirectly
                                        ? "Save attendance"
                                        : pendingRequest
                                            ? "Waiting for section head review"
                                            : "Submit for approval"}
                            </button>
                        </form>
                    ) : !error ? (
                        <p className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                            No active students are assigned to this class.
                        </p>
                    ) : null}
                </>
            ) : null}

            {error && (
                <p role="alert" className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                    {error}
                </p>
            )}

            {message && (
                <p role="status" className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">
                    {message}
                </p>
            )}
        </div>
    );
}