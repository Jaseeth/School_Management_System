import { useEffect, useState } from "react";
import api from "../../api/axios";

const control = "rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
const date = value => value?.slice(0, 10) || "";
export default function StaffLeaveCancellation({ mine, save, busy, version }) {
    const [data, setData] = useState(null);
    const [error, setError] = useState("");
    const [selected, setSelected] = useState("");
    const [effective, setEffective] = useState("");
    const [reason, setReason] = useState("");
    const [remarks, setRemarks] = useState({});
    const [confirm, setConfirm] = useState(null);
    const [filters, setFilters] = useState({ from: "", to: "", search: "", status: "", page: 1 });
    const [loading, setLoading] = useState(true);
    useEffect(() => {
        const controller = new AbortController();
        const timer = setTimeout(() => {
            setLoading(true);
            const params = { ...filters, from: filters.from || undefined, to: filters.to || undefined, status: filters.status || undefined };
            api.get("/staff-leave/cancellations", { signal: controller.signal, params }).then(r => { setData(r.data); setError(""); })
                .catch(err => { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load cancellations."); })
                .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        }, 250);
        return () => { clearTimeout(timer); controller.abort(); };
    }, [version, filters]);
    const today = date(data?.today);
    const eligible = mine.filter(item => item.statusName === "Pending" || item.statusName === "Approved" && date(item.toDate) >= today);
    const leave = eligible.find(item => item.id === Number(selected));
    const pendingCancellation = data?.pendingLeaveIds.includes(leave?.id);
    async function perform() {
        await save(async () => {
            const response = confirm.kind === "withdraw" ? await api.post(`/staff-leave/${leave.id}/withdraw`) : confirm.kind === "request" ? await api.post(`/staff-leave/${leave.id}/cancellation`, { effectiveFrom: effective, reason }) : await api.post(`/staff-leave/cancellations/${confirm.id}/review`, { approve: confirm.approve, remarks: remarks[confirm.id] || "" });
            setConfirm(null); setSelected(""); setReason(""); return response;
        });
    }
    return <section className="space-y-5 rounded-2xl border border-slate-200 bg-white p-6">
        <h2 className="text-lg font-semibold">Withdraw or cancel leave</h2>
        <p className="text-sm text-slate-500">Pending requests can be withdrawn immediately. Approved leave requires another school leader’s approval. For an early return, choose your return date: earlier leave days stay recorded. Coverage remains until cancellation is approved.</p>
        {error && <p role="alert" className="text-red-700">{error}</p>}
        <label className="block text-sm">Your leave request<select disabled={busy || !data} className={`${control} mt-2 block w-full`} value={selected} onChange={e => { setSelected(e.target.value); setConfirm(null); setEffective(""); }}><option value="">Select leave request</option>{eligible.map(item => <option key={item.id} value={item.id}>{item.leaveTypeName} · {date(item.fromDate)} to {date(item.toDate)} · {item.statusName}</option>)}</select></label>
        {leave?.statusName === "Pending" && <button disabled={busy} className={button} onClick={() => setConfirm({ kind: "withdraw" })}>Withdraw pending request</button>}
        {leave?.statusName === "Approved" && (pendingCancellation ? <p className="text-sm">Cancellation is already awaiting review.</p> : <form className="space-y-3" onSubmit={e => { e.preventDefault(); setConfirm({ kind: "request" }); }}><label className="block text-sm">Cancel leave from / return date<input required type="date" min={date(leave.fromDate) > today ? date(leave.fromDate) : today} max={date(leave.toDate)} disabled={busy} value={effective} onChange={e => setEffective(e.target.value)} className={`${control} ml-3`} /></label><label className="block text-sm">Reason<textarea required maxLength={1000} disabled={busy} value={reason} onChange={e => setReason(e.target.value)} className={`${control} mt-2 block w-full`} /></label><button disabled={busy || !reason.trim() || !effective} className={button}>Request cancellation</button></form>)}
        <h3 className="border-t border-slate-200 pt-5 font-semibold">Cancellation requests and history</h3>
        <p className="text-sm text-slate-500">Showing the last six months by default. Choose an earlier requested-date range to view older records. Leaders can review others’ requests; section heads can view requests within their section.</p>
        <div className="flex flex-wrap gap-3"><label className="text-sm">Requested from<input type="date" className={`${control} mt-1 block`} value={filters.from || data?.from || ""} onChange={e => setFilters({ ...filters, from: e.target.value, page: 1 })} /></label><label className="text-sm">Requested to<input type="date" className={`${control} mt-1 block`} value={filters.to || data?.to || ""} onChange={e => setFilters({ ...filters, to: e.target.value, page: 1 })} /></label><label className="text-sm">Staff number or name<input type="search" className={`${control} mt-1 block`} value={filters.search} onChange={e => setFilters({ ...filters, search: e.target.value, page: 1 })} /></label><label className="text-sm">Status<select className={`${control} mt-1 block`} value={filters.status} onChange={e => setFilters({ ...filters, status: e.target.value, page: 1 })}><option value="">All</option>{["Pending", "Approved", "Rejected"].map(s => <option key={s}>{s}</option>)}</select></label><button disabled={busy || loading} className={control} onClick={() => setFilters({ from: "", to: "", search: "", status: "", page: 1 })}>Last six months</button></div>
        {loading ? <p role="status">Loading…</p> : error ? null : !data?.rows.length ? <p className="text-sm text-slate-500">No cancellation requests match these filters.</p> : data.rows.map(row => <article key={row.id} className="space-y-2 rounded-xl border border-slate-200 p-4"><h4 className="font-semibold">{row.staff} · {row.staffNumber} · {row.status}</h4><p className="text-sm">Cancel from {date(row.effectiveFrom)} through {date(row.originalToDate)}</p><p className="whitespace-pre-wrap text-sm">{row.reason}</p><p className="text-sm text-slate-500">Requested: {new Date(row.requestedAt).toLocaleString()}{row.reviewedAt && ` · Reviewed by ${row.reviewedBy}: ${new Date(row.reviewedAt).toLocaleString()}`}</p>{row.remarks && <p className="text-sm">Review remarks: {row.remarks}</p>}{row.canReview && <><label className="block text-sm">Review remarks<textarea maxLength={1000} disabled={busy} value={remarks[row.id] || ""} onChange={e => setRemarks({ ...remarks, [row.id]: e.target.value })} className={`${control} mt-2 block w-full`} /></label><div className="flex gap-3"><button disabled={busy} className={button} onClick={() => setConfirm({ kind: "review", id: row.id, approve: true })}>Approve cancellation</button><button disabled={busy} className={control} onClick={() => setConfirm({ kind: "review", id: row.id, approve: false })}>Reject cancellation</button></div></>}</article>)}
        {data && !error && <div className="flex flex-wrap items-center gap-3"><button disabled={busy || loading || data.page <= 1} className={control} onClick={() => setFilters({ ...filters, page: data.page - 1 })}>Previous</button><span className="text-sm">Page {data.page} of {Math.max(1, data.totalPages)} · {data.total} requests</span><button disabled={busy || loading || data.page >= data.totalPages} className={control} onClick={() => setFilters({ ...filters, page: data.page + 1 })}>Next</button></div>}
        {confirm && <div role="alertdialog" aria-label="Confirm leave cancellation action" className="space-y-3 rounded-xl bg-amber-50 p-4"><p className="text-sm">{confirm.kind === "withdraw" ? "Withdraw this pending leave request now?" : confirm.kind === "request" ? "Submit this cancellation for school leader approval? Leave remains approved until the decision." : confirm.approve ? "Approve cancellation? Affected cover duties will end and staff will be notified. Earlier leave days stay recorded." : "Reject cancellation? Existing approved leave and coverage will remain."}</p><div className="flex gap-3"><button disabled={busy} className={button} onClick={perform}>Confirm</button><button disabled={busy} className={control} onClick={() => setConfirm(null)}>Back</button></div></div>}
    </section>;
}
