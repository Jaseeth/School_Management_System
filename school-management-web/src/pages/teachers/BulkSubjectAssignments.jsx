import { useEffect, useRef, useState } from "react";
import api from "../../api/axios";
import { DraftDialog } from "../../components/RegistrationDrafts";
const input = "mt-2 w-full rounded-xl border border-slate-200 bg-white p-3 text-sm disabled:bg-slate-50";
const button = "rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold disabled:opacity-50";
const newRow = () => ({ key: crypto.randomUUID(), subjectId: "", sectionId: "", gradeId: "", schoolClassId: "" });
export default function BulkSubjectAssignments({ refresh, onSaved, onContextChanged, onBusy }) {
    const [options, setOptions] = useState({ years: [], staff: [], classes: [] }), [yearId, setYear] = useState(""), [staffId, setStaff] = useState("");
    const [rows, setRows] = useState([newRow()]), [loading, setLoading] = useState(true), [busy, setBusy] = useState(false), [error, setError] = useState(""), [success, setSuccess] = useState(""), [review, setReview] = useState(false);
    const submitting = useRef(false);
    useEffect(() => {
        const c = new AbortController();
        api.get("/teacher-assignments/options", { signal: c.signal }).then(r => { setOptions(r.data); setLoading(false); }).catch(e => { if (!c.signal.aborted) { setLoading(false); setError(e.response?.data?.message || "Unable to load teaching staff and classes. Refresh to retry."); } });
        return () => c.abort();
    }, [refresh]);
    const staff = options.staff.find(s => String(s.id) === staffId), year = options.years.find(y => String(y.id) === yearId);
    const unique = (items, id, name) => [...new Map(items.map(x => [x[id], { id: x[id], name: x[name] }])).values()];
    const sections = unique(options.classes, "sectionId", "section");
    const complete = Boolean(year && staff && rows.length && rows.every(r => staff.subjects.some(s => String(s.id) === r.subjectId) && options.classes.some(c => String(c.id) === r.schoolClassId && String(c.gradeId) === r.gradeId && String(c.sectionId) === r.sectionId)));
    function contextChange(y, s) { setYear(y); setStaff(s); setRows([newRow()]); setReview(false); setError(""); setSuccess(""); onContextChanged(y, s); }
    function change(key, field, value) { setRows(v => v.map(r => r.key === key ? { ...r, [field]: value, ...(field === "sectionId" ? { gradeId: "", schoolClassId: "" } : field === "gradeId" ? { schoolClassId: "" } : {}) } : r)); setReview(false); setError(""); setSuccess(""); }
    async function save() {
        if (submitting.current || !complete) return; submitting.current = true; setBusy(true); onBusy?.(true); setError("");
        try { const { data } = await api.post("/teacher-assignments/bulk", { academicYearId: Number(yearId), staffId: Number(staffId), rows: rows.map(r => ({ subjectId: Number(r.subjectId), schoolClassId: Number(r.schoolClassId) })) }); setSuccess(data.message); setReview(false); setYear(""); setStaff(""); setRows([newRow()]); onContextChanged("", ""); onSaved(); }
        catch(e) { setError(e.response?.data?.message || "Could not confirm assignments. Refresh the existing assignments before retrying."); } finally { submitting.current = false; setBusy(false); onBusy?.(false); }
    }
    const select = (label, value, choices, changed, disabled = false) => <label className="block min-w-0 text-sm font-medium">{label}<span className="required-marker" aria-hidden="true"> *</span><select required className={input} disabled={busy || loading || disabled} value={value} onChange={e => changed(e.target.value)}><option value="">Select {label.toLowerCase()}</option>{choices.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>;
    return <section className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6" aria-label="Assign subject teachers"><h2 className="text-lg font-semibold">Assign subject teachers</h2><p className="text-sm text-slate-500">Choose the year and a staff member marked as able to teach. Each row assigns one of their subjects to a class. Multiple staff can be assigned the same subject and class; the timetable decides who teaches each period. Add rows for different sections, grades, classes or subjects, then save together.</p>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-sm text-red-700">{error}</p>}{success && <p role="status" className="rounded-xl bg-green-50 p-4 text-sm text-green-800">{success}</p>}
        <form onSubmit={e => { e.preventDefault(); setError(""); const keys = rows.map(r => `${r.subjectId}:${r.schoolClassId}`); if (new Set(keys).size !== keys.length) { setError("Remove duplicate subject/class rows before saving."); return; } setReview(true); }} className="space-y-5"><div className="grid gap-4 sm:grid-cols-2">
        {select("Academic year", yearId, options.years, v => contextChange(v, ""))}{select("Teaching staff", staffId, options.staff.map(s => ({ id: s.id, name: `${s.staffNumber} — ${s.fullName}` })), v => contextChange(yearId, v), !year)}
        </div>{!loading && !options.staff.length && <p className="text-sm text-amber-800">No eligible teaching staff. In Staff Management, choose Teaching subjects, set Can teach to Yes, and select their subjects. The account must be active and have the Admin, Section Head or Teacher role.</p>}
        {staff && <><p className="text-sm text-slate-600">Available subjects (from teaching subjects and existing assignments): {staff.subjects.map(s => s.name).join(", ") || "None active. Update Teaching subjects in Staff Management."}</p><div className="space-y-4">{rows.map((r, i) => <fieldset key={r.key} className="min-w-0 rounded-xl border border-slate-200 p-4"><legend className="px-1 text-sm font-semibold">Assignment {i + 1}</legend><div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {select("Subject", r.subjectId, staff.subjects, v => change(r.key, "subjectId", v))}
            {select("Section", r.sectionId, sections, v => change(r.key, "sectionId", v))}
            {select("Grade", r.gradeId, unique(options.classes.filter(c => String(c.sectionId) === r.sectionId), "gradeId", "grade"), v => change(r.key, "gradeId", v), !r.sectionId)}
            {select("Class", r.schoolClassId, options.classes.filter(c => String(c.sectionId) === r.sectionId && String(c.gradeId) === r.gradeId), v => change(r.key, "schoolClassId", v), !r.gradeId)}
        </div><button type="button" className="mt-3 text-sm text-red-700 disabled:opacity-50" disabled={busy || rows.length === 1} onClick={() => { setRows(v => v.filter(x => x.key !== r.key)); setError(""); }}>Remove row {i + 1}</button></fieldset>)}</div>
        <div className="flex flex-wrap gap-3"><button type="button" className={button} disabled={busy || rows.length >= 100} onClick={() => { setRows(v => [...v, newRow()]); setReview(false); setError(""); }}>Add another assignment</button><button className={button + " bg-blue-600 text-white"} disabled={busy || loading || !complete}>Review assignments ({rows.length})</button></div></>}
        <button type="button" className={button} disabled={busy} onClick={() => contextChange("", "")}>Clear selections</button></form>
        {review && <DraftDialog title="Save teaching assignments?"><p className="text-sm font-semibold">{staff?.fullName} · {year?.name}</p><div className="max-h-80 space-y-2 overflow-auto">{rows.map((r, i) => { const c = options.classes.find(c => String(c.id) === r.schoolClassId); return <p className="rounded-xl border p-3 text-sm" key={r.key}>{i + 1}. {staff?.subjects.find(s => String(s.id) === r.subjectId)?.name} · {c?.section} · Grade {c?.grade} · Class {c?.name}</p>; })}</div><p className="text-sm">All rows are checked together. An existing inactive assignment will be reactivated, retaining its history. Conflicts stop the entire save.</p>{error && <p role="alert" className="text-red-700">{error}</p>}<div className="flex gap-3"><button type="button" className={button + " bg-blue-600 text-white"} disabled={busy} onClick={save}>{busy ? "Saving…" : "Save assignments"}</button><button type="button" className={button} disabled={busy} onClick={() => setReview(false)}>Go back</button></div></DraftDialog>}
    </section>;
}
