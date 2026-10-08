import { useEffect, useRef, useState } from "react";
import { useAuth } from "../../context/AuthContext";
import { academicApi } from "../../api/academicApi";
import { examsManagementApi } from "../../api/examsManagementApi";

const buttonClass =
    "cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50";

const primaryClass =
    "cursor-pointer rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50";

const inputClass =
    "mt-2 block w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50";

export default function TermsExamsManagementPage() {
    const { user } = useAuth();

    const allowed = ["Admin", "Principal", "Deputy Principal"].some(
        (role) => user?.roles?.includes(role)
    );

    const [years, setYears] = useState([]);
    const [yearId, setYearId] = useState("");
    const [yearsLoading, setYearsLoading] = useState(true);
    const [yearsError, setYearsError] = useState("");

    const [terms, setTerms] = useState([]);
    const [termId, setTermId] = useState("");
    const [termsLoading, setTermsLoading] = useState(false);
    const [termsError, setTermsError] = useState("");
    const [termsRefresh, setTermsRefresh] = useState(0);

    const [exams, setExams] = useState([]);
    const [examsLoading, setExamsLoading] = useState(false);
    const [examsError, setExamsError] = useState("");
    const [examsRefresh, setExamsRefresh] = useState(0);

    const [mode, setMode] = useState(null);
    const [saving, setSaving] = useState(false);
    const busyRef = useRef(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    const [termForm, setTermForm] = useState({
        name: "",
        startDate: "",
        endDate: "",
    });

    const [examForm, setExamForm] = useState({
        name: "",
        examDate: "",
        maximumMarks: "100",
    });

    const selectedYear = years.find(
        (year) => String(year.id) === yearId
    );

    const selectedTerm = terms.find(
        (term) => String(term.id) === termId
    );

    useEffect(() => {
        if (!allowed) return;

        let active = true;

        async function loadYears() {
            setYearsLoading(true);
            setYearsError("");

            try {
                const response = await academicApi.getAcademicYears();

                if (!active) return;

                if (!Array.isArray(response.data)) {
                    throw new Error("Unexpected academic year response.");
                }

                setYears(response.data);
            } catch (err) {
                if (active) setYearsError(getErrorMessage(err));
            } finally {
                if (active) setYearsLoading(false);
            }
        }

        loadYears();

        return () => {
            active = false;
        };
    }, [allowed]);

    useEffect(() => {
        if (!allowed || !yearId) return;

        let active = true;

        async function loadTerms() {
            setTermsLoading(true);
            setTermsError("");

            try {
                const response = await examsManagementApi.getTerms(yearId);

                if (!active) return;

                if (!Array.isArray(response.data)) {
                    throw new Error("Unexpected terms response.");
                }

                setTerms(response.data);
            } catch (err) {
                if (active) {
                    setTerms([]);
                    setTermsError(getErrorMessage(err));
                }
            } finally {
                if (active) setTermsLoading(false);
            }
        }

        loadTerms();

        return () => {
            active = false;
        };
    }, [allowed, yearId, termsRefresh]);

    useEffect(() => {
        if (!allowed || !termId) return;

        let active = true;

        async function loadExams() {
            setExamsLoading(true);
            setExamsError("");

            try {
                const response = await examsManagementApi.getExams(termId);

                if (!active) return;

                if (!Array.isArray(response.data)) {
                    throw new Error("Unexpected exams response.");
                }

                setExams(response.data);
            } catch (err) {
                if (active) {
                    setExams([]);
                    setExamsError(getErrorMessage(err));
                }
            } finally {
                if (active) setExamsLoading(false);
            }
        }

        loadExams();

        return () => {
            active = false;
        };
    }, [allowed, termId, examsRefresh]);

    function changeYear(value) {
        setYearId(value);
        setTermId("");
        setTerms([]);
        setExams([]);
        setTermsLoading(Boolean(value));
        setExamsLoading(false);
        setTermsError("");
        setExamsError("");
        setMode(null);
        setError("");
        setSuccess("");
    }

    function changeTerm(value) {
        setTermId(value);
        setExams([]);
        setExamsLoading(Boolean(value));
        setExamsError("");
        setMode(null);
        setError("");
        setSuccess("");
    }

    function openTermForm() {
        setTermForm({ name: "", startDate: "", endDate: "" });
        setMode("term");
        setError("");
        setSuccess("");
    }

    function openExamForm() {
        setExamForm({
            name: "",
            examDate: "",
            maximumMarks: "100",
        });
        setMode("exam");
        setError("");
        setSuccess("");
    }

    async function createTerm(event) {
        event.preventDefault();
        if (busyRef.current) return;

        setError("");
        setSuccess("");

        if (!yearId || !termForm.name.trim()) {
            setError("Select an academic year and enter the term name.");
            return;
        }

        if (
            !termForm.startDate ||
            !termForm.endDate ||
            termForm.endDate <= termForm.startDate
        ) {
            setError("Term end date must be after its start date.");
            return;
        }

        busyRef.current = true;
        setSaving(true);

        try {
            const response = await examsManagementApi.createTerm({
                academicYearId: Number(yearId),
                name: termForm.name.trim(),
                startDate: termForm.startDate,
                endDate: termForm.endDate,
            });

            setMode(null);
            setSuccess(
                response.data?.message || "Term created successfully."
            );
            setTermsRefresh((value) => value + 1);
        } catch (err) {
            setError(getErrorMessage(err));
        } finally {
            busyRef.current = false;
            setSaving(false);
        }
    }

    async function createExam(event) {
        event.preventDefault();
        if (busyRef.current) return;

        setError("");
        setSuccess("");

        const maximumMarks = Number(examForm.maximumMarks);

        if (!selectedTerm || !examForm.name.trim() || !examForm.examDate) {
            setError("Select a term and enter the exam name and date.");
            return;
        }

        if (
            !Number.isFinite(maximumMarks) ||
            maximumMarks <= 0
        ) {
            setError("Maximum marks must be greater than zero.");
            return;
        }

        const start = dateInputValue(selectedTerm.startDate);
        const end = dateInputValue(selectedTerm.endDate);

        if (
            (start && examForm.examDate < start) ||
            (end && examForm.examDate > end)
        ) {
            setError("Exam date must be within the selected term dates.");
            return;
        }

        busyRef.current = true;
        setSaving(true);

        try {
            const response = await examsManagementApi.createExam({
                academicTermId: Number(termId),
                name: examForm.name.trim(),
                examDate: examForm.examDate,
                maximumMarks,
            });

            setMode(null);
            setSuccess(
                response.data?.message || "Exam created successfully."
            );
            setExamsRefresh((value) => value + 1);
        } catch (err) {
            setError(getErrorMessage(err));
        } finally {
            busyRef.current = false;
            setSaving(false);
        }
    }

    if (!allowed) {
        return (
            <p role="alert" className="p-6 text-sm text-red-700">
                You do not have access to Terms & Exams Management.
            </p>
        );
    }

    return (
        <main className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
            <h1 className="text-2xl font-bold text-slate-950">
                Terms & Exams
            </h1>
            <p className="mt-2 text-sm text-slate-500">
                Manage academic terms and examinations.
            </p>

            {success && (
                <p
                    role="status"
                    className="mt-5 rounded-xl bg-emerald-50 p-4 text-sm text-emerald-700"
                >
                    {success}
                </p>
            )}

            {yearsError && <ErrorMessage message={yearsError} />}

            <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <div className="grid gap-4 sm:grid-cols-2">
                    <label className="text-sm font-semibold text-slate-700">
                        Academic Year
                        <select
                            value={yearId}
                            disabled={yearsLoading || saving}
                            onChange={(event) => changeYear(event.target.value)}
                            className={`${inputClass} cursor-pointer disabled:cursor-not-allowed`}
                        >
                            <option value="">
                                {yearsLoading
                                    ? "Loading academic years..."
                                    : "Select academic year"}
                            </option>
                            {years.map((year) => (
                                <option key={year.id} value={year.id}>
                                    {year.name}
                                </option>
                            ))}
                        </select>
                    </label>

                    <label className="text-sm font-semibold text-slate-700">
                        Academic Term
                        <select
                            value={termId}
                            disabled={!yearId || termsLoading || saving}
                            onChange={(event) => changeTerm(event.target.value)}
                            className={`${inputClass} cursor-pointer disabled:cursor-not-allowed`}
                        >
                            <option value="">
                                {termsLoading
                                    ? "Loading terms..."
                                    : "Select academic term"}
                            </option>
                            {terms.map((term) => (
                                <option key={term.id} value={term.id}>
                                    {term.name}
                                </option>
                            ))}
                        </select>
                    </label>
                </div>

                <div className="mt-5 flex flex-wrap gap-3">
                    <button
                        type="button"
                        disabled={!yearId || saving}
                        onClick={openTermForm}
                        className={primaryClass}
                    >
                        Create Term
                    </button>

                    <button
                        type="button"
                        disabled={!selectedTerm || termsLoading || saving}
                        onClick={openExamForm}
                        className={primaryClass}
                    >
                        Create Exam
                    </button>

                    <button
                        type="button"
                        disabled={
                            !yearId || termsLoading || examsLoading || saving
                        }
                        onClick={() => {
                            setTermsRefresh((value) => value + 1);
                            if (termId) {
                                setExamsRefresh((value) => value + 1);
                            }
                        }}
                        className={buttonClass}
                    >
                        Refresh
                    </button>
                </div>
            </section>

            {mode && (
                <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                    <h2 className="text-lg font-bold text-slate-950">
                        {mode === "term" ? "Create Term" : "Create Exam"}
                    </h2>
                    <p className="mt-1 text-sm text-slate-500">
                        {selectedYear?.name}
                        {mode === "exam" && ` · ${selectedTerm?.name}`}
                    </p>

                    <form
                        onSubmit={mode === "term" ? createTerm : createExam}
                        className="mt-5"
                    >
                        <fieldset disabled={saving}>
                            {mode === "term" ? (
                                <div className="grid gap-4 sm:grid-cols-2">
                                    <Field
                                        label="Term Name"
                                        required
                                        value={termForm.name}
                                        onChange={(event) =>
                                            setTermForm((current) => ({
                                                ...current,
                                                name: event.target.value,
                                            }))
                                        }
                                    />
                                    <Field
                                        label="Start Date"
                                        type="date"
                                        required
                                        value={termForm.startDate}
                                        onChange={(event) =>
                                            setTermForm((current) => ({
                                                ...current,
                                                startDate: event.target.value,
                                            }))
                                        }
                                    />
                                    <Field
                                        label="End Date"
                                        type="date"
                                        required
                                        value={termForm.endDate}
                                        onChange={(event) =>
                                            setTermForm((current) => ({
                                                ...current,
                                                endDate: event.target.value,
                                            }))
                                        }
                                    />
                                </div>
                            ) : (
                                <div className="grid gap-4 sm:grid-cols-2">
                                    <Field
                                        label="Exam Name"
                                        required
                                        value={examForm.name}
                                        onChange={(event) =>
                                            setExamForm((current) => ({
                                                ...current,
                                                name: event.target.value,
                                            }))
                                        }
                                    />
                                    <Field
                                        label="Exam Date"
                                        type="date"
                                        required
                                        min={dateInputValue(
                                            selectedTerm?.startDate
                                        )}
                                        max={dateInputValue(
                                            selectedTerm?.endDate
                                        )}
                                        value={examForm.examDate}
                                        onChange={(event) =>
                                            setExamForm((current) => ({
                                                ...current,
                                                examDate: event.target.value,
                                            }))
                                        }
                                    />
                                    <Field
                                        label="Maximum Marks"
                                        type="number"
                                        step="0.01"
                                        min="0.01"
                                        required
                                        value={examForm.maximumMarks}
                                        onChange={(event) =>
                                            setExamForm((current) => ({
                                                ...current,
                                                maximumMarks:
                                                    event.target.value,
                                            }))
                                        }
                                    />
                                </div>
                            )}
                        </fieldset>

                        <ErrorMessage message={error} />

                        <div className="mt-5 flex flex-wrap gap-3">
                            <button
                                type="submit"
                                disabled={saving}
                                className={primaryClass}
                            >
                                {saving
                                    ? "Creating..."
                                    : mode === "term"
                                        ? "Create Term"
                                        : "Create Exam"}
                            </button>
                            <button
                                type="button"
                                disabled={saving}
                                onClick={() => {
                                    setMode(null);
                                    setError("");
                                }}
                                className={buttonClass}
                            >
                                Cancel
                            </button>
                        </div>
                    </form>
                </section>
            )}

            {yearId && (
                <section className="mt-6">
                    <h2 className="text-lg font-bold text-slate-950">
                        Academic Terms
                    </h2>

                    <ErrorMessage message={termsError} />

                    {termsLoading ? (
                        <p role="status" className="mt-4 text-sm text-slate-500">
                            Loading terms...
                        </p>
                    ) : !termsError && (
                        terms.length === 0 ? (
                            <EmptyState text="No active terms for this academic year." />
                        ) : (
                            <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                                {terms.map((term) => (
                                    <button
                                        key={term.id}
                                        type="button"
                                        disabled={saving}
                                        onClick={() =>
                                            changeTerm(String(term.id))
                                        }
                                        className={`min-w-0 cursor-pointer rounded-2xl border p-5 text-left shadow-sm disabled:cursor-not-allowed disabled:opacity-50 ${String(term.id) === termId
                                                ? "border-blue-500 bg-blue-50"
                                                : "border-slate-200 bg-white hover:border-blue-300"
                                            }`}
                                    >
                                        <span className="block break-words font-bold text-slate-950">
                                            {term.name}
                                        </span>
                                        <span className="mt-2 block text-sm text-slate-500">
                                            {formatDate(term.startDate)}
                                            {" – "}
                                            {formatDate(term.endDate)}
                                        </span>
                                    </button>
                                ))}
                            </div>
                        )
                    )}
                </section>
            )}

            {termId && (
                <section className="mt-6">
                    <h2 className="text-lg font-bold text-slate-950">
                        Exams · {selectedTerm?.name}
                    </h2>

                    <ErrorMessage message={examsError} />

                    {examsLoading ? (
                        <p role="status" className="mt-4 text-sm text-slate-500">
                            Loading exams...
                        </p>
                    ) : !examsError && (
                        exams.length === 0 ? (
                            <EmptyState text="No active exams for this term." />
                        ) : (
                            <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                                {exams.map((exam) => (
                                    <article
                                        key={exam.id}
                                        className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
                                    >
                                        <h3 className="break-words font-bold text-slate-950">
                                            {exam.name}
                                        </h3>
                                        <p className="mt-2 text-sm text-slate-500">
                                            {formatDate(exam.examDate)}
                                        </p>
                                        <p className="mt-3 text-sm text-slate-700">
                                            Maximum marks: {exam.maximumMarks}
                                        </p>
                                    </article>
                                ))}
                            </div>
                        )
                    )}
                </section>
            )}
        </main>
    );
}

function Field({ label, ...props }) {
    return (
        <label className="block text-sm font-semibold text-slate-700">
            {label}
            <input {...props} className={inputClass} />
        </label>
    );
}

function ErrorMessage({ message }) {
    if (!message) return null;

    return (
        <p
            role="alert"
            className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700"
        >
            {message}
        </p>
    );
}

function EmptyState({ text }) {
    return (
        <p className="mt-4 rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-500">
            {text}
        </p>
    );
}

function dateInputValue(value) {
    return value ? String(value).slice(0, 10) : "";
}

function formatDate(value) {
    const date = dateInputValue(value);
    if (!date) return "—";

    const [year, month, day] = date.split("-");
    return `${day}/${month}/${year}`;
}

function getErrorMessage(error) {
    const body = error?.response?.data;

    const errors = Array.isArray(body?.errors)
        ? body.errors
        : Object.values(body?.errors ?? {}).flat();

    if (errors.length) return errors.join(" ");
    if (body?.message) return body.message;

    if (error?.response?.status === 403) {
        return "Your account needs the Exams.View or Exams.Manage permission for this action.";
    }

    if (!error?.response) {
        return "Cannot reach the backend. Check that the API is running.";
    }

    return "The request failed. Refresh the list before retrying.";
}