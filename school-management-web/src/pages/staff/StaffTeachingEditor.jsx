import { useState } from "react";
import api from "../../api/axios";
import { DraftDialog } from "../../components/RegistrationDrafts";
export default function StaffTeachingEditor({ person, admin, onSaved }) {
    const [open, setOpen] = useState(false), [subjects, setSubjects] = useState([]), [ids, setIds] = useState([]), [answer, setAnswer] = useState("");
    const [busy, setBusy] = useState(false), [loading, setLoading] = useState(false), [error, setError] = useState(""), [search, setSearch] = useState("");
    if (!person.roles?.some(r => ["Admin", "Teacher", "Section Head"].includes(r)) || !admin && person.roles?.some(r => ["Admin", "Principal", "Deputy Principal"].includes(r))) return null;
    async function start() {
        setOpen(true); setLoading(true); setError(""); setSearch(""); setAnswer(person.canTeach == null ? "" : person.canTeach ? "yes" : "no"); setIds(person.teachingSubjects?.map(s => s.subjectId) || []);
        try { const { data } = await api.get("/Staff/teaching-subjects"); setSubjects(data); } catch { setError("Unable to load subjects. Close and try again."); } finally { setLoading(false); }
    }
    async function save(e) {
        e.preventDefault(); setBusy(true); setError("");
        try { await api.put(`/Staff/${person.id}/teaching`, { canTeach: answer === "yes", subjectIds: answer === "yes" ? ids : [] }); setOpen(false); onSaved(); }
        catch(e) { setError(e.response?.data?.message || "Unable to save teaching subjects."); } finally { setBusy(false); }
    }
    return <><button type="button" className="mt-3 text-sm text-blue-700" onClick={start}>Teaching subjects</button>{open && <DraftDialog title="Teaching capabilities"><p>{person.fullName} ({person.staffNumber})</p><form onSubmit={save} className="space-y-4"><label className="block text-sm">Can this staff member teach?<span className="required-marker"> *</span><select required disabled={busy || loading} value={answer} onChange={e => { setAnswer(e.target.value); if(e.target.value === "no") setIds([]); }} className="mt-2 w-full rounded-xl border p-3"><option value="">Select Yes or No</option><option value="yes">Yes</option><option value="no">No</option></select></label>
        {loading ? <p>Loading subjects…</p> : answer === "yes" && <><label className="block text-sm">Search subjects<input className="mt-2 w-full rounded-xl border p-3" value={search} onChange={e => setSearch(e.target.value)} /></label><p className="text-sm">{ids.length} selected</p><div className="max-h-64 overflow-auto">{subjects.filter(s => s.name.toLowerCase().includes(search.toLowerCase())).map(s => <label className="flex items-center gap-3 p-2 text-sm" key={s.id}><input type="checkbox" disabled={busy} checked={ids.includes(s.id)} onChange={e => setIds(v => e.target.checked ? [...v, s.id] : v.filter(id => id !== s.id))} />{s.name}</label>)}</div></>}
        {error && <p role="alert" className="text-sm text-red-700">{error}</p>}<div className="flex gap-3"><button disabled={busy || loading || !answer || answer === "yes" && !ids.length} className="rounded-xl bg-blue-600 px-4 py-3 text-sm text-white">{busy ? "Saving…" : "Save teaching subjects"}</button><button type="button" disabled={busy} onClick={() => setOpen(false)} className="rounded-xl border px-4 py-3 text-sm">Cancel</button></div></form></DraftDialog>}</>;
}
