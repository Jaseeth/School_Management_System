import { useEffect, useRef, useState } from "react";
import { Eye, RefreshCw, Send } from "lucide-react";
import { resultsPublishingApi } from "../../api/resultsPublishingApi";

const buttonClass =
    "inline-flex cursor-pointer items-center justify-center gap-2 " +
    "rounded-xl px-4 py-2.5 text-sm font-semibold transition " +
    "disabled:cursor-not-allowed disabled:opacity-50";

function getError(error) {
    if (error?.response?.data?.message) {
        return error.response.data.message;
    }

    if (error?.response?.status === 401) {
        return "Please sign out and sign in again.";
    }

    if (error?.response?.status === 403) {
        return "Your account does not have publishing permission.";
    }

    if (!error?.response) {
        return "Cannot reach the backend. Check that the API is running.";
    }

    return "The request failed. Check the backend error log.";
}

function formatDate(value) {
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

export default function ResultsPublishingPage() {
    const [tab, setTab] = useState("approved");
    const [items, setItems] = useState([]);
    const [details, setDetails] = useState(null);
    const [loading, setLoading] = useState(true);
    const [loadingDetails, setLoadingDetails] = useState(false);
    const [publishing, setPublishing] = useState(false);
    const [confirming, setConfirming] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [reload, setReload] = useState(0);

    const requestVersion = useRef(0);

    useEffect(() => {
        let cancelled = false;

        async function loadList() {
            setLoading(true);
            setItems([]);
            setError("");

            try {
                const response =
                    tab === "approved"
                        ? await resultsPublishingApi.getApproved()
                        : await resultsPublishingApi.getPublished();

                if (!cancelled) {
                    setItems(response.data ?? []);
                }
            } catch (err) {
                if (!cancelled) {
                    setError(getError(err));
                }
            } finally {
                if (!cancelled) setLoading(false);
            }
        }

        loadList();

        return () => {
            cancelled = true;
        };
    }, [tab, reload]);

    useEffect(() => {
        return () => {
            requestVersion.current += 1;
        };
    }, []);

    function clearDetails() {
        requestVersion.current += 1;
        setDetails(null);
        setLoadingDetails(false);
        setConfirming(false);
    }

    function changeTab(value) {
        if (publishing) return;

        clearDetails();
        setError("");
        setSuccess("");
        setTab(value);
    }

    function refresh() {
        clearDetails();
        setError("");
        setSuccess("");
        setReload((current) => current + 1);
    }

    async function viewMarks(id) {
        const version = ++requestVersion.current;

        setDetails(null);
        setConfirming(false);
        setError("");
        setSuccess("");
        setLoadingDetails(true);

        try {
            const response =
                await resultsPublishingApi.getSubmission(id);

            if (version === requestVersion.current) {
                setDetails(response.data);
            }
        } catch (err) {
            if (version === requestVersion.current) {
                setError(getError(err));
            }
        } finally {
            if (version === requestVersion.current) {
                setLoadingDetails(false);
            }
        }
    }

    async function publish() {
        const submission = details?.submission;

        if (
            publishing ||
            !confirming ||
            submission?.status !== "Approved"
        ) {
            return;
        }

        setPublishing(true);
        setError("");
        setSuccess("");

        try {
            const response =
                await resultsPublishingApi.publish(submission.id);

            setItems((current) =>
                current.filter((item) => item.id !== submission.id)
            );

            setDetails((current) => ({
                ...current,
                submission: {
                    ...current.submission,
                    status: "Published",
                    publishedAt: response.data?.publishedAt ?? null,
                },
            }));

            setSuccess(
                response.data?.message ||
                "Results published successfully."
            );
        } catch (err) {
            // Publishing can save successfully before a notification fails.
            // Read the persisted status before inviting another attempt.
            let persistedDetails = null;

            try {
                const response =
                    await resultsPublishingApi.getSubmission(submission.id);

                persistedDetails = response.data;
                setDetails(persistedDetails);
            } catch {
                // Keep the original publishing error visible.
            }

            if (persistedDetails?.submission?.status === "Published") {
                setItems((current) =>
                    current.filter((item) => item.id !== submission.id)
                );

                setSuccess("Results are published.");
                setError(
                    "The publishing request reported an error after saving. " +
                    "Check the backend log for notification or audit failures."
                );
            } else {
                setError(
                    `${getError(err)} Refresh and check the status before retrying.`
                );
            }
        } finally {
            setPublishing(false);
            setConfirming(false);
        }
    }

    const submission = details?.submission;
    const marks = details?.marks ?? [];
    const busy = loading || loadingDetails || publishing;

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                    <p className="text-sm font-semibold text-blue-600">
                        Exams and Results
                    </p>

                    <h1 className="mt-2 text-2xl font-bold text-slate-950 sm:text-3xl">
                        Results Publishing
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        Release approved results and view published history.
                    </p>
                </div>

                <button
                    type="button"
                    onClick={refresh}
                    disabled={busy}
                    className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}
                >
                    <RefreshCw className="h-4 w-4" />
                    Refresh
                </button>
            </div>

            <div className="flex flex-col gap-2 sm:flex-row">
                {[
                    ["approved", "Awaiting Publication"],
                    ["published", "Published History"],
                ].map(([value, label]) => (
                    <button
                        key={value}
                        type="button"
                        onClick={() => changeTab(value)}
                        disabled={busy}
                        aria-pressed={tab === value}
                        className={`${buttonClass} ${tab === value
                                ? "bg-blue-600 text-white"
                                : "border border-slate-200 bg-white text-slate-700 hover:bg-slate-50"
                            }`}
                    >
                        {label}
                    </button>
                ))}
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
                    {tab === "approved"
                        ? "Approved submissions"
                        : "Published submissions"}
                </h2>

                {loading ? (
                    <p role="status" className="mt-4 text-sm text-slate-500">
                        Loading submissions...
                    </p>
                ) : items.length === 0 ? (
                    <p className="mt-4 text-sm text-slate-500">
                        {error
                            ? "The list could not be loaded."
                            : tab === "approved"
                                ? "No approved submissions are awaiting publication."
                                : "No published submissions are available."}
                    </p>
                ) : (
                    <div className="mt-5 grid gap-4 xl:grid-cols-2">
                        {items.map((item) => (
                            <article
                                key={item.id}
                                className="rounded-xl border border-slate-200 p-4"
                            >
                                <div className="flex flex-wrap items-center justify-between gap-2">
                                    <h3 className="font-bold text-slate-900">
                                        {item.exam.name} — {item.subject.name}
                                    </h3>

                                    <span className="rounded-lg bg-blue-50 px-2 py-1 text-xs font-semibold text-blue-700">
                                        {item.status}
                                    </span>
                                </div>

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
                                    {tab === "approved"
                                        ? `Approved: ${formatDate(item.reviewedAt)}`
                                        : `Published: ${formatDate(item.publishedAt)}`}
                                    {" · Sri Lanka time"}
                                </p>

                                <button
                                    type="button"
                                    onClick={() => viewMarks(item.id)}
                                    disabled={busy}
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
                <p role="status" className="text-sm text-slate-500">
                    Loading student marks...
                </p>
            )}

            {submission && (
                <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                    <div className="border-b border-slate-200 p-5 sm:p-6">
                        <h2 className="text-lg font-bold text-slate-900">
                            {submission.exam.name}
                            {" — "}
                            {submission.subject.name}
                        </h2>

                        <p className="mt-2 text-sm text-slate-500">
                            Grade {submission.grade.name}
                            {" · Class "}
                            {submission.schoolClass.name}
                            {" · "}
                            {submission.academicYear.name}
                        </p>

                        <p className="mt-2 text-sm font-semibold text-blue-700">
                            Status: {submission.status}
                            {" · Students: "}
                            {marks.length}
                        </p>

                        {submission.reviewComment && (
                            <p className="mt-3 whitespace-pre-wrap break-words text-sm text-slate-600">
                                Review comment: {submission.reviewComment}
                            </p>
                        )}
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

                    <div className="border-t border-slate-200 p-5 sm:p-6">
                        {submission.status === "Approved" ? (
                            <div className="space-y-4">
                                <p className="text-sm text-slate-500">
                                    Publishing makes these marks available in
                                    student and linked-parent Results pages.
                                </p>

                                {!confirming ? (
                                    <button
                                        type="button"
                                        onClick={() => setConfirming(true)}
                                        disabled={publishing || marks.length === 0}
                                        className={`${buttonClass} bg-emerald-600 text-white hover:bg-emerald-700`}
                                    >
                                        <Send className="h-4 w-4" />
                                        Publish Results
                                    </button>
                                ) : (
                                    <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
                                        <p className="text-sm text-amber-900">
                                            Publish this submission now?
                                            Check the exam and student marks
                                            before confirming.
                                        </p>

                                        <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                                            <button
                                                type="button"
                                                onClick={publish}
                                                disabled={publishing}
                                                className={`${buttonClass} bg-emerald-600 text-white hover:bg-emerald-700`}
                                            >
                                                {publishing
                                                    ? "Publishing..."
                                                    : "Confirm Publication"}
                                            </button>

                                            <button
                                                type="button"
                                                onClick={() => setConfirming(false)}
                                                disabled={publishing}
                                                className={`${buttonClass} border border-slate-200 bg-white text-slate-700`}
                                            >
                                                Cancel
                                            </button>
                                        </div>
                                    </div>
                                )}
                            </div>
                        ) : (
                            <p className="text-sm text-slate-600">
                                {submission.status === "Published"
                                    ? `Published: ${formatDate(submission.publishedAt)} — Sri Lanka time.`
                                    : `This submission is ${submission.status} and cannot be published.`}
                            </p>
                        )}
                    </div>
                </section>
            )}
        </div>
    );
}