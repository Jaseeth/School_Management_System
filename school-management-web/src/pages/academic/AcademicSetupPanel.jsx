import { useEffect, useRef, useState } from "react";
import { Pencil, Plus, RefreshCw } from "lucide-react";
import { academicApi } from "../../api/academicApi";

const inputClass = "mt-2 block w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-slate-50";
const buttonClass = "inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50";
const categories = {
    years: { label: "Academic years", singular: "Academic year", get: academicApi.getAcademicYears, create: academicApi.createAcademicYear },
    sections: { label: "Sections", singular: "Section", get: academicApi.getSections, create: academicApi.createSection },
    grades: { label: "Grades", singular: "Grade", create: academicApi.createGrade },
    classes: { label: "Classes", singular: "Class", create: academicApi.createClass },
    subjects: { label: "Subjects", singular: "Subject", get: academicApi.getSubjects, create: academicApi.createSubject },
};
const emptyForm = { name: "", startDate: "", endDate: "" };

function errorMessage(error) {
    if (error?.response?.status === 401) return "Your session has expired. Sign in again.";
    if (error?.response?.status === 403) return "Your account does not have permission for this academic setup action.";
    const data = error?.response?.data;
    if (data?.message) return data.message;
    if (data?.errors) return Object.values(data.errors).flat().join(" ");
    if (!error?.response) return "Unable to complete the request. Check that the API is running, then try again.";
    return "Unable to complete the request. Please try again.";
}

function listData(response) {
    if (!Array.isArray(response.data)) throw new Error("Unexpected academic setup response.");
    return response.data;
}

function displayDate(value) {
    return value ? String(value).slice(0, 10) : "—";
}

export default function AcademicSetupPanel({ type, saving, setSaving }) {
    const category = categories[type];
    const scoped = type === "grades" || type === "classes";
    const [rows, setRows] = useState([]);
    const [sections, setSections] = useState([]);
    const [grades, setGrades] = useState([]);
    const [sectionId, setSectionId] = useState("");
    const [gradeId, setGradeId] = useState("");
    const [loading, setLoading] = useState(true);
    const [gradesLoading, setGradesLoading] = useState(false);
    const [rowsLoading, setRowsLoading] = useState(false);
    const [loadError, setLoadError] = useState("");
    const [gradesError, setGradesError] = useState("");
    const [rowsError, setRowsError] = useState("");
    const [formError, setFormError] = useState("");
    const [success, setSuccess] = useState("");
    const [reload, setReload] = useState(0);
    const [form, setForm] = useState(emptyForm);
    const [editingId, setEditingId] = useState(null);
    const [search, setSearch] = useState("");
    const busy = useRef(false);
    const mounted = useRef(false);

    useEffect(() => {
        mounted.current = true;
        return () => { mounted.current = false; };
    }, []);

    useEffect(() => {
        let active = true;
        async function load() {
            setLoading(true);
            setLoadError("");
            try {
                const data = listData(await (scoped ? academicApi.getSections() : category.get()));
                if (!active) return;
                if (scoped) setSections(data);
                else setRows(data);
            } catch (error) {
                if (active) setLoadError(errorMessage(error));
            } finally {
                if (active) setLoading(false);
            }
        }
        load();
        return () => { active = false; };
    }, [category, scoped, reload]);

    useEffect(() => {
        if (type !== "classes" || !sectionId) return;
        let active = true;
        async function loadGrades() {
            setGradesLoading(true);
            setGradesError("");
            try {
                const data = listData(await academicApi.getGrades(sectionId));
                if (active) setGrades(data);
            } catch (error) {
                if (active) setGradesError(errorMessage(error));
            } finally {
                if (active) setGradesLoading(false);
            }
        }
        loadGrades();
        return () => { active = false; };
    }, [type, sectionId, reload]);

    useEffect(() => {
        if (!scoped || !sectionId || (type === "classes" && !gradeId)) return;
        let active = true;
        async function loadRows() {
            setRowsLoading(true);
            setRowsError("");
            try {
                const response = type === "grades"
                    ? await academicApi.getGrades(sectionId)
                    : await academicApi.getClasses(gradeId);
                const data = listData(response);
                if (active) setRows(data);
            } catch (error) {
                if (active) setRowsError(errorMessage(error));
            } finally {
                if (active) setRowsLoading(false);
            }
        }
        loadRows();
        return () => { active = false; };
    }, [type, scoped, sectionId, gradeId, reload]);

    const selectedSection = sections.find((section) => String(section.id) === sectionId);
    const selectedGrade = grades.find((grade) => String(grade.id) === gradeId);
    const hasScope = !scoped || (selectedSection && (type === "grades" || selectedGrade));
    const pending = loading || gradesLoading || rowsLoading;
    const dataError = loadError || gradesError || rowsError;
    const disabled = saving || pending || !!dataError || !hasScope;
    const filtered = rows.filter((row) => row.name.toLowerCase().includes(search.trim().toLowerCase()));

    function changeSection(event) {
        setSectionId(event.target.value);
        setGradeId("");
        setGrades([]);
        setRows([]);
        setGradesLoading(type === "classes" && !!event.target.value);
        setRowsLoading(type === "grades" && !!event.target.value);
        setGradesError("");
        setRowsError("");
        setForm(emptyForm);
        setFormError("");
        setSuccess("");
        setSearch("");
    }

    function changeGrade(event) {
        setGradeId(event.target.value);
        setRows([]);
        setRowsLoading(!!event.target.value);
        setRowsError("");
        setForm(emptyForm);
        setFormError("");
        setSuccess("");
        setSearch("");
    }

    function editYear(year) {
        setEditingId(year.id);
        setForm({ name: year.name, startDate: displayDate(year.startDate), endDate: displayDate(year.endDate) });
        setFormError("");
        setSuccess("");
    }

    async function save(event) {
        event.preventDefault();
        if (busy.current || disabled) return;
        const name = form.name.trim();
        setFormError("");
        setSuccess("");
        if (!name || name.length > 100) {
            setFormError("Enter a name of 100 characters or fewer.");
            return;
        }
        if (rows.some((row) => row.id !== editingId && row.name.trim().toLowerCase() === name.toLowerCase())) {
            setFormError(`${category.singular} already exists${scoped ? " in this selection" : ""}.`);
            return;
        }
        if (type === "years" && (!form.startDate || !form.endDate || form.endDate <= form.startDate)) {
            setFormError("Choose an end date after the start date.");
            return;
        }
        const payload = { name };
        if (type === "years") Object.assign(payload, { startDate: form.startDate, endDate: form.endDate });
        if (type === "grades") payload.sectionId = Number(sectionId);
        if (type === "classes") payload.gradeId = Number(gradeId);
        busy.current = true;
        setSaving(true);
        try {
            if (editingId !== null) await academicApi.updateAcademicYear(editingId, payload);
            else await category.create(payload);
            if (!mounted.current) return;
            setSuccess(`${category.singular} “${name}” ${editingId !== null ? "updated" : "created"} successfully.`);
            setEditingId(null);
            setForm(emptyForm);
            setSearch("");
            setReload((current) => current + 1);
        } catch (error) {
            if (mounted.current) setFormError(errorMessage(error));
        } finally {
            busy.current = false;
            if (mounted.current) setSaving(false);
        }
    }

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 className="text-lg font-semibold text-slate-950">{category.label}</h2>
                <button type="button" disabled={saving || pending} onClick={() => setReload((current) => current + 1)} className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}>
                    <RefreshCw className="h-4 w-4" /> Refresh
                </button>
            </div>

            {dataError && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{dataError}</p>}
            {success && <p role="status" className="rounded-xl border border-green-200 bg-green-50 p-4 text-sm text-green-800">{success}</p>}

            {scoped && (
                <section aria-label="Select academic structure" className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
                    <h3 className="font-semibold text-slate-950">{type === "grades" ? "Choose a section" : "Choose a section and grade"}</h3>
                    <div className="mt-4 grid gap-5 sm:grid-cols-2">
                        <label className="text-sm font-medium text-slate-700">Section
                            <select value={sectionId} onChange={changeSection} disabled={saving || loading || !!loadError} className={`${inputClass} cursor-pointer`}>
                                <option value="">Select section</option>
                                {sections.map((section) => <option key={section.id} value={section.id}>{section.name}</option>)}
                            </select>
                        </label>
                        {type === "classes" && <label className="text-sm font-medium text-slate-700">Grade
                            <select value={gradeId} onChange={changeGrade} disabled={saving || loading || gradesLoading || !selectedSection || !!loadError || !!gradesError} className={`${inputClass} cursor-pointer`}>
                                <option value="">Select grade</option>
                                {grades.map((grade) => <option key={grade.id} value={grade.id}>{grade.name}</option>)}
                            </select>
                        </label>}
                    </div>
                    {!loading && !loadError && !sections.length && <p className="mt-4 text-sm text-slate-500">Create a section in Sections first.</p>}
                    {type === "classes" && selectedSection && !gradesLoading && !gradesError && !grades.length && <p className="mt-4 text-sm text-slate-500">Create a grade in this section using Grades first.</p>}
                </section>
            )}

            <form onSubmit={save} className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
                <h3 className="font-semibold text-slate-950">{editingId !== null ? "Edit academic year" : `Add ${category.singular.toLowerCase()}`}</h3>
                {scoped && hasScope && <p className="mt-2 text-sm text-slate-500">{selectedSection.name}{selectedGrade ? ` / ${selectedGrade.name}` : ""}</p>}
                <fieldset disabled={disabled} className="mt-4 grid min-w-0 gap-5 lg:grid-cols-3">
                    <label className="text-sm font-medium text-slate-700">Name
                        <span className="required-marker" aria-hidden="true"> *</span><input required maxLength={100} value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} className={inputClass}
                            placeholder={type === "years" ? "2026/2027" : type === "grades" ? "Grade 11" : type === "classes" ? "A" : type === "subjects" ? "Mathematics" : "Secondary"} />
                    </label>
                    {type === "years" && <>
                        <label className="text-sm font-medium text-slate-700">Start date
                            <span className="required-marker" aria-hidden="true"> *</span><input required type="date" value={form.startDate} onChange={(event) => setForm((current) => ({ ...current, startDate: event.target.value }))} className={inputClass} />
                        </label>
                        <label className="text-sm font-medium text-slate-700">End date
                            <span className="required-marker" aria-hidden="true"> *</span><input required type="date" min={form.startDate || undefined} value={form.endDate} onChange={(event) => setForm((current) => ({ ...current, endDate: event.target.value }))} className={inputClass} />
                        </label>
                    </>}
                </fieldset>
                {formError && <p role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">{formError}</p>}
                <div className="mt-5 flex flex-wrap gap-3">
                    <button type="submit" disabled={disabled} className={`${buttonClass} w-full bg-blue-600 text-white hover:bg-blue-700 sm:w-auto`}>
                        <Plus className="h-4 w-4" />{saving ? "Saving…" : editingId !== null ? "Save changes" : `Create ${category.singular.toLowerCase()}`}
                    </button>
                    {editingId !== null && <button type="button" disabled={saving} onClick={() => { setEditingId(null); setForm(emptyForm); setFormError(""); }} className={`${buttonClass} border border-slate-200 text-slate-700 hover:bg-slate-50`}>Cancel edit</button>}
                </div>
            </form>

            <section aria-label={`Existing ${category.label.toLowerCase()}`} className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <h3 className="font-semibold text-slate-950">Existing {category.label.toLowerCase()}</h3>
                    <label className="text-sm font-medium text-slate-700">Search
                        <input type="search" value={search} disabled={!hasScope || pending || !!dataError} onChange={(event) => setSearch(event.target.value)} className={inputClass} placeholder="Search by name" />
                    </label>
                </div>
                {pending ? <p role="status" className="py-8 text-sm text-slate-500">Loading academic setup…</p>
                    : dataError ? <p className="py-8 text-sm text-slate-500">Refresh to load the current records.</p>
                        : !hasScope ? <p className="py-8 text-sm text-slate-500">{type === "grades" ? "Select a section to view and create grades." : "Select a section and grade to view and create classes."}</p>
                            : !filtered.length ? <p className="py-8 text-sm text-slate-500">{rows.length ? "No records match your search." : "No records yet. Use the form above to create one."}</p>
                                : <>
                                    <p className="mt-4 text-sm text-slate-500">{filtered.length} of {rows.length} records</p>
                                    <div className="mt-4 grid gap-4 lg:grid-cols-2">
                                        {filtered.map((row) => (
                                            <article key={row.id} className="min-w-0 rounded-xl border border-slate-200 p-4">
                                                <div className="flex flex-wrap items-start justify-between gap-3">
                                                    <h4 className="break-words font-semibold text-slate-950">{row.name}</h4>
                                                    <span className="rounded-full bg-green-50 px-3 py-1 text-xs font-semibold text-green-700">Active</span>
                                                </div>
                                                {scoped && <p className="mt-2 break-words text-sm text-slate-500">{selectedSection?.name}{selectedGrade ? ` / ${selectedGrade.name}` : ""}</p>}
                                                {type === "years" && <>
                                                    <dl className="mt-4 grid grid-cols-2 gap-4 text-sm">
                                                        <div><dt className="text-slate-500">Start date</dt><dd className="mt-1 text-slate-800">{displayDate(row.startDate)}</dd></div>
                                                        <div><dt className="text-slate-500">End date</dt><dd className="mt-1 text-slate-800">{displayDate(row.endDate)}</dd></div>
                                                    </dl>
                                                    <button type="button" disabled={saving} onClick={() => editYear(row)} className={`${buttonClass} mt-4 text-blue-600 hover:bg-blue-50`}><Pencil className="h-4 w-4" />Edit year</button>
                                                </>}
                                            </article>
                                        ))}
                                    </div>
                                </>}
            </section>
        </div>
    );
}
