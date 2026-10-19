import { useEffect, useState } from "react";
import api from "../../api/axios";

const control = "mt-2 w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
const date = value => value?.slice(0, 10) || "—";
const timestamp = value => value ? new Date(/Z$|[+-]\d{2}:\d{2}$/.test(value) ? value : `${value}Z`).toLocaleString() : "—";
function period(item, today) {
    if (item.status !== "Approved") return "—";
    if (date(item.fromDate) > today) return "Scheduled";
    return date(item.toDate) < today ? "Approved dates ended" : "On approved leave";
}
export default function StaffLeaveHistory({ sectionScoped, refreshVersion }) {
    const [filters, setFilters] = useState({ search: "", status: "", fromDate: "", toDate: "" });
    const [page, setPage] = useState(1);
    const [result, setResult] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const invalidDates = filters.fromDate && filters.toDate && filters.fromDate > filters.toDate;
    useEffect(() => {
        const controller = new AbortController();
        const timer = window.setTimeout(async () => {
            setLoading(true); setError("");
            if (invalidDates) { setLoading(false); return; }
            try {
                const response = await api.get("/staff-leave/history", { params: { ...Object.fromEntries(Object.entries(filters).filter(([, value]) => value)), page, pageSize: 25 }, signal: controller.signal });
                if (!controller.signal.aborted) setResult(response.data);
            } catch (err) { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load leave history."); }
            finally { if (!controller.signal.aborted) setLoading(false); }
        }, 200);
        return () => { window.clearTimeout(timer); controller.abort(); };
    }, [filters, page, invalidDates, refreshVersion]);
    const change = (key, value) => { setFilters({ ...filters, [key]: value }); setPage(1); };
    return <section className="space-y-5 rounded-2xl border border-slate-200 bg-white p-6">
        <div><h2 className="text-lg font-semibold">{sectionScoped ? "Your section’s leave history" : "School staff leave history"}</h2><p className="mt-2 text-sm text-slate-500">View all request statuses, leave dates, reasons and review decisions.{sectionScoped && " Records are limited to your assigned sections and academic years."}</p></div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <label className="text-sm">Staff name or number<input maxLength={100} className={control} value={filters.search} onChange={event => change("search", event.target.value)} placeholder="Search staff" /></label>
            <label className="text-sm">Status<select className={control} value={filters.status} onChange={event => change("status", event.target.value)}><option value="">All statuses</option>{[[1, "Pending"], [2, "Approved"], [3, "Rejected"], [4, "Cancelled"]].map(([id, title]) => <option key={id} value={id}>{title}</option>)}</select></label>
            <label className="text-sm">Leave overlaps from<input type="date" className={control} value={filters.fromDate} onChange={event => change("fromDate", event.target.value)} /></label>
            <label className="text-sm">Leave overlaps to<input type="date" className={control} min={filters.fromDate || undefined} value={filters.toDate} onChange={event => change("toDate", event.target.value)} /></label>
        </div>
        <button className="text-sm font-semibold text-blue-600" onClick={() => { setFilters({ search: "", status: "", fromDate: "", toDate: "" }); setPage(1); }}>Clear filters</button>
        {invalidDates ? <p role="alert" className="text-sm text-red-700">The end date must be on or after the start date.</p> : error ? <p role="alert" className="text-sm text-red-700">{error}</p> : loading ? <p role="status" className="text-sm">Loading leave history…</p> : result && <>
            <dl className="grid grid-cols-2 gap-3 border-y border-slate-100 py-4 md:grid-cols-4">{["pending", "approved", "rejected", "cancelled"].map(status => <div key={status}><dt className="text-sm capitalize text-slate-500">{status}</dt><dd className="mt-1 text-xl font-semibold">{result.summary?.[status] || 0}</dd></div>)}</dl>
            <p className="text-xs text-slate-500">Leave days exclude the school’s configured weekends and holidays. Calendar days show the full date span.</p>
            {result.requests.length ? <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Staff", "Leave type", "Dates / days", "Status", "Leave period", "Reviewed by", "Details"].map(title => <th key={title} className="whitespace-nowrap p-3">{title}</th>)}</tr></thead><tbody>{result.requests.map(item => <tr key={item.id} className="border-t border-slate-100 align-top">
                <td className="p-3"><p className="font-semibold">{item.staff.fullName}</p><p className="mt-1 text-xs text-slate-500">{item.staff.staffNumber}{item.staff.designation ? ` · ${item.staff.designation}` : ""}</p></td>
                <td className="p-3">{item.leaveType}</td><td className="whitespace-nowrap p-3">{date(item.fromDate)} → {date(item.toDate)}<p className="mt-1 text-xs text-slate-500">{result.workingDays?.[item.id] ?? "—"} leave day(s) · {item.calendarDays} calendar day(s)</p></td>
                <td className="p-3"><span className={`rounded-full px-3 py-1 text-xs font-semibold ${item.status === "Approved" ? "bg-green-50 text-green-700" : item.status === "Rejected" ? "bg-red-50 text-red-700" : item.status === "Cancelled" ? "bg-slate-100 text-slate-600" : "bg-amber-50 text-amber-800"}`}>{item.status}</span></td>
                <td className="p-3">{period(item, result.today)}</td><td className="p-3">{item.reviewedBy || "—"}{item.reviewedAt && <p className="mt-1 text-xs text-slate-500">{timestamp(item.reviewedAt)}</p>}</td>
                <td className="min-w-48 p-3"><details><summary className="cursor-pointer font-semibold text-blue-600">View details</summary><dl className="mt-3 space-y-2 text-xs"><div><dt className="font-semibold">Reason</dt><dd className="whitespace-pre-wrap">{item.reason}</dd></div><div><dt className="font-semibold">Requested</dt><dd>{timestamp(item.requestedAt)}</dd></div><div><dt className="font-semibold">Review remarks</dt><dd className="whitespace-pre-wrap">{item.reviewRemarks || "—"}</dd></div>{item.cancelledAt && <div><dt className="font-semibold">Cancelled</dt><dd>{timestamp(item.cancelledAt)}</dd></div>}</dl></details></td>
            </tr>)}</tbody></table></div> : <p className="text-sm text-slate-500">No leave requests match these filters.</p>}
            <div className="flex flex-wrap items-center justify-between gap-3 text-sm"><p>{result.total} request(s) · Page {result.page} of {result.totalPages}</p><div className="flex gap-3"><button className="rounded-xl border border-slate-200 px-4 py-2 disabled:opacity-50" disabled={result.page <= 1} onClick={() => setPage(result.page - 1)}>Previous</button><button className="rounded-xl border border-slate-200 px-4 py-2 disabled:opacity-50" disabled={result.page >= result.totalPages} onClick={() => setPage(result.page + 1)}>Next</button></div></div>
        </>}
    </section>;
}
