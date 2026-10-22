import MondayAssembly from "../../components/MondayAssembly";
import { useEffect, useRef, useState } from "react";
import { CalendarDays, Pencil, RefreshCw } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import { academicApi } from "../../api/academicApi";
import { examsManagementApi } from "../../api/examsManagementApi";
import { teacherAssignmentsManagementApi } from "../../api/teacherAssignmentsManagementApi";
import { timetableManagementApi } from "../../api/timetableManagementApi";

const days = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
const inputClass = "mt-2 block w-full rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-slate-50";
const buttonClass = "inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50";
const emptyForm = { kind: "teaching", assignmentId: "", gradeId: "", schoolClassId: "", day: "1", startTime: "", endTime: "", room: "" };
function errorMessage(error) {
    if (error?.response?.status === 401) return "Your session has expired. Sign in again.";
    if (error?.response?.status === 403) return "Your account does not have permission for this timetable action.";
    const data = error?.response?.data;
    return data?.message || (data?.errors ? Object.values(data.errors).flat().join(" ") : null) || "Unable to complete the request. Check that the API is running and refresh.";
}
function asList(response) {
    if (!Array.isArray(response.data)) throw new Error("Unexpected timetable lookup response.");
    return response.data;
}
function assignmentLabel(item) {
    return `${item.teacher.staffNumber} - ${item.teacher.fullName} / ${item.subject.name}`;
}

export default function TimetableManagementPage() {
    const { user } = useAuth();
    const allowed = ["Admin", "Principal", "Deputy Principal"].some((role) => user?.roles?.includes(role));
    const [assembly, setAssembly] = useState(null);
    const [years, setYears] = useState([]);
    const [terms, setTerms] = useState([]);
    const [assignments, setAssignments] = useState([]);
    const [grades, setGrades] = useState([]);
    const [classes, setClasses] = useState([]);
    const [entries, setEntries] = useState([]);
    const [yearId, setYearId] = useState("");
    const [termId, setTermId] = useState("");
    const [form, setForm] = useState(emptyForm);
    const [editingId, setEditingId] = useState(null);
    const [deactivating, setDeactivating] = useState(null);
    const [loading, setLoading] = useState(true);
    const [termsLoading, setTermsLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [loadError, setLoadError] = useState("");
    const [termsError, setTermsError] = useState("");
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [reload, setReload] = useState(0);
    const [search, setSearch] = useState("");
    const [dayFilter, setDayFilter] = useState("");
    const busy = useRef(false);
    const mounted = useRef(false);
    useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);

    useEffect(() => {
        if (!allowed) return;
        let active = true;
        async function load() {
            setLoading(true); setLoadError("");
            try {
                const responses = await Promise.all([academicApi.getAcademicYears(), teacherAssignmentsManagementApi.getSubjectAssignments(), timetableManagementApi.getEntries(), academicApi.getSections(), timetableManagementApi.getIntervals()]);
                const rows = responses[2].data?.timetable;
                if (!Array.isArray(rows)) throw new Error("Unexpected timetable response.");
                if (!active) return;
                const sections = asList(responses[3]);
                const gradeResponses = await Promise.all(sections.map((section) => academicApi.getGrades(section.id)));
                const allGrades = gradeResponses.flatMap((response, index) => asList(response).map((grade) => ({ ...grade, sectionName: sections[index].name })));
                const classResponses = await Promise.all(allGrades.map((grade) => academicApi.getClasses(grade.id)));
                if (!active) return;
                setGrades(allGrades);
                setClasses(classResponses.flatMap((response, index) => asList(response).map((item) => ({ ...item, gradeId: allGrades[index].id }))));
                setYears(asList(responses[0]));
                setAssignments(asList(responses[1]).filter((item) => item.isActive));
                setEntries([...rows.map((item) => ({ ...item, kind: "teaching" })), ...asList(responses[4]).map((item) => ({ ...item, kind: "interval", subject: { name: "Interval" }, teacher: { fullName: "", staffNumber: "" } }))]);
            } catch (err) { if (active) setLoadError(errorMessage(err)); }
            finally { if (active) setLoading(false); }
        }
        load();
        return () => { active = false; };
    }, [allowed, reload]);

    useEffect(() => {
        if (!allowed || !yearId) return;
        let active = true;
        async function loadTerms() {
            setTermsLoading(true); setTermsError("");
            try {
                const data = asList(await examsManagementApi.getTerms(yearId));
                if (active) setTerms(data);
            } catch (err) { if (active) { setTerms([]); setTermsError(errorMessage(err)); } }
            finally { if (active) setTermsLoading(false); }
        }
        loadTerms();
        return () => { active = false; };
    }, [allowed, yearId, reload]);

    const assignment = assignments.find((item) => String(item.id) === form.assignmentId && String(item.academicYear.id) === yearId);
    const availableAssignments = [...new Map(assignments.filter((item) => String(item.academicYear.id) === yearId)
        .map((item) => [`${item.teacher.id}:${item.subject.id}`, item])).values()];
    const selectedClass = classes.find((item) => String(item.id) === form.schoolClassId && String(item.gradeId) === form.gradeId);
    const classPeriods = entries.filter((item) => String(item.academicYear.id) === yearId && String(item.academicTerm.id) === termId && item.kind !== "interval" && String(item.schoolClass.id) === form.schoolClassId);
    const atMaximum = form.kind !== "interval" && classPeriods.filter((item) => String(item.dayValue) === form.day && (item.kind !== form.kind || item.id !== editingId) && item.kind !== "interval").length >= (form.day === "5" ? 5 : form.day === "1" ? 8 : 9);
    const validYear = years.some((item) => String(item.id) === yearId);
    const validTerm = terms.some((item) => String(item.id) === termId);
    const pending = loading || termsLoading;
    const dataError = loadError || termsError;
    const query = search.trim().toLowerCase();
    const visibleEntries = entries.filter((item) => (!yearId || String(item.academicYear.id) === yearId) &&
        (!termId || String(item.academicTerm.id) === termId) && (item.kind === "interval" || !form.schoolClassId || String(item.schoolClass.id) === form.schoolClassId) && (!dayFilter || String(item.dayValue) === dayFilter) &&
        [item.teacher.fullName, item.teacher.staffNumber, item.subject.name, item.schoolClass?.name,
            item.schoolClass?.grade.name, item.schoolClass?.section.name, item.room].some((value) => value?.toLowerCase().includes(query)));

    function resetForm() { setForm(emptyForm); setEditingId(null); setDeactivating(null); setError(""); }
    function changeYear(value) {
        setYearId(value); setTermId(""); setTerms([]); setTermsError(""); setTermsLoading(!!value);
        resetForm(); setSuccess("");
    }
    function changeTerm(value) { setTermId(value); resetForm(); setSuccess(""); }
    function changeForm(event) { setForm((current) => ({ ...current, [event.target.name]: event.target.value, ...(event.target.name === "gradeId" ? { schoolClassId: "" } : {}), ...(event.target.name === "kind" && event.target.value === "interval" ? { gradeId: "", schoolClassId: "", assignmentId: "" } : {}) })); setError(""); setSuccess(""); }
    function edit(entry) {
        const match = assignments.find((item) => item.academicYear.id === entry.academicYear.id &&
            item.subject.id === entry.subject.id && item.teacher.id === entry.teacher.id);
        setYearId(String(entry.academicYear.id)); setTermId(String(entry.academicTerm.id));
        setForm({ kind: entry.kind, assignmentId: match ? String(match.id) : "", gradeId: entry.schoolClass ? String(entry.schoolClass.grade.id) : "", schoolClassId: entry.schoolClass ? String(entry.schoolClass.id) : "", day: String(entry.dayValue),
            startTime: entry.startTime.slice(0, 5), endTime: entry.endTime.slice(0, 5), room: entry.room || "" });
        setEditingId(entry.id); setDeactivating(null); setSuccess("");
        setError(match || entry.kind === "interval" ? "" : "The original teacher assignment is inactive. Select an active assignment before saving this period.");
        window.scrollTo({ top: 0, behavior: "smooth" });
    }
    async function mutate(operation, message) {
        if (busy.current || pending || dataError) return;
        busy.current = true; setSaving(true); setError(""); setSuccess("");
        try {
            await operation();
            if (!mounted.current) return;
            setEditingId(null); setDeactivating(null); setForm((current) => ({ ...current, startTime: "", endTime: "" })); setSuccess(message); setReload((current) => current + 1);
        } catch (err) { if (mounted.current) setError(errorMessage(err)); }
        finally { busy.current = false; if (mounted.current) setSaving(false); }
    }
    function save(event) {
        event.preventDefault();
        if (!validYear || !validTerm || (form.kind !== "interval" && !assignment) || (form.kind !== "interval" && !selectedClass) || saving || pending || dataError) return;
        if (atMaximum) { setError(`This class has reached the ${form.day === "5" ? 5 : 9}-period daily maximum.`); return; }
        if (form.day === "1" && assembly && form.startTime < assembly.endTime.slice(0, 5)) { setError(`Monday period 1 is Assembly. Teaching starts at ${assembly.endTime.slice(0, 5)}.`); return; }
        if (!form.startTime || !form.endTime || form.endTime <= form.startTime) { setError("End time must be later than start time."); return; }
        const schedule = { academicYearId: Number(yearId), academicTermId: Number(termId),
            day: Number(form.day), startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00` };
        if (form.kind === "interval") {
            mutate(() => editingId === null ? timetableManagementApi.createInterval(schedule)
                : timetableManagementApi.updateInterval(editingId, schedule), "Interval saved successfully.");
            return;
        }
        const payload = { ...schedule, schoolClassId: selectedClass.id, subjectId: assignment.subject.id,
            staffId: assignment.teacher.id, room: null };
        mutate(() => editingId === null ? timetableManagementApi.create(payload)
            : timetableManagementApi.update(editingId, { ...payload, isActive: true }), `Timetable period ${editingId === null ? "created" : "updated"} successfully.`);
    }
    if (!allowed) return <p className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600">Sign in with a school management role to manage the timetable.</p>;

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div><h1 className="flex items-center gap-3 text-2xl font-bold text-slate-950"><CalendarDays className="h-6 w-6 text-blue-600" />Timetable Management</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">Choose a teacher and subject, then select the grade, class and time. Different teachers can teach the same subject to the same class in separate periods. Class and teacher times cannot overlap. Maximum 9 periods per class Monday–Thursday and 5 on Friday.</p></div>
                <button type="button" disabled={saving || pending} onClick={() => { setDeactivating(null); setReload((value) => value + 1); }} className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}><RefreshCw className="h-4 w-4" />Refresh</button>
            </div>
            {(dataError || error) && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{dataError || error}</p>}
            {success && <p role="status" className="rounded-xl border border-green-200 bg-green-50 p-4 text-sm text-green-800">{success}</p>}
            <section className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6" aria-label="Academic period">
                <div className="grid gap-5 sm:grid-cols-2">
                    <label className="text-sm font-medium text-slate-700">Academic year<select value={yearId} onChange={(event) => changeYear(event.target.value)} disabled={saving || loading || !!loadError} className={`${inputClass} cursor-pointer`}><option value="">Select academic year</option>{years.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
                    <label className="text-sm font-medium text-slate-700">Academic term<select value={termId} onChange={(event) => changeTerm(event.target.value)} disabled={saving || pending || !validYear || !!dataError} className={`${inputClass} cursor-pointer`}><option value="">Select term</option>{terms.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
                </div>
                {yearId && !termsLoading && !termsError && !terms.length && <p className="mt-4 text-sm text-slate-500">Create a term for this year in Terms &amp; Exams first.</p>}
            </section>
            <MondayAssembly editable={allowed} onChanged={setAssembly} />
            <form onSubmit={save} className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
                <h2 className="text-lg font-semibold text-slate-950">{editingId === null ? "Add a timetable period" : "Edit timetable period"}</h2>
                <fieldset disabled={saving || pending || !!dataError || !validYear || !validTerm} className="mt-5 grid min-w-0 gap-5 sm:grid-cols-2 xl:grid-cols-3">
                    <label className="text-sm font-medium text-slate-700 sm:col-span-2 xl:col-span-3">Period type<select name="kind" value={form.kind} onChange={changeForm} disabled={editingId !== null} className={inputClass}><option value="teaching">Teaching</option><option value="interval">Interval</option></select></label>
                    {form.kind === "teaching" && <label className="text-sm font-medium text-slate-700 sm:col-span-2 xl:col-span-3">Teacher / Subject<span className="required-marker" aria-hidden="true"> *</span><select required name="assignmentId" value={form.assignmentId} onChange={changeForm} className={`${inputClass} cursor-pointer`}><option value="">Select teacher and subject</option>{availableAssignments.map((item) => <option key={item.id} value={item.id}>{assignmentLabel(item)}</option>)}</select></label>}
                    <label className="text-sm font-medium text-slate-700">Day<select name="day" value={form.day} onChange={changeForm} className={`${inputClass} cursor-pointer`}>{days.map((day, index) => <option key={day} value={index + 1}>{day}</option>)}</select></label>
                    <label className="text-sm font-medium text-slate-700">Start time<span className="required-marker" aria-hidden="true"> *</span><input required type="time" min={form.day === "1" ? assembly?.endTime.slice(0, 5) : undefined} name="startTime" value={form.startTime} onChange={changeForm} className={inputClass} /></label>
                    <label className="text-sm font-medium text-slate-700">End time<span className="required-marker" aria-hidden="true"> *</span><input required type="time" name="endTime" value={form.endTime} onChange={changeForm} className={inputClass} /></label>
                    {form.kind === "teaching" && <label className="text-sm font-medium text-slate-700">Grade *<span className="required-marker" aria-hidden="true"> *</span><select required name="gradeId" value={form.gradeId} onChange={changeForm} className={inputClass}><option value="">Select grade</option>{grades.map((item) => <option key={item.id} value={item.id}>{item.sectionName} · {item.name}</option>)}</select></label>}
                    {form.kind === "teaching" && <label className="text-sm font-medium text-slate-700">Class *<span className="required-marker" aria-hidden="true"> *</span><select required name="schoolClassId" value={form.schoolClassId} onChange={changeForm} disabled={!form.gradeId || saving || pending} className={inputClass}><option value="">Select class</option>{classes.filter((item) => String(item.gradeId) === form.gradeId).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
                </fieldset>
                {form.kind === "teaching" && validYear && !loading && !availableAssignments.length && <p className="mt-4 text-sm text-slate-500">Create a subject-teacher assignment for this year in Teacher Assignments first.</p>}
                <div className="mt-5 flex flex-wrap gap-3"><button type="submit" disabled={saving || pending || !!dataError || !validYear || !validTerm || (form.kind !== "interval" && !assignment) || (form.kind !== "interval" && !selectedClass) || atMaximum} className={`${buttonClass} w-full bg-blue-600 text-white hover:bg-blue-700 sm:w-auto`}>{saving ? "Saving…" : editingId === null ? "Create period" : "Save changes"}</button>{editingId !== null && <button type="button" disabled={saving} onClick={resetForm} className={`${buttonClass} border border-slate-200 text-slate-700 hover:bg-slate-50`}>Cancel edit</button>}</div>
            </form>
            {form.kind === "teaching" && selectedClass && validTerm && <section className="grid gap-3 sm:grid-cols-5" aria-label="Daily period counts">{days.map((day, index) => <div key={day} className="rounded-xl border border-slate-200 bg-white p-4"><p className="text-sm font-medium text-slate-700">{day}</p><p className="mt-2 font-semibold text-blue-700">{classPeriods.filter((item) => item.dayValue === index + 1 && item.kind !== "interval").length + (index === 0 ? 1 : 0)} / {index === 4 ? 5 : 9} periods{index === 0 ? " (includes Assembly)" : ""}</p></div>)}</section>}
            {assembly && classPeriods.some((item) => item.dayValue === 1 && item.startTime < assembly.endTime) && <p role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">An existing Monday teaching period starts before {assembly?.endTime.slice(0, 5)}. Edit or remove it to make room for Assembly.</p>}
            {form.kind === "interval" && <p className="text-sm text-slate-500">Intervals reserve time for all classes and do not count toward the teaching-period limit.</p>}
            {atMaximum && <p role="status" className="text-sm text-amber-700">Daily maximum reached. Edit or remove a period to free a slot.</p>}
            {deactivating && <section className="rounded-2xl border border-amber-200 bg-amber-50 p-5" aria-label="Confirm removal"><h2 className="font-semibold text-amber-950">Remove this period?</h2><p className="mt-2 text-sm text-amber-900">{deactivating.day} · {deactivating.startTime.slice(0, 5)}–{deactivating.endTime.slice(0, 5)} · {deactivating.kind === "interval" ? "All classes" : `${deactivating.schoolClass.grade.name} ${deactivating.schoolClass.name}`} · {deactivating.subject.name}. It will be removed from the active timetable.</p><div className="mt-4 flex flex-wrap gap-3"><button type="button" disabled={saving || pending || !!dataError} onClick={() => mutate(() => (deactivating.kind === "interval" ? timetableManagementApi.removeInterval(deactivating.id) : timetableManagementApi.deactivate(deactivating.id)), "Timetable period deactivated.")} className={`${buttonClass} bg-red-600 text-white hover:bg-red-700`}>Confirm removal</button><button type="button" disabled={saving} onClick={() => setDeactivating(null)} className={`${buttonClass} border border-amber-200 bg-white text-slate-700`}>Cancel</button></div></section>}
            <section className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6" aria-label="Weekly timetable">
                <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold text-slate-950">Weekly timetable</h2><button type="button" disabled={saving} onClick={() => { changeYear(""); setSearch(""); setDayFilter(""); }} className={`${buttonClass} text-blue-600 hover:bg-blue-50`}>Clear filters</button></div>
                <div className="mt-4 grid gap-4 sm:grid-cols-[1fr_200px]"><label className="text-sm font-medium text-slate-700">Search<input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Teacher, subject or class" className={inputClass} /></label><label className="text-sm font-medium text-slate-700">Day<select value={dayFilter} onChange={(event) => setDayFilter(event.target.value)} className={`${inputClass} cursor-pointer`}><option value="">All weekdays</option>{days.map((day, index) => <option key={day} value={index + 1}>{day}</option>)}</select></label></div>
                {loading ? <p role="status" className="py-8 text-sm text-slate-500">Loading timetable…</p> : loadError ? <p className="py-8 text-sm text-slate-500">Refresh to load the current timetable.</p> : !visibleEntries.length ? <p className="py-8 text-sm text-slate-500">No active periods match these filters.</p> : <div className="mt-5 space-y-6">{days.map((day, index) => {
                    const periods = visibleEntries.filter((item) => item.dayValue === index + 1).sort((a, b) => a.startTime.localeCompare(b.startTime));
                    return periods.length > 0 && <div key={day}><h3 className="mb-3 font-semibold text-slate-950">{day}</h3><div className="grid gap-4 xl:grid-cols-2">{periods.map((entry) => <article key={`${entry.kind}:${entry.id}`} className="min-w-0 rounded-xl border border-slate-200 p-4"><p className="font-semibold text-blue-700">{entry.startTime.slice(0, 5)}–{entry.endTime.slice(0, 5)} · {entry.subject.name}</p><p className="mt-2 break-words font-medium text-slate-950">{entry.kind === "interval" ? "All classes" : `${entry.schoolClass.section.name} · ${entry.schoolClass.grade.name} ${entry.schoolClass.name}`}</p>{entry.kind !== "interval" && <p className="mt-1 break-words text-sm text-slate-500">{entry.teacher.staffNumber} — {entry.teacher.fullName}</p>}<p className="mt-2 break-words text-sm text-slate-500">{entry.academicYear.name} · {entry.academicTerm.name}</p><div className="mt-3 flex flex-wrap gap-2"><button type="button" disabled={saving || pending || !!dataError} onClick={() => edit(entry)} className={`${buttonClass} text-blue-600 hover:bg-blue-50`}><Pencil className="h-4 w-4" />Edit</button><button type="button" disabled={saving || pending || !!dataError} onClick={() => { setDeactivating(entry); setError(""); setSuccess(""); }} className={`${buttonClass} text-red-600 hover:bg-red-50`}>Remove</button></div></article>)}</div></div>;
                })}</div>}
            </section>
        </div>
    );
}
