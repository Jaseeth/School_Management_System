import { useEffect, useState } from "react";
import { BookOpen, RefreshCw, Save, Send } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import { teacherMarksApi } from "../../api/teacherMarksApi";

function getErrorMessage(error) {
    const data = error?.response?.data;

    if (typeof data === "string" && data.trim()) {
        return data;
    }

    if (data?.message) {
        return data.message;
    }

    if (data?.errors) {
        const messages = Array.isArray(data.errors)
            ? data.errors
            : Object.values(data.errors).flat();

        if (messages.length) {
            return messages.join(" ");
        }
    }

    if (error?.response?.status === 403) {
        return "Your account does not have permission for this action.";
    }

    if (error?.response?.status === 401) {
        return "Your session has expired. Please sign in again.";
    }

    if (!error?.response) {
        return "Cannot reach the backend. Check that the API is running.";
    }

    return "Unable to complete the request. Please try again.";
}

const buttonClass =
    "inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl " +
    "px-4 py-3 text-sm font-semibold transition " +
    "disabled:cursor-not-allowed disabled:opacity-50";

export default function TeacherMarksPage() {
    const { user } = useAuth();
    const isTeacher = user?.roles?.includes("Teacher") === true;

    const [assignments, setAssignments] = useState([]);
    const [terms, setTerms] = useState([]);
    const [exams, setExams] = useState([]);

    const [assignmentId, setAssignmentId] = useState("");
    const [termId, setTermId] = useState("");
    const [examId, setExamId] = useState("");

    const [entry, setEntry] = useState(null);
    const [values, setValues] = useState({});
    const [dirty, setDirty] = useState(false);

    const [loadingAssignments, setLoadingAssignments] = useState(true);
    const [loadingTerms, setLoadingTerms] = useState(false);
    const [loadingExams, setLoadingExams] = useState(false);
    const [loadingEntry, setLoadingEntry] = useState(false);

    const [saving, setSaving] = useState(false);
    const [confirmSubmit, setConfirmSubmit] = useState(false);
    const [reload, setReload] = useState(0);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    const assignment = assignments.find(
        (item) => String(item.id) === assignmentId
    );

    const students = entry?.students ?? [];
    const locked = students.some(
        (student) =>
            student.mark?.isSubmitted || student.mark?.isPublished
    );

    const completed = students.filter(
        (student) => String(values[student.id] ?? "").trim() !== ""
    ).length;

    const loading =
        loadingAssignments || loadingTerms || loadingExams || loadingEntry;

    useEffect(() => {
        let cancelled = false;

        if (!isTeacher) {
            setLoadingAssignments(false);
            return;
        }

        async function loadAssignments() {
            try {
                setLoadingAssignments(true);

                const response = await teacherMarksApi.getMyAssignments();

                if (!cancelled) {
                    setAssignments(response.data?.assignments ?? []);
                }
            } catch (err) {
                if (!cancelled) {
                    setError(getErrorMessage(err));
                }
            } finally {
                if (!cancelled) {
                    setLoadingAssignments(false);
                }
            }
        }

        loadAssignments();

        return () => {
            cancelled = true;
        };
    }, [isTeacher]);

    useEffect(() => {
        let cancelled = false;

        if (!isTeacher || !assignment) {
            setLoadingTerms(false);
            return;
        }

        async function loadTerms() {
            try {
                setLoadingTerms(true);

                const response = await teacherMarksApi.getTerms(
                    assignment.academicYear.id
                );

                if (!cancelled) {
                    setTerms(response.data ?? []);
                }
            } catch (err) {
                if (!cancelled) {
                    setError(getErrorMessage(err));
                }
            } finally {
                if (!cancelled) {
                    setLoadingTerms(false);
                }
            }
        }

        loadTerms();

        return () => {
            cancelled = true;
        };
    }, [assignment, isTeacher]);

    useEffect(() => {
        let cancelled = false;

        if (!isTeacher || !termId) {
            setLoadingExams(false);
            return;
        }

        async function loadExams() {
            try {
                setLoadingExams(true);

                const response = await teacherMarksApi.getExams(termId);

                if (!cancelled) {
                    setExams(response.data ?? []);
                }
            } catch (err) {
                if (!cancelled) {
                    setError(getErrorMessage(err));
                }
            } finally {
                if (!cancelled) {
                    setLoadingExams(false);
                }
            }
        }

        loadExams();

        return () => {
            cancelled = true;
        };
    }, [termId, isTeacher]);

    useEffect(() => {
        let cancelled = false;

        if (!isTeacher || !assignmentId || !examId) {
            setLoadingEntry(false);
            return;
        }

        async function loadEntry() {
            try {
                setLoadingEntry(true);
                setEntry(null);
                setValues({});

                const response = await teacherMarksApi.getEntry(
                    Number(examId),
                    Number(assignmentId)
                );

                if (cancelled) return;

                const data = response.data;
                const initialValues = {};

                for (const student of data.students ?? []) {
                    initialValues[student.id] =
                        student.mark?.marksObtained == null
                            ? ""
                            : String(student.mark.marksObtained);
                }

                setEntry(data);
                setValues(initialValues);
                setDirty(false);
            } catch (err) {
                if (!cancelled) {
                    setError(getErrorMessage(err));
                }
            } finally {
                if (!cancelled) {
                    setLoadingEntry(false);
                }
            }
        }

        loadEntry();

        return () => {
            cancelled = true;
        };
    }, [assignmentId, examId, reload, isTeacher]);

    function canDiscardChanges() {
        return (
            !dirty ||
            window.confirm("Discard your unsaved marks?")
        );
    }

    function clearMessages() {
        setError("");
        setSuccess("");
        setConfirmSubmit(false);
    }

    function changeAssignment(value) {
        if (!canDiscardChanges()) return;

        clearMessages();
        setAssignmentId(value);
        setTermId("");
        setExamId("");
        setTerms([]);
        setExams([]);
        setEntry(null);
        setValues({});
        setDirty(false);
    }

    function changeTerm(value) {
        if (!canDiscardChanges()) return;

        clearMessages();
        setTermId(value);
        setExamId("");
        setExams([]);
        setEntry(null);
        setValues({});
        setDirty(false);
    }

    function changeExam(value) {
        if (!canDiscardChanges()) return;

        clearMessages();
        setExamId(value);
        setEntry(null);
        setValues({});
        setDirty(false);
    }

    function changeMark(studentId, value) {
        setValues((current) => ({
            ...current,
            [studentId]: value,
        }));

        setDirty(true);
        clearMessages();
    }

    function buildPayload(requireAll) {
        const marks = [];
        const maximumMarks = Number(entry.exam.maximumMarks);

        for (const student of students) {
            const text = String(values[student.id] ?? "").trim();

            if (text === "") {
                if (requireAll) {
                    throw new Error(
                        `Enter marks for ${student.indexNumber} — ${student.fullName}.`
                    );
                }

                if (student.mark?.marksObtained != null) {
                    throw new Error(
                        `Enter a mark for ${student.indexNumber}. ` +
                        "An existing saved mark cannot be deleted by leaving it blank."
                    );
                }

                continue;
            }

            const marksObtained = Number(text);

            if (
                !Number.isFinite(marksObtained) ||
                marksObtained < 0 ||
                marksObtained > maximumMarks
            ) {
                throw new Error(
                    `Marks for ${student.indexNumber} must be between ` +
                    `0 and ${maximumMarks}.`
                );
            }

            marks.push({
                studentId: student.id,
                marksObtained,
            });
        }

        if (!marks.length) {
            throw new Error("Enter at least one student mark.");
        }

        return {
            examId: Number(examId),
            teacherAssignmentId: Number(assignmentId),
            marks,
        };
    }

    async function saveMarks(submitAfterSaving = false) {
        if (!entry || locked || saving || loading) return;

        clearMessages();

        let payload;

        try {
            payload = buildPayload(submitAfterSaving);
        } catch (err) {
            setError(err.message);
            return;
        }

        let draftSaved = false;

        try {
            setSaving(true);

            const draftResponse = await teacherMarksApi.saveDraft(payload);
            draftSaved = true;
            setDirty(false);

            if (submitAfterSaving) {
                const submitResponse = await teacherMarksApi.submit(
                    Number(examId),
                    Number(assignmentId)
                );

                setSuccess(
                    submitResponse.data?.message ||
                    "Marks submitted successfully."
                );
            } else {
                setSuccess(
                    draftResponse.data?.message ||
                    "Marks draft saved successfully."
                );
            }

            setReload((current) => current + 1);
        } catch (err) {
            const message = getErrorMessage(err);

            setError(
                draftSaved && submitAfterSaving
                    ? `Draft saved, but submission failed. ${message}`
                    : message
            );

            if (draftSaved) {
                setReload((current) => current + 1);
            }
        } finally {
            setSaving(false);
        }
    }

    function refreshEntry() {
        if (!canDiscardChanges()) return;

        clearMessages();
        setReload((current) => current + 1);
    }

    if (!isTeacher) {
        return (
            <div className="rounded-2xl border border-slate-200 bg-white p-6">
                <p className="text-sm text-slate-600">
                    This page is available to teachers.
                </p>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            <div>
                <p className="text-sm font-semibold text-blue-600">
                    Teacher Portal
                </p>

                <h1 className="mt-2 text-2xl font-bold text-slate-950 sm:text-3xl">
                    Marks
                </h1>

                <p className="mt-2 text-sm text-slate-500">
                    Save marks as a draft, then submit them for review.
                </p>
            </div>

            {error && (
                <div
                    role="alert"
                    className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700"
                >
                    {error}
                </div>
            )}

            {success && (
                <div
                    role="status"
                    className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700"
                >
                    {success}
                </div>
            )}

            <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                <div className="mb-5 flex items-center gap-3">
                    <div className="rounded-xl bg-blue-50 p-3 text-blue-600">
                        <BookOpen className="h-5 w-5" />
                    </div>

                    <h2 className="font-semibold text-slate-900">
                        Select class and exam
                    </h2>
                </div>

                <div className="grid gap-4 lg:grid-cols-3">
                    <SelectField
                        label="Class / Subject / Academic Year"
                        value={assignmentId}
                        onChange={changeAssignment}
                        disabled={loading || saving}
                    >
                        <option value="">
                            {loadingAssignments
                                ? "Loading assignments..."
                                : "Select assignment"}
                        </option>

                        {assignments.map((item) => (
                            <option key={item.id} value={item.id}>
                                Grade {item.schoolClass.grade.name}
                                {" — "}
                                {item.schoolClass.name}
                                {" — "}
                                {item.subject.name}
                                {" — "}
                                {item.academicYear.name}
                            </option>
                        ))}
                    </SelectField>

                    <SelectField
                        label="Academic Term"
                        value={termId}
                        onChange={changeTerm}
                        disabled={!assignmentId || loading || saving}
                    >
                        <option value="">
                            {loadingTerms
                                ? "Loading terms..."
                                : "Select term"}
                        </option>

                        {terms.map((term) => (
                            <option key={term.id} value={term.id}>
                                {term.name}
                            </option>
                        ))}
                    </SelectField>

                    <SelectField
                        label="Exam"
                        value={examId}
                        onChange={changeExam}
                        disabled={!termId || loading || saving}
                    >
                        <option value="">
                            {loadingExams
                                ? "Loading exams..."
                                : "Select exam"}
                        </option>

                        {exams.map((exam) => (
                            <option key={exam.id} value={exam.id}>
                                {exam.name}
                                {" — Max: "}
                                {exam.maximumMarks}
                            </option>
                        ))}
                    </SelectField>
                </div>

                {!loadingAssignments && assignments.length === 0 && (
                    <p className="mt-4 text-sm text-slate-500">
                        No active class and subject assignments are available.
                    </p>
                )}

                {assignmentId && !loadingTerms && terms.length === 0 && (
                    <p className="mt-4 text-sm text-slate-500">
                        No active terms are available for this academic year.
                    </p>
                )}

                {termId && !loadingExams && exams.length === 0 && (
                    <p className="mt-4 text-sm text-slate-500">
                        No active exams are available for this term.
                    </p>
                )}
            </section>

            {loadingEntry && (
                <div
                    role="status"
                    className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500"
                >
                    Loading student marks...
                </div>
            )}

            {entry && !loadingEntry && (
                <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                    <div className="flex flex-col gap-4 border-b border-slate-200 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
                        <div>
                            <h2 className="text-lg font-bold text-slate-900">
                                {entry.exam.name}
                            </h2>

                            <p className="mt-1 text-sm text-slate-500">
                                {entry.teacherAssignment.subject.name}
                                {" · Grade "}
                                {entry.teacherAssignment.class.grade}
                                {" · Class "}
                                {entry.teacherAssignment.class.name}
                            </p>

                            <p className="mt-2 text-sm text-slate-500">
                                Maximum marks: {entry.exam.maximumMarks}
                                {" · Entered: "}
                                {completed}/{students.length}
                            </p>
                        </div>

                        <button
                            type="button"
                            onClick={refreshEntry}
                            disabled={saving || loading}
                            className={`${buttonClass} border border-slate-200 text-slate-700 hover:bg-slate-50`}
                        >
                            <RefreshCw className="h-4 w-4" />
                            Refresh
                        </button>
                    </div>

                    {locked && (
                        <p
                            role="status"
                            className="m-5 rounded-xl bg-blue-50 p-4 text-sm text-blue-700"
                        >
                            These marks have been submitted or published.
                            Editing is locked.
                        </p>
                    )}

                    {students.length === 0 ? (
                        <p className="p-8 text-center text-sm text-slate-500">
                            No active students are available for this class.
                        </p>
                    ) : (
                        <div className="divide-y divide-slate-100">
                            {students.map((student) => (
                                <div
                                    key={student.id}
                                    className="grid gap-3 p-5 sm:grid-cols-[minmax(0,1fr)_160px_110px] sm:items-center sm:px-6"
                                >
                                    <div className="min-w-0">
                                        <p className="break-words font-semibold text-slate-900">
                                            {student.fullName}
                                        </p>

                                        <p className="mt-1 text-sm text-slate-500">
                                            {student.indexNumber}
                                        </p>
                                    </div>

                                    <label className="block text-xs font-medium text-slate-500">
                                        Marks / {entry.exam.maximumMarks}

                                        <input
                                            type="number"
                                            inputMode="decimal"
                                            min="0"
                                            max={entry.exam.maximumMarks}
                                            step="0.01"
                                            value={values[student.id] ?? ""}
                                            onChange={(event) =>
                                                changeMark(
                                                    student.id,
                                                    event.target.value
                                                )
                                            }
                                            disabled={locked || saving}
                                            aria-label={`Marks for ${student.fullName}`}
                                            placeholder="Enter marks"
                                            className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50 disabled:text-slate-500"
                                        />
                                    </label>

                                    <span className="text-xs font-semibold text-slate-500">
                                        {student.mark?.isPublished
                                            ? "Published"
                                            : student.mark?.isSubmitted
                                                ? "Submitted"
                                                : student.mark
                                                    ? "Draft"
                                                    : "Not saved"}
                                    </span>
                                </div>
                            ))}
                        </div>
                    )}

                    {!locked && students.length > 0 && (
                        <div className="space-y-4 border-t border-slate-200 p-5 sm:p-6">
                            <p className="text-sm text-slate-500">
                                Drafts can contain some students’ marks.
                                Submission requires every student’s mark.
                                Zero is a valid mark.
                            </p>

                            {dirty && (
                                <p className="text-sm font-medium text-amber-700">
                                    You have unsaved changes.
                                </p>
                            )}

                            <div className="flex flex-col gap-3 sm:flex-row">
                                <button
                                    type="button"
                                    onClick={() => saveMarks(false)}
                                    disabled={saving || loading}
                                    className={`${buttonClass} border border-slate-200 text-slate-700 hover:bg-slate-50`}
                                >
                                    <Save className="h-4 w-4" />
                                    {saving ? "Processing..." : "Save Draft"}
                                </button>

                                <button
                                    type="button"
                                    onClick={() => {
                                        try {
                                            buildPayload(true);
                                            setError("");
                                            setConfirmSubmit(true);
                                        } catch (err) {
                                            setError(err.message);
                                        }
                                    }}
                                    disabled={saving || loading}
                                    className={`${buttonClass} bg-blue-600 text-white hover:bg-blue-700`}
                                >
                                    <Send className="h-4 w-4" />
                                    Submit for Review
                                </button>
                            </div>

                            {confirmSubmit && (
                                <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
                                    <p className="text-sm text-amber-900">
                                        Submit these marks for review?
                                        Your current entries will be saved first.
                                        Editing will be locked after submission.
                                    </p>

                                    <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                                        <button
                                            type="button"
                                            onClick={() => saveMarks(true)}
                                            disabled={saving}
                                            className={`${buttonClass} bg-blue-600 text-white hover:bg-blue-700`}
                                        >
                                            Confirm Submission
                                        </button>

                                        <button
                                            type="button"
                                            onClick={() =>
                                                setConfirmSubmit(false)
                                            }
                                            disabled={saving}
                                            className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>
                    )}
                </section>
            )}
        </div>
    );
}

function SelectField({
    label,
    value,
    onChange,
    disabled,
    children,
}) {
    return (
        <label className="block min-w-0 text-sm font-semibold text-slate-700">
            {label}

            <select
                value={value}
                onChange={(event) => onChange(event.target.value)}
                disabled={disabled}
                className="mt-2 w-full min-w-0 cursor-pointer rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm font-normal text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-slate-50"
            >
                {children}
            </select>
        </label>
    );
}