import { useCallback, useEffect, useState } from "react";
import api from "../../api/axios";



const input = "mt-1 block rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";

export function LeaveDayPreview({ form }) {
    const [data, setData] = useState(null);
    const [error, setError] = useState("");
    useEffect(() => {
        const controller = new AbortController();
        if (!form.fromDate || !form.toDate || form.fromDate > form.toDate) return;
        const timer = setTimeout(() => api.get("/staff-leave/preview", { signal: controller.signal, params: { leaveType: form.leaveType, from: form.fromDate, to: form.toDate } })
            .then(r => { setData(r.data); setError(""); }).catch(err => { if (!controller.signal.aborted) { setData(null); setError(err.response?.data?.message || "Unable to calculate leave days."); } }), 250);
        return () => { clearTimeout(timer); controller.abort(); };
    }, [form.fromDate, form.toDate, form.leaveType]);
    if (!form.fromDate || !form.toDate || form.fromDate > form.toDate) return null;
    return <div className="rounded-xl bg-blue-50 p-3 text-sm">{error || (data ? <><strong>{data.workingDays} leave day(s)</strong> after excluding weekends and holidays.{data.years.map(y => <p key={y.year} className="mt-1">{y.year}: {y.workingDays} days · {y.allowance == null ? "Allowance not configured" : `${y.remaining} days remaining before this request`}{y.exceedsBalance && <strong className="ml-2 text-red-700">Exceeds remaining balance — discuss with a school leader.</strong>}{!y.calendarConfigured && <span className="block text-amber-800">Calendar not configured: Saturday/Sunday are excluded by default. Ask Admin to confirm the school calendar.</span>}</p>)}</> : "Calculating leave days…")}</div>;
}

export function LeaveReviewBalance({ id }) {
    const [data, setData] = useState(null);
    useEffect(() => {
        const controller = new AbortController();
        api.get(`/staff-leave/review-balance/${id}`, { signal: controller.signal }).then(r => setData(r.data)).catch(() => {});
        return () => controller.abort();
    }, [id]);
    return data ? <div className="rounded-xl bg-blue-50 p-3 text-sm"><strong>{data.workingDays} leave day(s)</strong> excluding weekends and school holidays.{data.years.map(y => <p key={y.year}>{y.year}: {y.allowance == null ? "Allowance not configured" : `${y.remaining} days remaining before this request`}{y.exceedsBalance && <strong className="ml-2 text-red-700">Exceeds remaining balance — check before approving.</strong>}{!y.calendarConfigured && <span className="block text-amber-800">School calendar not configured; Saturday/Sunday defaults apply.</span>}</p>)}</div> : null;
}

export default function StaffLeavePolicy({ manage = false, refreshVersion = 0 }) {
    const [year, setYear] = useState(new Date().getFullYear());
    const [staffId, setStaffId] = useState("");
    const [data, setData] = useState(null);
    const [allowance, setAllowance] = useState({ staffId: "", days: "" });
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [busy, setBusy] = useState(false);
    const load = useCallback(async signal => {
        const r = await api.get(manage ? "/staff-leave/settings" : "/staff-leave/balances", { signal, params: { year, staffId: staffId || undefined } });
        if (!signal?.aborted) { setData(r.data); setError(""); }
    }, [manage, year, staffId]);
    useEffect(() => {
        const controller = new AbortController();
        Promise.resolve().then(() => load(controller.signal)).catch(err => { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load leave allowances."); });
        return () => controller.abort();
    }, [load, refreshVersion]);
    async function save(e) {
        e.preventDefault(); setBusy(true); setError("");
        try { const r = await api.put("/staff-leave/settings/allowance", { year, staffId: allowance.staffId ? Number(allowance.staffId) : null, days: Number(allowance.days) }); setMessage(r.data.message); await load(); }
        catch (err) { setError(err.response?.data?.message || "Unable to save allowance."); }
        finally { setBusy(false); }
    }
    return <section className="space-y-5 rounded-2xl border border-slate-200 bg-white p-6"><h1 className="text-2xl font-bold">{manage ? "Staff Leave Allowance" : "Leave balances"}</h1>
        <p className="text-sm text-slate-500">One shared yearly allowance covers all leave types. For example: 40 days minus 5 Sick days and 3 Casual days leaves 32 days. Weekends and school holidays do not use this allowance. Pending requests are shown separately.</p>
        <label className="block text-sm">Calendar year<input className={input} type="number" min="1900" max="2100" value={year} disabled={busy} onChange={e => { setYear(Number(e.target.value)); setData(null); }} /></label>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-blue-700">{message}</p>}
        {!data ? !error && <p>Loading…</p> : manage ? <><form onSubmit={save} className="space-y-4"><div className="flex flex-wrap gap-3"><label className="text-sm">Applies to<select className={input} value={allowance.staffId} onChange={e => setAllowance({ ...allowance, staffId: e.target.value })}><option value="">All staff (default)</option>{data.staff.map(s => <option key={s.id} value={s.id}>{s.staffNumber} — {s.fullName}</option>)}</select></label><label className="text-sm">Total leave days for the year<input required type="number" min="0" max="366" step="1" className={input} value={allowance.days} onChange={e => setAllowance({ ...allowance, days: e.target.value })} /></label></div><p className="text-sm text-slate-500">Individual totals replace the school default. Previous per-type limits are retained in the database but no longer used; enter the shared total here.</p><button disabled={busy} className={button}>Save yearly allowance</button></form><div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Staff", "Shared yearly total", "Action"].map(t => <th className="p-3" key={t}>{t}</th>)}</tr></thead><tbody>{data.allowances.map(a => <tr key={a.id} className="border-t border-slate-100"><td className="p-3">{a.staffId ? data.staff.find(s => s.id === a.staffId)?.fullName || `Staff #${a.staffId}` : "All staff (default)"}</td><td className="p-3">{a.days} days</td><td className="p-3"><button className="font-semibold text-blue-600" onClick={() => setAllowance({ staffId: a.staffId || "", days: a.days })}>Edit</button></td></tr>)}</tbody></table>{!data.allowances.length && <p className="p-3 text-sm text-slate-500">No shared allowance configured for this year.</p>}</div></> : <><label className="block text-sm">Staff<select className={input} value={staffId || data.staff.id} onChange={e => setStaffId(e.target.value)}>{data.staffOptions.map(s => <option key={s.id} value={s.id}>{s.staffNumber} — {s.fullName}</option>)}</select></label><div className="flex flex-wrap gap-3">{[["Yearly allowance", data.total.allowance ?? "Not set"], ["Used — all types", data.total.used], ["Pending", data.total.pending], ["Remaining", data.total.remaining ?? "—"]].map(([label, value]) => <div key={label} className="rounded-xl border border-slate-200 p-4"><p className="text-sm text-slate-500">{label}</p><strong className="text-xl">{value}</strong></div>)}</div>{!data.total.calendarConfigured && <p className="text-sm text-amber-800">School calendar not configured; Saturday/Sunday defaults apply. Ask Admin to confirm it.</p>}<h2 className="font-semibold">Usage by type — all deducted from the same total</h2><table className="w-full text-left text-sm"><thead><tr>{["Leave type", "Approved days used", "Pending days"].map(t => <th className="p-3" key={t}>{t}</th>)}</tr></thead><tbody>{data.rows.map(r => <tr key={r.leaveType} className="border-t border-slate-100"><td className="p-3">{r.typeName}</td><td className="p-3">{r.used}</td><td className="p-3">{r.pending}</td></tr>)}</tbody></table></>}
    </section>;
}
