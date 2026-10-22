import { useCallback, useEffect, useState } from "react";
import api from "../../api/axios";
import { useAuth } from "../../context/AuthContext";
import TermsExamsManagementPage from "./TermsExamsManagementPage";

const input = "mt-1 w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
export function AssessmentList({ schedules, onEdit, onCancel }) {
    return <div className="space-y-3">{!schedules.length && <p className="text-sm text-slate-500">No exams or tests for this period.</p>}{schedules.map(e => <article key={e.id} className={`space-y-2 rounded-xl border p-4 ${e.isCancelled ? "border-slate-200 bg-slate-50" : "border-blue-200 bg-white"}`}>
        <div className="flex flex-wrap justify-between gap-2"><h3 className="font-semibold">{e.title}</h3><span className="text-xs font-semibold text-slate-600">{e.isCancelled ? "Cancelled" : e.isOfficial ? "Government exam" : "Internal test"}</span></div>
        <p className="text-sm text-slate-600">{e.date} · {e.startTime.slice(0, 5)}–{e.endTime.slice(0, 5)} · {e.className} · {e.subjectName}</p>
        <p className="text-sm text-slate-600">Maximum marks: {e.maximumMarks}{e.location ? ` · ${e.location}` : ""}</p>
        {e.description && <p className="whitespace-pre-wrap text-sm">{e.description}</p>}
        {e.isCancelled && <p className="text-sm text-red-700">Reason: {e.cancellationReason}</p>}
        {e.canEdit && onEdit && !e.isCancelled && <div className="flex gap-4 text-sm"><button className="font-semibold text-blue-700" onClick={() => onEdit(e)}>Edit schedule</button><button className="text-red-700" onClick={() => onCancel(e)}>Cancel assessment</button></div>}
    </article>)}</div>;
}
export default function ExamsTestsPage() {
    const { user } = useAuth();
    const leader = ["Admin", "Principal", "Deputy Principal"].some(r => user?.roles?.includes(r));
    const [setupVersion, setSetupVersion] = useState(0);
    const [tab, setTab] = useState(leader ? "official" : "tests");
    return <main className="space-y-5"><header><h1 className="text-2xl font-bold">Exams &amp; Tests</h1><p className="mt-2 text-sm text-slate-500">Record government exam timetables and create internal tests. Times use the school's local time.</p></header>
        <nav aria-label="Assessment type" className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3">{[["official", "Government Exam Timetable"], ["tests", "Internal Tests"]].map(([key, label]) => <button key={key} aria-current={tab === key ? "page" : undefined} className={tab === key ? button : "rounded-xl px-4 py-3 text-sm text-slate-600"} onClick={() => setTab(key)}>{label}</button>)}</nav>
        {tab === "official" && leader && <details className="rounded-xl border border-slate-200 bg-white p-4"><summary className="cursor-pointer font-semibold">Step 1: Record the government exam period</summary><TermsExamsManagementPage onExamChanged={() => setSetupVersion(v => v + 1)} /></details>}
        <AssessmentSchedules key={`${tab}:${setupVersion}`} official={tab === "official"} canCreate={tab !== "official" || leader} />
    </main>;
}
function AssessmentSchedules({ official, canCreate }) {
    const [calendarYear, setCalendarYear] = useState(new Date().getFullYear());
    const [options, setOptions] = useState(null);
    const [schedules, setSchedules] = useState([]);
    const [form, setForm] = useState(null);
    const [cancel, setCancel] = useState(null);
    const [reason, setReason] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const reload = useCallback(async (year = calendarYear, signal) => {
        const r = await api.get("/assessments", { params: { year }, signal }); setSchedules(r.data.schedules);
    }, [calendarYear]);
    useEffect(() => {
        const c = new AbortController();
        Promise.resolve().then(() => reload(calendarYear, c.signal)).catch(e => { if (!c.signal.aborted) setError(e.response?.data?.message || "Unable to load assessments."); });
        return () => c.abort();
    }, [calendarYear, reload]);
    useEffect(() => {
        const c = new AbortController();
        api.get("/assessments/options", { signal: c.signal }).then(r => setOptions(r.data)).catch(e => { if (!c.signal.aborted) setError(e.response?.data?.message || "Unable to load assigned classes."); });
        return () => c.abort();
    }, []);
    async function chooseYear(year) {
        setBusy(true); setError("");
        try { const r = await api.get("/assessments/options", { params: { academicYearId: year } }); setOptions(r.data); setForm(f => f && ({ ...f, academicYearId: Number(year), schoolClassId: "", subjectId: "", examId: "" })); }
        catch (e) { setError(e.response?.data?.message || "Unable to load this academic year."); } finally { setBusy(false); }
    }
    function create() {
        const year = options.years.find(y => y.id === options.academicYearId);
        const now = new Date(); const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
        const date = year ? today < year.startDate.slice(0, 10) || today > year.endDate.slice(0, 10) ? year.startDate.slice(0, 10) : today : today;
        setForm({ isOfficial: official, academicYearId: options.academicYearId, schoolClassId: "", subjectId: "", examId: "", title: "", description: "", location: "", date, startTime: "09:00", endTime: "10:00", maximumMarks: 100, revision: 0 }); setCancel(null); setError(""); setMessage("");
    }
    const change = (key, value) => setForm(f => ({ ...f, [key]: value }));
    const schoolClass = options?.classes.find(c => Number(c.id) === Number(form?.schoolClassId));
    const selectedExam = options?.exams.find(e => Number(e.id) === Number(form?.examId));
    const year = options?.years.find(y => Number(y.id) === Number(form?.academicYearId));
    async function save(event) {
        event.preventDefault(); setBusy(true); setError(""); setMessage("");
        try {
            const payload = { ...form, academicYearId: Number(form.academicYearId), schoolClassId: Number(form.schoolClassId), subjectId: form.subjectId ? Number(form.subjectId) : null, examId: official ? Number(form.examId) : null, maximumMarks: Number(form.maximumMarks) };
            const r = form.id ? await api.put(`/assessments/${form.id}`, payload) : await api.post("/assessments", payload);
            setForm(null); setMessage(r.data.message); const year = Number(form.date.slice(0, 4)); setCalendarYear(year); await reload(year);
        } catch (e) { setError(e.response?.data?.message || "Unable to save assessment."); } finally { setBusy(false); }
    }
    async function cancelAssessment(event) {
        event.preventDefault(); setBusy(true); setError(""); setMessage("");
        try { const r = await api.post(`/assessments/${cancel.id}/cancel`, { revision: cancel.revision, reason }); setCancel(null); setMessage(r.data.message); await reload(); }
        catch (e) { setError(e.response?.data?.message || "Unable to cancel assessment."); } finally { setBusy(false); }
    }
    async function edit(e) {
        setBusy(true); setError(""); setMessage("");
        try { const r = await api.get("/assessments/options", { params: { academicYearId: e.academicYearId } }); setOptions(r.data); setForm({ ...e, subjectId: e.subjectId ?? "", examId: e.examId ?? "", startTime: e.startTime.slice(0, 5), endTime: e.endTime.slice(0, 5) }); setCancel(null); window.scrollTo({ top: 0, behavior: "smooth" }); }
        catch (err) { setError(err.response?.data?.message || "Unable to edit this assessment."); } finally { setBusy(false); }
    }
    return <section className="space-y-4"><div className="flex flex-wrap items-end justify-between gap-3"><label className="text-sm">Calendar year<input type="number" min="1900" max="2100" disabled={busy} className={input} value={calendarYear} onChange={e => { setCalendarYear(Number(e.target.value)); setError(""); }} /></label>{canCreate && <button className={button} disabled={busy || !options?.years.length || !options.classes.length} onClick={create}>{official ? "Add government timetable entry" : "Create internal test"}</button>}</div>
        <p className="text-sm text-slate-500">{official ? "Step 2: Enter each subject, date and time exactly as announced by the government. Only school leaders can record or correct this timetable." : "Section heads manage tests in their section. Teachers create tests for their assigned class or subject. Tests stay separate from official term results."}</p>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-blue-700">{message}</p>}
        {canCreate && options && !options.classes.length && <p className="text-sm text-slate-500">No assigned classes are available. Check the active academic year and staff assignments.</p>}
        {form && <form onSubmit={save} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5"><h2 className="text-lg font-semibold">{form.id ? "Edit schedule" : official ? "Record government timetable entry" : "Create internal test"}</h2><fieldset disabled={busy} className="space-y-4">
            <label className="block text-sm">Academic year<span className="required-marker" aria-hidden="true"> *</span><select required className={input} value={form.academicYearId} onChange={e => chooseYear(e.target.value)}>{options.years.map(y => <option value={y.id} key={y.id}>{y.name}</option>)}</select></label>
            {official && <label className="block text-sm">Government exam period<span className="required-marker" aria-hidden="true"> *</span><select required className={input} value={form.examId} onChange={e => { const exam = options.exams.find(x => String(x.id) === e.target.value); setForm({ ...form, examId: e.target.value, maximumMarks: exam?.maximumMarks || 100, ...(exam ? { date: exam.fromDate.slice(0, 10) } : {}) }); }}><option value="">Select government exam period</option>{options.exams.map(e => <option key={e.id} value={e.id}>{e.name}</option>)}</select></label>}
            <div className="grid gap-4 sm:grid-cols-2"><label className="text-sm">Class<span className="required-marker" aria-hidden="true"> *</span><select required className={input} value={form.schoolClassId} onChange={e => setForm({ ...form, schoolClassId: e.target.value, subjectId: "" })}><option value="">Select class</option>{options.classes.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
            <label className="text-sm">Subject{official || !schoolClass?.wholeClass && <span className="required-marker" aria-hidden="true"> *</span>}<select required={official || !schoolClass?.wholeClass} disabled={!schoolClass} className={input} value={form.subjectId} onChange={e => change("subjectId", e.target.value)}><option value="">{!official && schoolClass?.wholeClass ? "Whole class test" : "Select subject"}</option>{schoolClass?.subjects.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label></div>
            {official && selectedExam && <p className="text-sm text-slate-600">Government exam period: {selectedExam.fromDate.slice(0, 10)} to {selectedExam.throughDate.slice(0, 10)}. Choose the subject date and time from the supplied timetable.</p>}
            <label className="block text-sm">{official ? "Timetable entry title" : "Test title"}<span className="required-marker" aria-hidden="true"> *</span><input required maxLength={200} className={input} value={form.title} onChange={e => change("title", e.target.value)} /></label>
            <div className="grid gap-4 sm:grid-cols-3"><label className="text-sm">Date<span className="required-marker" aria-hidden="true"> *</span><input required type="date" min={official && selectedExam ? selectedExam.fromDate.slice(0, 10) : year?.startDate.slice(0, 10)} max={official && selectedExam ? selectedExam.throughDate.slice(0, 10) : year?.endDate.slice(0, 10)} className={input} value={form.date} onChange={e => change("date", e.target.value)} /></label>{[["startTime", "Start time"], ["endTime", "End time"]].map(([key, label]) => <label key={key} className="text-sm">{label}<span className="required-marker" aria-hidden="true"> *</span><input required type="time" className={input} value={form[key]} onChange={e => change(key, e.target.value)} /></label>)}</div>
            <label className="block text-sm">Maximum marks<span className="required-marker" aria-hidden="true"> *</span><input required disabled={official} type="number" min="0.01" max="1000000" step="0.01" className={input} value={form.maximumMarks} onChange={e => change("maximumMarks", e.target.value)} /></label>
            <label className="block text-sm">Location<input maxLength={300} className={input} value={form.location} onChange={e => change("location", e.target.value)} /></label>
            <label className="block text-sm">Instructions<textarea maxLength={4000} rows={3} className={input} value={form.description} onChange={e => change("description", e.target.value)} /></label>
            <p className="text-xs text-slate-500">Publishing notifies the relevant class / subject teachers, section heads, students and linked parents. The person publishing is not notified about their own action.</p>
        </fieldset><button disabled={busy} className={button}>{form.id ? "Save and notify" : official ? "Publish timetable entry and notify" : "Publish internal test and notify"}</button><button disabled={busy} type="button" className="ml-3 text-sm" onClick={() => setForm(null)}>Close</button></form>}
        {cancel && <form onSubmit={cancelAssessment} className="space-y-3 rounded-xl border border-red-200 bg-white p-5"><h2 className="font-semibold">Cancel {cancel.title}?</h2><textarea required aria-label="Cancellation reason" disabled={busy} maxLength={1000} className={input} value={reason} onChange={e => setReason(e.target.value)} /><button disabled={busy || !reason.trim()} className={button}>Confirm cancellation</button><button type="button" disabled={busy} className="ml-3 text-sm" onClick={() => setCancel(null)}>Keep assessment</button></form>}
        <fieldset disabled={busy}><AssessmentList schedules={schedules.filter(e => e.isOfficial === official)} onEdit={edit} onCancel={e => { setCancel(e); setReason(""); setForm(null); setError(""); setMessage(""); }} /></fieldset>
    </section>;
}
