import { useCallback, useEffect, useRef, useState } from "react";
import api from "../../api/axios";

const control = "mt-1 block w-full rounded-xl border border-slate-300 bg-white p-3 text-sm disabled:bg-slate-50";
const button = "min-h-11 rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-50";
export default function ExtraLeaveAllowance({ year: selectedYear, onChanged, scope = "all", refreshVersion = 0 }) {
    const [ownYear, setOwnYear] = useState(new Date().getFullYear());
    const year = selectedYear ?? ownYear;
    const [data, setData] = useState(null);
    const [filters, setFilters] = useState({ status: "all", search: "", page: 1 });
    const [form, setForm] = useState({ staffId: "", extraDays: "", reason: "" });
    const [review, setReview] = useState(null);
    const [remarks, setRemarks] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [reload, setReload] = useState(0);
    const pending = useRef(false);
    const load = useCallback(async signal => {
        const { data: result } = await api.get("/staff-leave/extra-allowances", { signal, params: { year, scope, ...filters } });
        if (!signal?.aborted) { setData(result); setError(""); }
    }, [year, filters, scope]);
    useEffect(() => {
        const controller = new AbortController();
        const timer = setTimeout(() => load(controller.signal).catch(e => {
            if (!controller.signal.aborted) setError(e.response?.data?.message || "Unable to load extra leave allowances.");
        }), 200);
        return () => { clearTimeout(timer); controller.abort(); };
    }, [load, reload, refreshVersion]);
    async function mutate(work) {
        if (pending.current) return;
        pending.current = true; setBusy(true); setError(""); setMessage("");
        try {
            const { data: result } = await work(); setMessage(result.message); setReview(null); setRemarks("");
            setForm({ staffId: "", extraDays: "", reason: "" }); setReload(value => value + 1); await onChanged?.();
            window.dispatchEvent(new Event("staff-notifications-changed"));
        } catch (e) { setError(e.response?.data?.message || "Unable to save the extra leave allowance request."); }
        finally { pending.current = false; setBusy(false); }
    }
    function filter(key, value) { setFilters(current => ({ ...current, [key]: value, page: key === "page" ? value : 1 })); setData(null); setReview(null); }
    return <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5" aria-label="Extra leave days">
        <h2 className="text-xl font-bold">{scope === "mine" ? "My extra leave days" : "Extra leave days"}</h2>
        <p className="text-sm text-slate-500">Staff can request extra days with a reason, such as government-approved maternity leave. Admin can request for staff; Principal can approve requests or grant days directly. Pending or rejected requests do not change the balance. Actual leave dates must still be requested separately.</p>
        {selectedYear == null && <label className="block max-w-xs text-sm">Calendar year<input type="number" min="1900" max="2100" className={control} disabled={busy} value={year} onChange={e => { setOwnYear(Number(e.target.value)); setData(null); setReview(null); setFilters(current => ({ ...current, page: 1 })); }} /></label>}
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}
        {message && <p role="status" className="rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">{message}</p>}
        {data?.year === year && data.canRequest && <form onSubmit={e => {
            e.preventDefault(); const grant = e.nativeEvent.submitter?.value === "grant"; mutate(() => api.post(grant ? "/staff-leave/extra-allowances/grant" : "/staff-leave/extra-allowances", { year, staffId: Number(form.staffId || (!data.canSelectStaff ? data.currentStaffId : 0)), extraDays: Number(form.extraDays), reason: form.reason }));
        }} className="space-y-4 border-b border-slate-200 pb-5">
            <div className="grid gap-4 sm:grid-cols-2"><label className="text-sm font-semibold">Staff member<span className="required-marker" aria-hidden="true"> *</span>
                <select required disabled={busy || !data.canSelectStaff} className={control} value={data.canSelectStaff ? form.staffId : data.currentStaffId} onChange={e => setForm({ ...form, staffId: e.target.value })}><option value="">Select staff member</option>{data.staffOptions.map(s => <option value={s.id} key={s.id}>{s.staffNumber} — {s.fullName}</option>)}</select>
            </label><label className="text-sm font-semibold">Additional leave days<span className="required-marker" aria-hidden="true"> *</span>
                <input required type="number" min="1" max="366" step="1" disabled={busy} className={control} value={form.extraDays} onChange={e => setForm({ ...form, extraDays: e.target.value })} />
            </label></div>
            <label className="block text-sm font-semibold">Reason for extra days<span className="required-marker" aria-hidden="true"> *</span><textarea required maxLength={1000} rows={3} disabled={busy} className={control} value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })} /></label>
            <p className="text-sm text-slate-500">Enter the additional days, not a replacement total. For example: 40 yearly days + 5 approved extra days = 45 days.</p>
            <div className="flex flex-wrap gap-3"><button className={button} disabled={busy} name="action" value="request">{busy ? "Saving…" : "Request Principal approval"}</button>{data.canGrant && <button className={button} disabled={busy} name="action" value="grant">Grant extra days directly</button>}</div>
        </form>}
        <h3 className="font-semibold">Requests and history · {year}</h3>
        <div className="flex flex-wrap gap-3"><label className="text-sm">Status<select className={control} disabled={busy} value={filters.status} onChange={e => filter("status", e.target.value)}>{["all", "Pending", "Approved", "Rejected"].map(value => <option key={value} value={value}>{value === "all" ? "All statuses" : value}</option>)}</select></label>
            <label className="text-sm">Search staff<input type="search" maxLength={100} disabled={busy} className={control} placeholder="Staff number or name" value={filters.search} onChange={e => filter("search", e.target.value)} /></label>
            <button type="button" disabled={busy} onClick={() => setReload(value => value + 1)} className="self-end rounded-xl border border-slate-300 px-4 py-3 text-sm">Refresh</button>
        </div>
        {data?.year !== year ? !error && <p role="status">Loading extra leave requests…</p> : <>
            {data.rows.map(item => <article key={item.id} className="space-y-3 rounded-xl border border-slate-200 p-4">
                <div className="flex flex-wrap justify-between gap-2"><h4 className="font-semibold">{item.staff.staffNumber} — {item.staff.fullName}</h4><span className={`rounded-full px-3 py-1 text-xs font-semibold ${item.status === "Approved" ? "bg-emerald-50 text-emerald-800" : item.status === "Rejected" ? "bg-red-50 text-red-700" : "bg-amber-50 text-amber-800"}`}>{item.status}</span></div>
                <p className="text-sm font-semibold">{item.status === "Approved" ? item.approvedDays ?? item.extraDays : item.extraDays} extra day(s) · {item.year}{item.approvedDays != null && item.approvedDays !== item.extraDays && <span className="ml-2 font-normal">({item.extraDays} requested)</span>}</p><p className="whitespace-pre-wrap break-words text-sm text-slate-600">{item.reason}</p>
                <p className="text-xs text-slate-500">Requested by {item.requestedBy} · {new Date(item.requestedAt).toLocaleString()}</p>
                {item.reviewedAt && <div className="text-sm text-slate-600"><p>{item.status} by {item.reviewedBy} · {new Date(item.reviewedAt).toLocaleString()}</p>{item.reviewRemarks && <p className="mt-1 whitespace-pre-wrap break-words">Review: {item.reviewRemarks}</p>}</div>}
                {item.canReview && (review?.id === item.id ? <form className="space-y-3" onSubmit={e => { e.preventDefault(); mutate(() => api.post(`/staff-leave/extra-allowances/${item.id}/review`, { approve: review.approve, remarks, approvedDays: review.approve ? Number(review.days) : null })); }}>
                    <p className="text-sm font-semibold">{review.approve ? "Approve additional leave days" : "Reject this request?"}</p>
                    {review.approve && <label className="block text-sm">Approved extra days<span className="required-marker" aria-hidden="true"> *</span><input type="number" required min="1" max="366" step="1" className={control} disabled={busy} value={review.days} onChange={e => setReview({ ...review, days: e.target.value })} /></label>}<label className="block text-sm">{review.approve ? "Approval reason" : "Reason for rejection"}<span className="required-marker" aria-hidden="true"> *</span><textarea required maxLength={1000} disabled={busy} className={control} value={remarks} onChange={e => setRemarks(e.target.value)} /></label>
                    <div className="flex flex-wrap gap-3"><button disabled={busy} className={button}>{busy ? "Saving…" : review.approve ? "Confirm approval" : "Confirm rejection"}</button><button type="button" disabled={busy} className="rounded-xl border border-slate-300 px-4 py-3 text-sm" onClick={() => setReview(null)}>Cancel</button></div>
                </form> : <div className="flex flex-wrap gap-3"><button disabled={busy} onClick={() => { setReview({ id: item.id, approve: true, days: item.extraDays }); setRemarks(""); }} className={button}>Approve</button><button disabled={busy} onClick={() => { setReview({ id: item.id, approve: false }); setRemarks(""); }} className="rounded-xl border border-red-300 px-4 py-3 text-sm font-semibold text-red-700">Reject</button></div>)}
                {data.canApprove && item.status === "Pending" && !item.canReview && <p className="text-sm text-slate-500">Another Principal must review requests made by you or for your own allowance.</p>}
            </article>)}
            {!data.rows.length && <p className="text-sm text-slate-500">No matching requests.</p>}
            <div className="flex flex-wrap items-center gap-3"><button type="button" disabled={busy || data.page <= 1} className="rounded-xl border border-slate-300 px-4 py-2 text-sm disabled:opacity-50" onClick={() => filter("page", data.page - 1)}>Previous</button><p className="text-sm">Page {data.page} of {data.totalPages} · {data.total} request(s)</p><button type="button" disabled={busy || data.page >= data.totalPages} className="rounded-xl border border-slate-300 px-4 py-2 text-sm disabled:opacity-50" onClick={() => filter("page", data.page + 1)}>Next</button></div>
        </>}
    </section>;
}
