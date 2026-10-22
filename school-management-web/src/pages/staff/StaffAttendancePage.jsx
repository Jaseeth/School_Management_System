import { useCallback, useEffect, useState } from "react";
import api from "../../api/axios";

const control = "rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50";
export default function StaffAttendancePage() {
    const [day, setDay] = useState("");
    const [search, setSearch] = useState("");
    const [data, setData] = useState(null);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [edit, setEdit] = useState(null);
    const [history, setHistory] = useState(null);
    const load = useCallback(async signal => {
        const response = await api.get("/staff-attendance", { signal, params: { date: day || undefined, search } });
        if (!signal?.aborted) { setData(response.data); setError(""); }
    }, [day, search]);
    useEffect(() => {
        const controller = new AbortController();
        const timer = setTimeout(() => { setLoading(true); load(controller.signal).catch(err => { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load staff attendance."); }).finally(() => { if (!controller.signal.aborted) setLoading(false); }); }, 200);
        return () => { clearTimeout(timer); controller.abort(); };
    }, [load]);
    async function save(e) {
        e.preventDefault(); if (busy) return;
        setBusy(true); setError(""); setMessage("");
        try {
            const response = await api.post("/staff-attendance", { staffId: edit.row.id, date: data.date, status: edit.status, remarks: edit.remarks, expectedRecordId: edit.row.recordId, confirmLeaveConflict: edit.confirm });
            setMessage(response.data.message); setEdit(null); await load();
        } catch (err) { setError(err.response?.data?.message || "Unable to save staff attendance."); }
        finally { setBusy(false); }
    }
    async function showHistory(row) {
        setError("");
        try { const response = await api.get(`/staff-attendance/${row.id}/history`, { params: { date: data.date } }); setHistory({ name: row.fullName, rows: response.data.rows }); }
        catch (err) { setError(err.response?.data?.message || "Unable to load attendance history."); }
    }
    const conflict = edit?.row.approvedLeave && ["Present", "Late"].includes(edit.status);
    return <main className="space-y-5"><header><h1 className="text-2xl font-bold">Staff Attendance</h1><p className="mt-2 text-sm text-slate-500">Approved leave appears automatically. School leaders record attendance; section heads view their section. Present or Late during approved leave requires a confirmed reason.</p></header>
        <div className="flex flex-wrap gap-3"><label className="text-sm">Date <input type="date" className={control} value={day || data?.date || ""} onChange={e => { setDay(e.target.value); setEdit(null); setHistory(null); }} /></label><input aria-label="Search staff number or name" type="search" placeholder="Staff number or name" className={control} value={search} onChange={e => { setSearch(e.target.value); setEdit(null); setHistory(null); }} /><button disabled={busy || loading} className={control} onClick={() => load().catch(() => setError("Unable to refresh attendance."))}>Refresh</button></div>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-blue-700">{message}</p>}
        {data && <div className="flex flex-wrap gap-3">{[["Total", data.summary.total], ["Present", data.summary.present], ["Late", data.summary.late], ["Absent", data.summary.absent], ["On approved leave", data.summary.onLeave], ["Not marked", data.summary.notMarked], ["Needs review", data.summary.reviewRequired]].map(([label, value]) => <div key={label} className="min-w-28 rounded-xl border border-slate-200 bg-white p-4"><p className="text-xs text-slate-500">{label}</p><strong className="text-xl">{value}</strong></div>)}</div>}
        {loading ? <p role="status">Loading…</p> : data && !error && <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white p-4"><table className="w-full text-left text-sm"><thead><tr>{["Staff", "Attendance", "Approved leave", "Recorded by", "Actions"].map(label => <th key={label} className="p-3">{label}</th>)}</tr></thead><tbody>{data.rows.map(row => <tr key={row.id} className="border-t border-slate-100"><td className="p-3 font-medium">{row.fullName}<span className="block text-xs text-slate-500">{row.staffNumber}</span></td><td className="p-3">{row.status}{row.conflict && <span className={`block text-xs ${row.reviewRequired ? "text-red-700" : "text-amber-700"}`}>{row.reviewRequired ? "Conflicts with approved leave — review required" : "Leave conflict confirmed by school leader"}</span>}{row.remarks && <p className="mt-1 text-xs text-slate-500">{row.remarks}</p>}</td><td className="p-3">{row.approvedLeave ? `${row.approvedLeave.type}: ${row.approvedLeave.fromDate.slice(0, 10)} to ${row.approvedLeave.toDate.slice(0, 10)}` : "—"}</td><td className="p-3">{row.recordedBy || "—"}{row.recordedAt && <span className="block text-xs text-slate-500">{new Date(row.recordedAt).toLocaleString()}</span>}</td><td className="p-3"><div className="flex flex-wrap gap-2">{data.canMark && <button disabled={busy} className={button} onClick={() => { setHistory(null); setEdit({ row, status: row.recordedStatus || "Present", remarks: "", confirm: false }); }}>{row.recordId ? "Review / change" : "Record"}</button>}<button disabled={busy} className={control} onClick={() => showHistory(row)}>History</button></div></td></tr>)}</tbody></table>{!data.rows.length && <p className="p-3 text-sm text-slate-500">No staff match this search.</p>}</div>}
        {edit && <form onSubmit={save} className="space-y-3 rounded-2xl border border-blue-200 bg-white p-5"><h2 className="font-semibold">Record attendance: {edit.row.fullName} · {data.date}</h2><label className="block text-sm">Status <select disabled={busy} className={control} value={edit.status} onChange={e => setEdit({ ...edit, status: e.target.value, confirm: false })}>{["Present", "Late", "Absent"].map(s => <option key={s}>{s}</option>)}</select></label>{conflict && <div className="rounded-xl bg-amber-50 p-3 text-sm"><p>This person is on approved leave. Verify why they attended. To correct their leave dates, use Withdraw / cancel leave.</p><label className="mt-2 flex gap-2"><input type="checkbox" checked={edit.confirm} disabled={busy} onChange={e => setEdit({ ...edit, confirm: e.target.checked })} />I have checked this leave conflict and confirm this attendance.</label></div>}<label className="block text-sm">Reason / remarks{(edit.row.recordId || conflict) ? " (required)" : " (optional)"}{Boolean(edit.row.recordId || conflict) && <span className="required-marker" aria-hidden="true"> *</span>}<textarea className={`${control} mt-1 block w-full`} disabled={busy} required={Boolean(edit.row.recordId || conflict)} maxLength={1000} value={edit.remarks} onChange={e => setEdit({ ...edit, remarks: e.target.value })} /></label><div className="flex gap-3"><button disabled={busy || conflict && !edit.confirm} className={button}>Save attendance</button><button type="button" disabled={busy} className={control} onClick={() => setEdit(null)}>Cancel</button></div></form>}
        {history && <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5"><div className="flex justify-between"><h2 className="font-semibold">Attendance history: {history.name} · {data.date}</h2><button className={control} onClick={() => setHistory(null)}>Close</button></div>{history.rows.length ? history.rows.map(row => <article key={row.id} className="border-t border-slate-100 pt-3 text-sm"><strong>{row.status}</strong> · {row.recordedBy} · {new Date(row.recordedAt).toLocaleString()}<p>{row.remarks || "No remarks"}</p>{row.leaveConflictConfirmed && <p className="text-amber-700">Leave conflict confirmed</p>}</article>) : <p className="text-sm text-slate-500">No recorded entries. Approved leave is calculated from the current leave approval.</p>}</section>}
    </main>;
}
