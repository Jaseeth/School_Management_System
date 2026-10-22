import { useEffect, useState } from "react";
import api from "../../api/axios";
import { DraftDialog } from "../../components/RegistrationDrafts";
const input = "mt-2 block w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold disabled:opacity-50";
export default function MarksReviewerAssignments({ onChanged }) {
    const [options, setOptions] = useState(null), [scope, setScope] = useState(""), [rows, setRows] = useState([]);
    const [teacher, setTeacher] = useState(""), [reason, setReason] = useState(""), [error, setError] = useState(""), [message, setMessage] = useState("");
    const [reload, setReload] = useState(0), [confirm, setConfirm] = useState(null), [busy, setBusy] = useState(false), [loading, setLoading] = useState(true);
    useEffect(() => { const c = new AbortController(); api.get("/marks-reviewers/options", { signal: c.signal }).then(r => { setOptions(r.data); if (r.data.scopes[0]) setScope(`${r.data.scopes[0].academicYearId}:${r.data.scopes[0].sectionId}`); else setLoading(false); }).catch(() => { if (!c.signal.aborted) { setError("Unable to load reviewer assignment options."); setLoading(false); } }); return () => c.abort(); }, []);
    const [yearId, sectionId] = scope.split(":").map(Number);
    useEffect(() => {
        if (!scope) return;
        const c = new AbortController(); api.get("/marks-reviewers", { signal: c.signal, params: { yearId, sectionId } }).then(r => { setRows(r.data); setLoading(false); }).catch(() => { if (!c.signal.aborted) { setError("Unable to load assigned reviewers."); setLoading(false); } }); return () => c.abort();
    }, [scope, yearId, sectionId, reload]);
    const current = rows.find(r => r.isActive);
    const selected = options?.teachers.find(t => t.id === Number(teacher) && t.sectionId === sectionId && t.academicYearId === yearId);
    async function save() {
        if (busy) return; setBusy(true); setError("");
        try {
            const r = confirm === "remove" ? await api.post(`/marks-reviewers/${current.id}/remove`, { reason })
                : await api.post("/marks-reviewers", { sectionId, yearId, staffId: Number(teacher), reason, previousId: current?.id || null });
            setMessage(r.data.message); setConfirm(null); setTeacher(""); setReason(""); setReload(v => v + 1); onChanged?.();
        } catch(e) { setError(e.response?.data?.message || "Unable to update the reviewer. Refresh and try again."); }
        finally { setBusy(false); }
    }
    return <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5" aria-label="Reviewer assignments"><h2 className="text-lg font-semibold">Section marks reviewer</h2><p className="text-sm text-slate-500">Assign a teacher from your section to help review submitted marks. Either you or the reviewer can approve or return a submission; nobody can review their own marks. Publishing permission is unchanged.</p>
        {error && <p role="alert" className="text-sm text-red-700">{error}</p>}{message && <p role="status" className="text-sm text-blue-700">{message}</p>}
        {options?.scopes.length === 0 ? <p className="text-sm">You have no active section-head assignments.</p> : <form onSubmit={e => { e.preventDefault(); setError(""); setConfirm("assign"); }} className="space-y-4"><div className="grid gap-4 md:grid-cols-2"><label className="text-sm">Section / academic year<select className={input} value={scope} disabled={busy} onChange={e => { setScope(e.target.value); setRows([]); setTeacher(""); setLoading(true); setReason(""); setError(""); setMessage(""); }}>{options?.scopes.map(s => <option key={`${s.academicYearId}:${s.sectionId}`} value={`${s.academicYearId}:${s.sectionId}`}>{s.section} · {s.year}</option>)}</select></label><label className="text-sm">Reviewer teacher<span className="required-marker" aria-hidden="true"> *</span><select required className={input} disabled={busy || loading} value={teacher} onChange={e => setTeacher(e.target.value)}><option value="">Select a teacher from this section</option>{options?.teachers.filter(t => t.sectionId === sectionId && t.academicYearId === yearId).map(t => <option key={t.id} value={t.id}>{t.staffNumber} — {t.fullName}</option>)}</select></label></div>
        <p className="text-sm">Current reviewer: <strong>{loading ? "Loading…" : current ? `${current.teacher} (${current.staffNumber})` : "None assigned"}</strong></p>
        <label className="block text-sm">Assignment / removal reason<span className="required-marker" aria-hidden="true"> *</span><textarea required maxLength={1000} className={input} disabled={busy} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <div className="flex flex-wrap gap-3"><button className={button + " bg-blue-600 text-white"} disabled={busy || loading || !selected || !reason.trim() || current?.staffId === Number(teacher)}>{current ? "Replace reviewer" : "Assign reviewer"}</button>{current && <button type="button" className={button + " text-red-700"} disabled={busy || !reason.trim()} onClick={() => { setError(""); setConfirm("remove"); }}>Remove reviewer</button>}</div></form>}
        {rows.length > 0 && <details className="text-sm"><summary className="cursor-pointer font-semibold">Reviewer assignment history</summary><div className="mt-3 space-y-3">{rows.map(r => <article key={r.id} className="rounded-xl border border-slate-200 p-3"><p className="font-medium">{r.teacher} ({r.staffNumber}) · {r.isActive ? "Assigned" : "Ended"}</p><p>{r.reason}</p><p className="text-slate-500">Assigned by {r.assignedBy} · {new Date(r.assignedAt + (/Z$/.test(r.assignedAt) ? "" : "Z")).toLocaleString()}</p>{r.endedAt && <p>Ended: {new Date(r.endedAt + (/Z$/.test(r.endedAt) ? "" : "Z")).toLocaleString()} · {r.endReason}</p>}</article>)}</div></details>}
        {confirm && <DraftDialog title={confirm === "remove" ? "Remove marks reviewer?" : current ? "Replace marks reviewer?" : "Assign marks reviewer?"}><p className="text-sm">{confirm === "remove" ? `Remove ${current?.teacher}’s assigned review access?` : `Assign ${selected?.fullName} (${selected?.staffNumber})? ${current ? `${current.teacher} will lose this reviewer assignment.` : ""}`} You will retain your section-head review access.</p>{error && <p role="alert" className="text-red-700">{error}</p>}<div className="flex gap-3"><button type="button" className={button + " bg-blue-600 text-white"} disabled={busy} onClick={save}>{busy ? "Saving…" : "Confirm"}</button><button type="button" className={button} disabled={busy} onClick={() => setConfirm(null)}>Cancel</button></div></DraftDialog>}
    </section>;
}
