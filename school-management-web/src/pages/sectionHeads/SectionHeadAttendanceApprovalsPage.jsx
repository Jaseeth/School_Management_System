import { Check, ClipboardCheck, RefreshCw, X } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import api from "../../api/axios";
import { useAuth } from "../../context/AuthContext";

const statuses = {
    1: "Present",
    2: "Absent",
    3: "Late",
    4: "Excused",
};

function statusLabel(value) {
    return value == null ? "Not marked" : statuses[value] || String(value);
}

function formatDate(value) {
    if (!value) return "—";
    const [year, month, day] = value.slice(0, 10).split("-");
    return `${day}/${month}/${year}`;
}

export default function SectionHeadAttendanceApprovalsPage() {
    const { user } = useAuth();
    const allowed = user?.roles?.includes("Section Head");

    const [requests, setRequests] = useState([]);
    const [remarks, setRemarks] = useState({});
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [decision, setDecision] = useState(null);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    const loadRequests = useCallback(async (signal) => {
        setLoading(true);
        setError("");

        try {
            const response = await api.get(
                "/attendance/approval/requests/pending",
                { signal }
            );

            if (signal?.aborted) return;

            setRequests(
                Array.isArray(response.data?.requests)
                    ? response.data.requests
                    : []
            );
        } catch (err) {
            if (signal?.aborted) return;

            setError(
                err?.response?.data?.message ||
                "Unable to load attendance requests."
            );
        } finally {
            if (!signal?.aborted) setLoading(false);
        }
    }, []);

    useEffect(() => {
        if (!allowed) {
            setLoading(false);
            return;
        }

        const controller = new AbortController();
        loadRequests(controller.signal);

        return () => controller.abort();
    }, [allowed, loadRequests]);

    function chooseDecision(requestId, approve) {
        setDecision({ requestId, approve });
        setError("");
        setSuccess("");
    }

    async function confirmReview() {
        if (!decision || saving) return;

        const { requestId, approve } = decision;
        const reviewRemarks = (remarks[requestId] || "").trim();

        if (!approve && !reviewRemarks) {
            setError("Enter a review remark explaining the rejection.");
            return;
        }

        setSaving(true);
        setError("");
        setSuccess("");

        try {
            const response = await api.post(
                `/attendance/approval/requests/${requestId}/review`,
                {
                    approve,
                    remarks: reviewRemarks || null,
                }
            );

            setRequests((current) =>
                current.filter((request) => request.id !== requestId)
            );
            setDecision(null);

            setRemarks((current) => {
                const updated = { ...current };
                delete updated[requestId];
                return updated;
            });

            setSuccess(
                response.data?.message ||
                (approve
                    ? "Attendance approved and applied."
                    : "Request rejected; attendance unchanged.")
            );
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to review this request."
            );
        } finally {
            setSaving(false);
        }
    }

    if (!allowed) {
        return (
            <p role="alert" className="rounded-xl bg-amber-50 p-5 text-sm text-amber-900">
                This page is available to Section Heads only.
            </p>
        );
    }

    return (
        <div className="mx-auto w-full max-w-6xl">
            <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                    <p className="text-sm font-semibold text-blue-600">
                        Section Head Portal
                    </p>
                    <h1 className="mt-2 text-2xl font-bold text-slate-950 sm:text-3xl">
                        Attendance Approvals
                    </h1>
                    <p className="mt-2 text-sm text-slate-600">
                        Review attendance requests for your assigned sections.
                    </p>
                </div>

                <button
                    type="button"
                    onClick={() => {
                        setDecision(null);
                        loadRequests();
                    }}
                    disabled={loading || saving}
                    className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                >
                    <RefreshCw className="h-4 w-4" />
                    Refresh
                </button>
            </div>

            {error && (
                <p role="alert" className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                    {error}
                </p>
            )}

            {success && (
                <p role="status" className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">
                    {success}
                </p>
            )}

            {loading ? (
                <p role="status" className="mt-6 rounded-2xl bg-white p-6 text-slate-600">
                    Loading attendance requests...
                </p>
            ) : requests.length === 0 ? (
                <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm">
                    <ClipboardCheck className="mx-auto h-10 w-10 text-slate-400" />
                    <h2 className="mt-4 text-lg font-bold text-slate-900">
                        {error ? "Unable to load requests" : "No pending requests"}
                    </h2>
                    <p className="mt-2 text-sm text-slate-600">
                        {error
                            ? "Click Refresh to try again."
                            : "New teacher attendance requests will appear here."}
                    </p>
                </div>
            ) : (
                <div className="mt-6 space-y-6">
                    <p className="text-sm font-semibold text-slate-600">
                        Pending requests: {requests.length}
                    </p>

                    {requests.map((request) => {
                        const students = request.students || [];
                        const selectedDecision =
                            decision?.requestId === request.id ? decision : null;

                        return (
                            <article
                                key={request.id}
                                className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"
                            >
                                <div className="flex flex-wrap items-center justify-between gap-3">
                                    <h2 className="text-lg font-bold text-slate-950">
                                        Grade {request.gradeName} · Class {request.className}
                                    </h2>
                                    <span className="rounded-full bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700">
                                        Pending · #{request.id}
                                    </span>
                                </div>

                                <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
                                    <Detail label="Teacher" value={`${request.teacherName} (${request.staffNumber})`} />
                                    <Detail label="Section" value={request.sectionName} />
                                    <Detail label="Attendance date" value={formatDate(request.attendanceDate)} />
                                    <Detail
                                        label="Scope"
                                        value={`${request.isWholeClass ? "Whole class" : "Selected students"} · ${students.length} student(s)`}
                                    />
                                </dl>

                                <div className="mt-5 rounded-xl bg-slate-50 p-4">
                                    <p className="text-xs font-semibold text-slate-500">
                                        Overall reason
                                    </p>
                                    <p className="mt-1 whitespace-pre-wrap break-words text-sm text-slate-800">
                                        {request.reason?.trim() || "No overall reason provided."}
                                    </p>
                                </div>

                                <h3 className="mt-6 font-semibold text-slate-950">
                                    Proposed attendance
                                </h3>

                                <div className="mt-3 space-y-3">
                                    {students.map((student) => (
                                        <div
                                            key={student.studentId}
                                            className="grid gap-4 rounded-xl border border-slate-200 p-4 sm:grid-cols-3"
                                        >
                                            <div className="min-w-0">
                                                <p className="break-words font-semibold text-slate-900">
                                                    {student.studentFullName}
                                                </p>
                                                <p className="mt-1 text-xs text-slate-500">
                                                    {student.studentIndexNumber}
                                                </p>
                                            </div>

                                            <AttendanceValue
                                                label="Saved at submission"
                                                status={student.previousStatus}
                                                remarks={student.previousRemarks}
                                            />

                                            <AttendanceValue
                                                label="Proposed"
                                                status={student.proposedStatus}
                                                remarks={student.proposedRemarks}
                                                proposed
                                            />
                                        </div>
                                    ))}
                                </div>

                                <label className="mt-5 block text-sm font-semibold text-slate-700">
                                    Review remarks
                                    <span className="ml-1 font-normal text-slate-500">
                                        (required when rejecting)
                                    </span>
                                    <textarea
                                        value={remarks[request.id] || ""}
                                        onChange={(event) => {
                                            const value = event.target.value;
                                            setRemarks((current) => ({
                                                ...current,
                                                [request.id]: value,
                                            }));
                                            setError("");
                                        }}
                                        disabled={saving}
                                        rows={3}
                                        maxLength={1000}
                                        placeholder="Enter your review remarks"
                                        className="mt-2 block w-full rounded-xl border border-slate-200 p-3 font-normal text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:opacity-60"
                                    />
                                </label>

                                {selectedDecision ? (
                                    <div className="mt-5 rounded-xl border border-slate-200 bg-slate-50 p-4">
                                        <p className="text-sm text-slate-700">
                                            {selectedDecision.approve
                                                ? "Approve and apply the proposed attendance?"
                                                : "Reject and keep saved attendance unchanged?"}
                                        </p>
                                        <div className="mt-3 flex flex-wrap gap-3">
                                            <button
                                                type="button"
                                                onClick={confirmReview}
                                                disabled={saving}
                                                className="w-full cursor-pointer rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                                            >
                                                {saving
                                                    ? "Saving..."
                                                    : selectedDecision.approve
                                                        ? "Confirm approval"
                                                        : "Confirm rejection"}
                                            </button>
                                            <button
                                                type="button"
                                                onClick={() => setDecision(null)}
                                                disabled={saving}
                                                className="w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                                            >
                                                Cancel
                                            </button>
                                        </div>
                                    </div>
                                ) : (
                                    <div className="mt-5 flex flex-wrap gap-3">
                                        <button
                                            type="button"
                                            onClick={() => chooseDecision(request.id, true)}
                                            disabled={saving}
                                            className="inline-flex w-full cursor-pointer items-center justify-center gap-2 rounded-xl bg-emerald-600 px-4 py-3 text-sm font-semibold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                                        >
                                            <Check className="h-4 w-4" />
                                            Approve
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() => chooseDecision(request.id, false)}
                                            disabled={saving}
                                            className="inline-flex w-full cursor-pointer items-center justify-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700 hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                                        >
                                            <X className="h-4 w-4" />
                                            Reject
                                        </button>
                                    </div>
                                )}
                            </article>
                        );
                    })}
                </div>
            )}
        </div>
    );
}

function Detail({ label, value }) {
    return (
        <div className="min-w-0">
            <dt className="text-slate-500">{label}</dt>
            <dd className="mt-1 break-words font-semibold text-slate-900">
                {value}
            </dd>
        </div>
    );
}

function AttendanceValue({ label, status, remarks, proposed = false }) {
    return (
        <div className="min-w-0">
            <p className="text-xs font-semibold text-slate-500">{label}</p>
            <p className={`mt-1 text-sm font-semibold ${proposed ? "text-blue-700" : "text-slate-800"}`}>
                {statusLabel(status)}
            </p>
            <p className="mt-1 whitespace-pre-wrap break-words text-xs text-slate-600">
                {remarks || "No remark"}
            </p>
        </div>
    );
}