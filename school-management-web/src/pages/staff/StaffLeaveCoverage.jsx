import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../../api/axios";

const control = "rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
export default function StaffLeaveCoverage({ manage = false, compact = false, refreshVersion = 0 }) {
    const [day, setDay] = useState("");
    const [data, setData] = useState(null);
    const [picked, setPicked] = useState({});
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [remove, setRemove] = useState(null);
    const [change, setChange] = useState(null);
    const [history, setHistory] = useState(null);
    const [filters, setFilters] = useState({ search: "", from: "", to: "", page: 1 });
    const [historyError, setHistoryError] = useState("");
    useEffect(() => {
        if (!manage) return;
        const controller = new AbortController();
        const timer = setTimeout(() => {
            api.get("/leave-coverage/history", { signal: controller.signal, params: filters }).then(response => { setHistory(response.data); setHistoryError(""); })
                .catch(err => { if (!controller.signal.aborted) setHistoryError(err.response?.data?.message || "Unable to load coverage history."); });
        }, 250);
        return () => { clearTimeout(timer); controller.abort(); };
    }, [manage, filters, message, refreshVersion]);
    const load = useCallback(async (signal) => {
        const response = await api.get(manage ? "/leave-coverage/options" : "/leave-coverage/my", { signal, params: manage && day ? { date: day } : undefined });
        if (!signal?.aborted) setData(response.data);
    }, [manage, day]);
    useEffect(() => {
        const controller = new AbortController();
        Promise.resolve().then(() => load(controller.signal)).catch(err => { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load cover duties."); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [load, refreshVersion]);
    async function action(work) {
        if (busy) return;
        setBusy(true); setError(""); setMessage("");
        try { const response = await work(); setMessage(response.data.message); await load(); window.dispatchEvent(new Event("staff-notifications-changed")); }
        catch (err) { setError(err.response?.data?.message || "Unable to save cover duty."); }
        finally { setBusy(false); }
    }
    const selectedDay = day || data?.date || "";
    const assignments = compact ? data?.assignments?.filter(item => !item.expired) : data?.assignments;
    return <section className="my-6 space-y-4 rounded-2xl border border-slate-200 bg-white p-6">
        <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold">{manage ? "Arrange teacher cover" : "My leave cover duties"}</h2><button disabled={busy || loading} className={control} onClick={() => action(async () => ({ data: { message: "Cover duties refreshed." } }))}>Refresh</button></div>
        <p className="text-sm text-slate-500">{manage ? "Choose a date to see classes affected by approved leave. Assign each lesson or attendance duty to an available teacher. Timetable clashes and the replacement’s approved leave are checked before saving." : "These duties replace a teacher on approved leave. Attendance access is available only on the assigned date; normal attendance deadlines still apply. Teaching cover does not grant marks access."}</p>
        {manage && <label className="block text-sm">Cover date<input type="date" className={`${control} mt-2 block`} min={data?.today} value={selectedDay} disabled={busy} onChange={event => { setDay(event.target.value); setPicked({}); setRemove(null); setChange(null); setLoading(true); setError(""); }} /></label>}
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}
        {message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-sm text-blue-700">{message}</p>}
        {loading ? <p role="status" className="text-sm">Loading cover duties…</p> : manage ? data?.leaves?.length ? data.leaves.map(leave => <article key={leave.leaveRequestId} className="space-y-3 rounded-xl border border-slate-200 p-4">
            <h3 className="font-semibold">{leave.fullName} <span className="text-sm font-normal text-slate-500">{leave.staffNumber} · Approved leave</span></h3>
            <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Class", "Duty", "Time", "Replacement"].map(title => <th className="p-3" key={title}>{title}</th>)}</tr></thead><tbody>{leave.slots.map(slot => {
                const key = `${leave.leaveRequestId}:${slot.sourceKind}:${slot.sourceId}`;
                const cover = leave.assignments.find(item => item.sourceKind === slot.sourceKind && item.sourceId === slot.sourceId);
                return <tr key={key} className="border-t border-slate-100"><td className="p-3">{slot.classLabel}</td><td className="p-3">{slot.duty}</td><td className="whitespace-nowrap p-3">{slot.startTime ? `${slot.startTime.slice(0, 5)}–${slot.endTime.slice(0, 5)}` : "Attendance duty"}</td><td className="p-3">{cover ? <div className="flex flex-wrap items-center gap-3"><span>{cover.fullName}</span><button disabled={busy} className="font-semibold text-blue-600" onClick={() => { setRemove(null); setChange({ id: cover.id, teacher: cover.fullName, oldStaffId: cover.staffId, absentStaffId: leave.staffId, leaveRequestId: leave.leaveRequestId, sourceKind: slot.sourceKind, sourceId: slot.sourceId, duty: slot.duty }); }}>Change teacher</button><button disabled={busy} className="font-semibold text-red-600" onClick={() => setRemove({ id: cover.id, teacher: cover.fullName, duty: slot.duty })}>Remove</button></div> : <div className="flex flex-wrap gap-2"><select aria-label={`Replacement for ${slot.classLabel} ${slot.duty}`} disabled={busy} className={control} value={picked[key] || ""} onChange={event => setPicked({ ...picked, [key]: event.target.value })}><option value="">Select teacher</option>{data.teachers.filter(teacher => teacher.id !== leave.staffId).map(teacher => <option key={teacher.id} value={teacher.id}>{teacher.staffNumber} — {teacher.fullName}</option>)}</select><button disabled={busy || !picked[key]} className={button} onClick={() => action(() => api.post("/leave-coverage", { leaveRequestId: leave.leaveRequestId, date: selectedDay, sourceKind: slot.sourceKind, sourceId: slot.sourceId, staffId: Number(picked[key]) }))}>Assign</button></div>}</td></tr>;
            })}</tbody></table></div>
        </article>) : <p className="text-sm text-slate-500">No class duties found for approved leave on this date.</p> : assignments?.length ? <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Date", "Class", "Duty", "Time", "Replacing", "Access"].map(title => <th key={title} className="p-3">{title}</th>)}</tr></thead><tbody>{assignments.map(item => <tr key={item.id} className="border-t border-slate-100"><td className="whitespace-nowrap p-3">{item.coverDate}</td><td className="p-3">{item.classLabel}</td><td className="p-3">{item.duty}</td><td className="whitespace-nowrap p-3">{item.startTime ? `${item.startTime.slice(0, 5)}–${item.endTime.slice(0, 5)}` : "Attendance"}</td><td className="p-3">{item.absentTeacher}</td><td className="p-3">{item.expired ? "Ended" : item.attendanceAccess ? <Link className="font-semibold text-blue-600" to="/teacher/attendance">Open attendance</Link> : item.sourceKind === "ClassTeacher" ? "Available on assigned date" : "Teaching duty"}</td></tr>)}</tbody></table></div> : <p className="text-sm text-slate-500">No {compact ? "upcoming " : ""}cover duties assigned.</p>}
        {remove && <div role="alertdialog" aria-label="Remove cover duty" className="space-y-3 rounded-xl bg-amber-50 p-4"><p className="text-sm">Remove {remove.teacher} from {remove.duty}? The teacher will be notified and any attendance access will end.</p><div className="flex gap-3"><button disabled={busy} className={button} onClick={() => action(async () => { const response = await api.delete(`/leave-coverage/${remove.id}`); setRemove(null); return response; })}>Confirm removal</button><button disabled={busy} className={control} onClick={() => setRemove(null)}>Cancel</button></div></div>}
        {change && <div role="alertdialog" aria-label="Change replacement teacher" className="space-y-3 rounded-xl bg-amber-50 p-4"><p className="text-sm">Change {change.teacher}’s {change.duty} cover to another teacher? The previous assignment stays in history. If the new teacher is unavailable, the current assignment stays in place.</p><select aria-label="New replacement teacher" className={control} disabled={busy} value={change.staffId || ""} onChange={event => setChange({ ...change, staffId: Number(event.target.value) })}><option value="">Select teacher</option>{data.teachers.filter(t => t.id !== change.oldStaffId && t.id !== change.absentStaffId).map(t => <option key={t.id} value={t.id}>{t.staffNumber} — {t.fullName}</option>)}</select><div className="flex gap-3"><button disabled={busy || !change.staffId} className={button} onClick={() => action(async () => { const response = await api.post("/leave-coverage", { leaveRequestId: change.leaveRequestId, date: selectedDay, sourceKind: change.sourceKind, sourceId: change.sourceId, staffId: change.staffId, replacesCoverageId: change.id }); setChange(null); return response; })}>Confirm change</button><button disabled={busy} className={control} onClick={() => setChange(null)}>Cancel</button></div></div>}
        {manage && <section className="space-y-3 border-t border-slate-200 pt-5"><h3 className="font-semibold">Coverage history</h3><p className="text-sm text-slate-500">Assignments, changes and removals are retained. Search by replacement or absent teacher’s staff number or name.</p><div className="flex flex-wrap gap-3"><input aria-label="Search coverage history" placeholder="Staff number or name" className={control} value={filters.search} onChange={e => setFilters({ ...filters, search: e.target.value, page: 1 })} /><label className="text-sm">From <input type="date" className={control} value={filters.from} onChange={e => setFilters({ ...filters, from: e.target.value, page: 1 })} /></label><label className="text-sm">To <input type="date" className={control} value={filters.to} onChange={e => setFilters({ ...filters, to: e.target.value, page: 1 })} /></label></div>{historyError ? <p role="alert" className="text-sm text-red-700">{historyError}</p> : <><div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Cover date", "Class / duty", "Replacement", "Absent teacher", "Status", "Assigned by / at", "Removed by / at"].map(title => <th key={title} className="p-3">{title}</th>)}</tr></thead><tbody>{history?.rows.map(row => <tr key={row.id} className="border-t border-slate-100"><td className="p-3">{row.coverDate}<br />{row.startTime && `${row.startTime.slice(0, 5)}–${row.endTime.slice(0, 5)}`}</td><td className="p-3">{row.classLabel}<br />{row.duty}</td><td className="p-3">{row.teacher}<br />{row.staffNumber}</td><td className="p-3">{row.absentTeacher}</td><td className="p-3">{row.status}{row.replacesCoverageId && <span className="block text-xs text-slate-500">Replaced assignment #{row.replacesCoverageId}</span>}</td><td className="p-3">{row.assignedBy}<br />{new Date(row.createdAt).toLocaleString()}</td><td className="p-3">{row.removedBy || (row.revokedAt ? "Not recorded" : "—")}<br />{row.revokedAt && new Date(row.revokedAt).toLocaleString()}</td></tr>)}</tbody></table></div>{history && !history.rows.length && <p className="text-sm text-slate-500">No coverage history matches these filters.</p>}<div className="flex items-center gap-3"><button className={control} disabled={filters.page <= 1} onClick={() => setFilters({ ...filters, page: filters.page - 1 })}>Previous</button><span className="text-sm">Page {filters.page} · {history?.total || 0} assignments</span><button className={control} disabled={!history || filters.page >= history.totalPages} onClick={() => setFilters({ ...filters, page: filters.page + 1 })}>Next</button></div></>}</section>}
    </section>;
}
