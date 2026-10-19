import { useCallback, useEffect, useState } from "react";
import { CalendarDays } from "lucide-react";
import api from "../../api/axios";
import { useAuth } from "../../context/AuthContext";
import { useSearchParams } from "react-router-dom";
import StaffLeaveHistory from "./StaffLeaveHistory";
import StaffLeaveCoverage from "./StaffLeaveCoverage";
import StaffLeaveCancellation from "./StaffLeaveCancellation";
import StaffLeavePolicy, { LeaveDayPreview, LeaveReviewBalance } from "./StaffLeavePolicy";

const types = [[1, "Casual"], [2, "Sick"], [3, "Emergency"], [4, "Annual"], [5, "Other"]];
const control = "mt-2 w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
const date = value => value?.slice(0, 10) || "—";
export default function StaffLeavePage() {
    const { user } = useAuth();
    const [searchParams] = useSearchParams();
    const leader = ["Admin", "Principal", "Deputy Principal"].some(role => user?.roles?.includes(role));
    const head = user?.roles?.includes("Section Head");
    const [tab, setTab] = useState(() => ["coverDuties", "cancellations"].includes(searchParams.get("tab")) ? searchParams.get("tab") : ["pending", "coverage"].includes(searchParams.get("tab")) && (leader || head) ? searchParams.get("tab") : "my");
    const [mine, setMine] = useState([]);
    const [pending, setPending] = useState([]);
    const [historyVersion, setHistoryVersion] = useState(0);
    const [form, setForm] = useState({ leaveType: 1, fromDate: "", toDate: "", reason: "" });
    const [remarks, setRemarks] = useState({});
    const [review, setReview] = useState(null);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const load = useCallback(async (signal) => {
        const response = await api.get("/staff-leave/my", { signal });
        if (signal?.aborted) return;
        setMine(response.data.requests || []);
        if (leader || head) {
            const response = await api.get("/staff-leave/pending", { signal });
            if (!signal?.aborted) setPending(response.data.requests || []);
        }
    }, [leader, head]);
    useEffect(() => {
        const controller = new AbortController();
        Promise.resolve().then(() => load(controller.signal)).catch(err => { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load leave requests."); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [load]);
    async function refresh() {
        setError(""); setLoading(true);
        try { await load(); } catch (err) { setError(err.response?.data?.message || "Unable to refresh leave requests."); }
        finally { setLoading(false); setHistoryVersion(value => value + 1); }
    }
    async function save(work) {
        if (busy) return;
        setBusy(true); setError(""); setMessage("");
        try { const response = await work(); setMessage(response.data.message); await load(); setHistoryVersion(value => value + 1); window.dispatchEvent(new Event("staff-notifications-changed")); }
        catch (err) { setError(err.response?.data?.message || "Unable to save this request."); }
        finally { setBusy(false); }
    }
    async function submit(event) {
        event.preventDefault();
        if (form.fromDate > form.toDate) { setError("The end date must be on or after the start date."); return; }
        await save(async () => { const result = await api.post("/staff-leave", { ...form, leaveType: Number(form.leaveType) }); setForm({ leaveType: 1, fromDate: "", toDate: "", reason: "" }); setTab("my"); return result; });
    }
    return <main className="space-y-6">
        <header className="flex flex-wrap items-start justify-between gap-4"><div><div className="flex items-center gap-3"><CalendarDays className="h-6 w-6 text-blue-600" /><h1 className="text-2xl font-bold">Staff Leave</h1></div><p className="mt-2 text-sm text-slate-500">Request leave and track decisions. All staff roles can apply, including school leaders.</p></div><button disabled={busy || loading} className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm" onClick={refresh}>Refresh</button></header>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</p>}
        {message && <p role="status" className="rounded-xl bg-blue-50 p-4 text-blue-700">{message}</p>}
        <nav aria-label="Leave categories" className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3">{[["my", "My requests"], ["balances", "Leave balances"], ["apply", "Request leave"], ["cancellations", "Withdraw / cancel leave"], ["coverDuties", "My cover duties"], ...((leader || head) ? [["pending", `${leader ? "Pending approvals" : "Section requests"} (${pending.length})`], ["history", "Leave history"], ["coverage", "Arrange cover"]] : [])].map(([id, title]) => <button key={id} disabled={busy} aria-current={tab === id ? "page" : undefined} className={`rounded-xl px-4 py-3 text-sm font-semibold ${tab === id ? "bg-blue-600 text-white" : "text-slate-600"}`} onClick={() => { setTab(id); setReview(null); }}>{title}</button>)}</nav>
        {tab === "cancellations" && <StaffLeaveCancellation mine={mine} save={save} busy={busy} version={historyVersion} />}
        {tab === "balances" && <StaffLeavePolicy refreshVersion={historyVersion} />}

        {tab === "apply" && <form onSubmit={submit} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6"><h2 className="text-lg font-semibold">Request leave</h2><p className="text-sm text-slate-500">An Admin, Principal or Deputy Principal will review your request. You cannot approve your own leave.</p><fieldset disabled={busy} className="space-y-4"><div className="grid gap-4 md:grid-cols-3"><label className="text-sm">Leave type<select className={control} value={form.leaveType} onChange={event => setForm({ ...form, leaveType: event.target.value })}>{types.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label><label className="text-sm">From date<input required type="date" className={control} value={form.fromDate} onChange={event => setForm({ ...form, fromDate: event.target.value })} /></label><label className="text-sm">To date<input required type="date" min={form.fromDate || undefined} className={control} value={form.toDate} onChange={event => setForm({ ...form, toDate: event.target.value })} /></label></div><label className="block text-sm">Reason<textarea required maxLength={1000} className={control} value={form.reason} onChange={event => setForm({ ...form, reason: event.target.value })} /></label></fieldset><LeaveDayPreview form={form} /><button disabled={busy || !form.reason.trim()} className={button}>{busy ? "Submitting…" : "Submit leave request"}</button></form>}
        {tab === "my" && <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6"><h2 className="text-lg font-semibold">My leave requests</h2>{loading ? <p role="status">Loading…</p> : mine.length ? <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Type", "From", "To", "Status", "Reason", "Review remarks"].map(title => <th className="p-3" key={title}>{title}</th>)}</tr></thead><tbody>{mine.map(item => <tr key={item.id} className="border-t border-slate-100"><td className="p-3">{item.leaveTypeName}</td><td className="whitespace-nowrap p-3">{date(item.fromDate)}</td><td className="whitespace-nowrap p-3">{date(item.toDate)}</td><td className="p-3"><span className={`rounded-full px-3 py-1 text-xs font-semibold ${item.statusName === "Approved" ? "bg-green-50 text-green-700" : item.statusName === "Rejected" ? "bg-red-50 text-red-700" : "bg-amber-50 text-amber-800"}`}>{item.statusName}</span></td><td className="whitespace-pre-wrap p-3">{item.reason}</td><td className="whitespace-pre-wrap p-3">{item.reviewRemarks || "—"}</td></tr>)}</tbody></table></div> : <p className="text-sm text-slate-500">No leave requests yet. Select Request leave to apply.</p>}</section>}
        {tab === "pending" && <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6"><h2 className="text-lg font-semibold">{leader ? "Requests awaiting approval" : "Your section’s pending leave"}</h2>{!leader && <p className="text-sm text-slate-500">Section Heads can view their section’s requests. Approval and rejection are handled by school leaders.</p>}{loading ? <p role="status">Loading…</p> : pending.length ? pending.map(item => <article key={item.id} className="space-y-3 rounded-xl border border-slate-200 p-4"><div className="flex flex-wrap justify-between gap-3"><h3 className="font-semibold">{item.staff.fullName} <span className="text-sm font-normal text-slate-500">{item.staff.staffNumber}</span></h3><p className="text-sm">{item.leaveTypeName} · {date(item.fromDate)} → {date(item.toDate)}</p></div><p className="whitespace-pre-wrap text-sm text-slate-600">{item.reason}</p><LeaveReviewBalance id={item.id} />{leader && item.canReview ? <><label className="block text-sm">Review remarks (optional)<textarea className={control} disabled={busy} maxLength={1000} value={remarks[item.id] || ""} onChange={event => setRemarks({ ...remarks, [item.id]: event.target.value })} /></label><div className="flex gap-3"><button className={button} disabled={busy} onClick={() => setReview({ item, approve: true })}>Approve</button><button className="rounded-xl border border-red-200 px-4 py-3 text-sm font-semibold text-red-700" disabled={busy} onClick={() => setReview({ item, approve: false })}>Reject</button></div></> : leader && <p className="text-sm text-slate-500">Your request must be reviewed by another school leader.</p>}</article>) : <p className="text-sm text-slate-500">No pending requests.</p>}</section>}
        {(tab === "coverage" && (leader || head) || tab === "coverDuties") && <StaffLeaveCoverage manage={tab === "coverage"} refreshVersion={historyVersion} />}
        {tab === "history" && (leader || head) && <StaffLeaveHistory sectionScoped={!leader} refreshVersion={historyVersion} />}
        {review && <section role="alertdialog" aria-label="Confirm leave review" className="space-y-4 rounded-2xl border border-blue-200 bg-blue-50 p-6"><h2 className="font-semibold">{review.approve ? "Approve" : "Reject"} leave for {review.item.staff.fullName}?</h2><p className="text-sm">{date(review.item.fromDate)} to {date(review.item.toDate)}. The staff member will receive a notification.{review.approve && " School leaders and relevant Section Heads will also be notified."}</p><div className="flex gap-3"><button className={button} disabled={busy} onClick={() => save(async () => { const result = await api.post(`/staff-leave/${review.item.id}/review`, { approve: review.approve, remarks: remarks[review.item.id] || null }); setReview(null); return result; })}>Confirm {review.approve ? "approval" : "rejection"}</button><button className="rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm" disabled={busy} onClick={() => setReview(null)}>Cancel</button></div></section>}
    </main>;
}
