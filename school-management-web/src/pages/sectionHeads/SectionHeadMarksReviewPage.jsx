import { useEffect, useRef, useState } from "react";
import { Check, Eye, RefreshCw, RotateCcw } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import { marksReviewApi } from "../../api/marksReviewApi";

const buttonClass =
    "inline-flex cursor-pointer items-center justify-center gap-2 " +
    "rounded-xl px-4 py-2.5 text-sm font-semibold transition " +
    "disabled:cursor-not-allowed disabled:opacity-50";

function errorMessage(error) {
    const data = error?.response?.data;

    if (data?.message) return data.message;

    if (data?.errors) {
        const messages = Array.isArray(data.errors)
            ? data.errors
            : Object.values(data.errors).flat();

        if (messages.length) return messages.join(" ");
    }

    if (error?.response?.status === 403) {
        return "You do not have access to review these marks.";
    }

    if (error?.response?.status === 401) {
        return "Your session has expired. Please sign in again.";
    }

    if (!error?.response) {
        return "Cannot reach the backend. Check that the API is running.";
    }

    return "Unable to complete the request. Please try again.";
}

function schoolDate(value) {
    if (!value) return "—";

    const hasOffset = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value);
    const date = new Date(hasOffset ? value : `${value}Z`);

    if (Number.isNaN(date.getTime())) return "—";

    return date.toLocaleString("en-GB", {
        timeZone: "Asia/Colombo",
        dateStyle: "medium",
        timeStyle: "short",
    });
}

export default function SectionHeadMarksReviewPage() {
    const { user } = useAuth();
    const isSectionHead =
        user?.roles?.includes("Section Head") === true;

    const [pending, setPending] = useState([]);
    const [details, setDetails] = useState(null);
    const [selectedId, setSelectedId] = useState(null);
    const [comment, setComment] = useState("");
    const [confirmation, setConfirmation] = useState("");

    const [loading, setLoading] = useState(true);
    const [loadingDetails, setLoadingDetails] = useState(false);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [reload, setReload] = useState(0);

    const detailsRequest = useRef(0);

    useEffect(() => {
        let cancelled = false;

        if (!isSectionHead) {
            setLoading(false);
            return;
        }

        async function loadPending() {
            setLoading(true);
            setError("");

            try {
                const response = await marksReviewApi.getPending();

                if (!cancelled) {
                    setPending(response.data ?? []);
                }
            } catch (err) {
                if (!cancelled) {
                    setPending([]);
                    setError(errorMessage(err));
                }
            } finally {
                if (!cancelled) setLoading(false);
            }
        }

        loadPending();

        return () => {
            cancelled = true;
        };
    }, [isSectionHead, reload]);

    useEffect(() => {
        return () => {
            detailsRequest.current += 1;
        };
    }, []);

    async function openSubmission(id) {
        const requestId = ++detailsRequest.current;

        setSelectedId(id);
        setDetails(null);
        setComment("");
        setConfirmation("");
        setError("");
        setSuccess("");
        setLoadingDetails(true);

        try {
            const response = await marksReviewApi.getSubmission(id);

            if (requestId === detailsRequest.current) {
                setDetails(response.data);
            }
        } catch (err) {
            if (requestId === detailsRequest.current) {
                setError(errorMessage(err));
            }
        } finally {
            if (requestId === detailsRequest.current) {
                setLoadingDetails(false);
            }
        }
    }

    function refresh() {
        detailsRequest.current += 1;
        setDetails(null);
        setSelectedId(null);
        setLoadingDetails(false);
        setComment("");
        setConfirmation("");
        setSuccess("");
        setReload((current) => current + 1);
    }

    function prepareReview(action) {
        setError("");
        setSuccess("");

        if (action === "reject" && !comment.trim()) {
            setError("Enter a comment explaining the required correction.");
            return;
        }

        setConfirmation(action);
    }

    async function confirmReview() {
        const submission = details?.submission;

        if (
            !submission ||
            submission.status !== "Submitted" ||
            !confirmation ||
            saving
        ) {
            return;
        }

        const action = confirmation;

        if (action === "reject" && !comment.trim()) {
            setError("A comment is required when returning marks.");
            return;
        }

        setSaving(true);
        setError("");
        setSuccess("");

        try {
            const response =
                action === "approve"
                    ? await marksReviewApi.approve(
                        submission.id,
                        comment
                    )
                    : await marksReviewApi.reject(
                        submission.id,
                        comment
                    );

            setPending((current) =>
                current.filter((item) => item.id !== submission.id)
            );

            setDetails((current) => ({
                ...current,
                submission: {
                    ...current.submission,
                    status:
                        action === "approve" ? "Approved" : "Rejected",
                    reviewComment: comment.trim() || null,
                    reviewedAt: response.data?.reviewedAt ?? null,
                },
            }));

            setSuccess(
                response.data?.message ||
                (action === "approve"
                    ? "Marks approved successfully."
                    : "Marks returned for correction.")
            );

            setConfirmation("");
        } catch (err) {
            setError(errorMessage(err));
            setConfirmation("");
        } finally {
            setSaving(false);
        }
    }

    if (!isSectionHead) {
        return (
            <div className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600">
                This page is available to Section Heads.
            </div>
        );
    }

    const submission = details?.submission;
    const marks = details?.marks ?? [];

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                    <p className="text-sm font-semibold text-blue-600">
                        Section Head Portal
                    </p>

                    <h1 className="mt-2 text-2xl font-bold text-slate-950 sm:text-3xl">
                        Marks Review
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        Review submitted marks or return them for correction.
                    </p>
                </div>

                <button
                    type="button"
                    onClick={refresh}
                    disabled={loading || saving || loadingDetails}
                    className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}
                >
                    <RefreshCw className="h-4 w-4" />
                    Refresh
                </button>
            </div>

            {error && (
                <p
                    role="alert"
                    className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700"
                >
                    {error}
                </p>
            )}

            {success && (
                <p
                    role="status"
                    className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700"
                >
                    {success}
                </p>
            )}

            <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                <h2 className="text-lg font-bold text-slate-900">
                    Pending submissions
                    {!loading && ` (${pending.length})`}
                </h2>

                {loading ? (
                    <p role="status" className="mt-5 text-sm text-slate-500">
                        Loading submissions...
                    </p>
                ) : pending.length === 0 ? (
                    <p className="mt-5 text-sm text-slate-500">
                        {error
                            ? "Submissions could not be loaded."
                            : "No marks submissions are waiting for your review. Only submissions for your active section and academic-year assignments appear. If you expected a submission, ask Admin to check Section Head Assignments for that year."}
                    </p>
                ) : (
                    <div className="mt-5 grid gap-4 xl:grid-cols-2">
                        {pending.map((item) => (
                            <article
                                key={item.id}
                                className={`rounded-xl border p-4 ${selectedId === item.id
                                        ? "border-blue-300 bg-blue-50"
                                        : "border-slate-200"
                                    }`}
                            >
                                <h3 className="font-bold text-slate-900">
                                    {item.exam.name} — {item.subject.name}
                                </h3>

                                <p className="mt-2 text-sm text-slate-600">
                                    {item.section.name}
                                    {" · Grade "}
                                    {item.grade.name}
                                    {" · Class "}
                                    {item.schoolClass.name}
                                </p>

                                <p className="mt-1 text-sm text-slate-500">
                                    {item.academicYear.name}
                                    {" · "}
                                    {item.teacher.fullName}
                                    {" ("}
                                    {item.teacher.staffNumber}
                                    {")"}
                                </p>

                                <p className="mt-2 text-xs text-slate-500">
                                    Submitted: {schoolDate(item.submittedAt)}
                                    {" · Sri Lanka time"}
                                </p>

                                <button
                                    type="button"
                                    onClick={() => openSubmission(item.id)}
                                    disabled={saving || loadingDetails}
                                    className={`${buttonClass} mt-4 bg-blue-600 text-white hover:bg-blue-700`}
                                >
                                    <Eye className="h-4 w-4" />
                                    View Marks
                                </button>
                            </article>
                        ))}
                    </div>
                )}
            </section>

            {loadingDetails && (
                <p
                    role="status"
                    className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-500"
                >
                    Loading student marks...
                </p>
            )}

            {submission && !loadingDetails && (
                <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                    <div className="border-b border-slate-200 p-5 sm:p-6">
                        <h2 className="text-lg font-bold text-slate-900">
                            {submission.exam.name}
                            {" — "}
                            {submission.subject.name}
                        </h2>

                        <p className="mt-2 text-sm text-slate-500">
                            {submission.section.name}
                            {" · Grade "}
                            {submission.grade.name}
                            {" · Class "}
                            {submission.schoolClass.name}
                            {" · "}
                            {submission.academicYear.name}
                        </p>

                        <p className="mt-2 text-sm text-slate-500">
                            Teacher: {submission.teacher.fullName}
                            {" ("}
                            {submission.teacher.staffNumber}
                            {")"}
                        </p>

                        <p className="mt-2 text-sm font-semibold text-blue-700">
                            Status: {submission.status}
                            {" · Students: "}
                            {marks.length}
                            {" · Maximum marks: "}
                            {submission.exam.maximumMarks}
                        </p>
                    </div>

                    <div className="divide-y divide-slate-100">
                        {marks.map((mark) => (
                            <div
                                key={mark.id}
                                className="flex items-center justify-between gap-4 p-5 sm:px-6"
                            >
                                <div className="min-w-0">
                                    <p className="break-words font-semibold text-slate-900">
                                        {mark.fullName}
                                    </p>

                                    <p className="mt-1 text-sm text-slate-500">
                                        {mark.indexNumber}
                                    </p>
                                </div>

                                <p className="shrink-0 font-bold text-slate-900">
                                    {mark.marksObtained}
                                    <span className="ml-1 text-sm font-normal text-slate-500">
                                        / {submission.exam.maximumMarks}
                                    </span>
                                </p>
                            </div>
                        ))}
                    </div>

                    {submission.status === "Submitted" ? (
                        <div className="space-y-4 border-t border-slate-200 p-5 sm:p-6">
                            <label className="block text-sm font-semibold text-slate-700">
                                Review comment
                                <textarea
                                    value={comment}
                                    onChange={(event) => {
                                        setComment(event.target.value);
                                        setConfirmation("");
                                        setError("");
                                    }}
                                    disabled={saving}
                                    rows={3}
                                    placeholder="Optional for approval; required when returning for correction."
                                    className="mt-2 block w-full rounded-xl border border-slate-200 px-4 py-3 text-sm font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                                />
                            </label>

                            <div className="flex flex-col gap-3 sm:flex-row">
                                <button
                                    type="button"
                                    onClick={() => prepareReview("approve")}
                                    disabled={saving || marks.length === 0}
                                    className={`${buttonClass} bg-emerald-600 text-white hover:bg-emerald-700`}
                                >
                                    <Check className="h-4 w-4" />
                                    Approve Marks
                                </button>

                                <button
                                    type="button"
                                    onClick={() => prepareReview("reject")}
                                    disabled={saving}
                                    className={`${buttonClass} border border-amber-300 text-amber-800 hover:bg-amber-50`}
                                >
                                    <RotateCcw className="h-4 w-4" />
                                    Return for Correction
                                </button>
                            </div>

                            {confirmation && (
                                <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                                    <p className="text-sm text-slate-700">
                                        {confirmation === "approve"
                                            ? "Approve this submission? Results will still require publishing."
                                            : "Return this submission? The teacher will be able to edit and resubmit the marks."}
                                    </p>

                                    <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                                        <button
                                            type="button"
                                            onClick={confirmReview}
                                            disabled={saving}
                                            className={`${buttonClass} bg-blue-600 text-white hover:bg-blue-700`}
                                        >
                                            {saving ? "Saving..." : "Confirm"}
                                        </button>

                                        <button
                                            type="button"
                                            onClick={() => setConfirmation("")}
                                            disabled={saving}
                                            className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-100`}
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>
                    ) : (
                        <div className="border-t border-slate-200 p-5 sm:p-6">
                            <p className="text-sm text-slate-600">
                                {submission.status === "Approved"
                                    ? "Approved. Results still require publishing before students can see them."
                                    : submission.status === "Rejected"
                                        ? "Returned for correction. The teacher can edit and resubmit."
                                        : `Submission status: ${submission.status}.`}
                            </p>

                            {submission.reviewComment && (
                                <p className="mt-3 whitespace-pre-wrap break-words text-sm text-slate-600">
                                    Comment: {submission.reviewComment}
                                </p>
                            )}
                        </div>
                    )}
                </section>
            )}
        </div>
    );
}
