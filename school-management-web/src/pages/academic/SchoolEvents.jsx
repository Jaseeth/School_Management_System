import { useEffect, useState } from "react";
import api from "../../api/axios";

const input = "mt-1 w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
const todayFor = year => { const now = new Date(); return now.getFullYear() === year ? `${year}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}` : `${year}-01-01`; };
const blank = year => ({ title: "", description: "", location: "", fromDate: todayFor(year), throughDate: todayFor(year), startTime: "", endTime: "", audience: "School", audienceId: "", includeStaff: true, includeStudents: true, includeParents: true, isHoliday: false, revision: 0 });
export function EventList({ events, canManage = false, onEdit, onCancel }) {
    return <div className="space-y-3">{events.length === 0 && <p className="text-sm text-slate-500">No events for this period.</p>}{events.map(e => <article key={e.id} className={`space-y-2 rounded-xl border p-4 ${e.isCancelled ? "border-slate-200 bg-slate-50" : "border-blue-200 bg-white"}`}>
        <div className="flex flex-wrap justify-between gap-2"><h3 className="font-semibold">{e.title}</h3><span className="text-xs font-semibold text-slate-600">{e.isCancelled ? "Cancelled" : e.isHoliday ? "School holiday" : "School activity"}</span></div>
        <p className="text-sm text-slate-600">{e.fromDate}{e.throughDate !== e.fromDate ? ` to ${e.throughDate}` : ""} · {e.startTime ? `${e.startTime.slice(0, 5)}–${e.endTime.slice(0, 5)}` : "All day"}{e.location ? ` · ${e.location}` : ""}</p>
        {e.description && <p className="whitespace-pre-wrap text-sm">{e.description}</p>}
        {e.isCancelled && <p className="text-sm text-red-700">Reason: {e.cancellationReason}</p>}
        {canManage && !e.isCancelled && <div className="flex gap-4 text-sm"><button className="font-semibold text-blue-700" onClick={() => onEdit(e)}>Edit event</button><button className="text-red-700" onClick={() => onCancel(e)}>Cancel event</button></div>}
    </article>)}</div>;
}
export default function SchoolEvents({ year, events, canManage, onChanged }) {
    const [options, setOptions] = useState(null);
    const [form, setForm] = useState(null);
    const [cancel, setCancel] = useState(null);
    const [reason, setReason] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    useEffect(() => {
        if (!canManage) return;
        const controller = new AbortController();
        api.get("/school-events/options", { signal: controller.signal }).then(r => setOptions(r.data)).catch(e => { if (!controller.signal.aborted) setError(e.response?.data?.message || "Unable to load event audiences."); });
        return () => controller.abort();
    }, [canManage]);
    const change = (key, value) => setForm(f => ({ ...f, [key]: value, ...(key === "fromDate" && (f.throughDate < value || f.throughDate === f.fromDate) ? { throughDate: value } : {}) }));
    async function save(event) {
        event.preventDefault(); setBusy(true); setError(""); setMessage("");
        try {
            const payload = { ...form, audienceId: form.audience === "School" ? null : Number(form.audienceId), startTime: form.startTime || null, endTime: form.endTime || null };
            const r = form.id ? await api.put(`/school-events/${form.id}`, payload) : await api.post("/school-events", payload);
            setForm(null); setMessage(r.data.message); await onChanged();
        } catch (e) { setError(e.response?.data?.message || "Unable to save event."); } finally { setBusy(false); }
    }
    async function cancelEvent(event) {
        event.preventDefault(); setBusy(true); setError(""); setMessage("");
        try {
            const r = await api.post(`/school-events/${cancel.id}/cancel`, { revision: cancel.revision, reason });
            setCancel(null); setMessage(r.data.message); await onChanged();
        } catch (e) { setError(e.response?.data?.message || "Unable to cancel event."); } finally { setBusy(false); }
    }
    return <section className="space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="text-xl font-bold">School Events · {year}</h2><p className="mt-1 text-sm text-slate-500">Activities, meetings and ceremonies shared with you. Times use the school's local time.</p></div>{canManage && <button disabled={busy || !options} className={button} onClick={() => { setForm(blank(year)); setCancel(null); setError(""); setMessage(""); }}>Create event</button>}</div>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-blue-700">{message}</p>}
        {form && <form onSubmit={save} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5"><h3 className="font-semibold">{form.id ? "Edit event" : "Create event"}</h3><fieldset disabled={busy} className="space-y-4">
            <label className="block text-sm">Event title<span className="required-marker" aria-hidden="true"> *</span><input required maxLength={200} className={input} value={form.title} onChange={e => change("title", e.target.value)} /></label>
            <label className="block text-sm">Description<textarea maxLength={4000} rows={3} className={input} value={form.description} onChange={e => change("description", e.target.value)} /></label>
            <label className="block text-sm">Location<input maxLength={300} className={input} value={form.location} onChange={e => change("location", e.target.value)} /></label>
            <div className="grid gap-4 sm:grid-cols-2">{[["fromDate", "From date"], ["throughDate", "Through date"]].map(([key, label]) => <label className="text-sm" key={key}>{label}<span className="required-marker" aria-hidden="true"> *</span><input required type="date" min={key === "throughDate" ? form.fromDate : `${year}-01-01`} max={`${year}-12-31`} className={input} value={form[key]} onChange={e => change(key, e.target.value)} /></label>)}</div>
            <div className="grid gap-4 sm:grid-cols-2">{[["startTime", "Start time (optional)"], ["endTime", "End time (optional)"]].map(([key, label]) => <label className="text-sm" key={key}>{label}<input type="time" className={input} value={form[key]} onChange={e => change(key, e.target.value)} /></label>)}</div>
            <label className="block text-sm">Audience<select className={input} disabled={form.isHoliday} value={form.audience} onChange={e => setForm({ ...form, audience: e.target.value, audienceId: "" })}>{[["School", "Whole school"], ["Section", "Section"], ["Grade", "Grade"], ["Class", "Class"]].map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
            {form.audience !== "School" && <label className="block text-sm">Select {form.audience.toLowerCase()}<span className="required-marker" aria-hidden="true"> *</span><select required className={input} value={form.audienceId ?? ""} onChange={e => change("audienceId", e.target.value)}><option value="">Select {form.audience.toLowerCase()}</option>{(options?.[form.audience === "Class" ? "classes" : form.audience === "Grade" ? "grades" : "sections"] || []).map(o => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>}
            <div className="flex flex-wrap gap-5">{[["includeStaff", "Staff"], ["includeStudents", "Students"], ["includeParents", "Parents / guardians"]].map(([key, label]) => <label className="flex items-center gap-2 text-sm" key={key}><input type="checkbox" disabled={form.isHoliday} checked={form[key]} onChange={e => change(key, e.target.checked)} />{label}</label>)}</div>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isHoliday} onChange={e => setForm({ ...form, isHoliday: e.target.checked, ...(e.target.checked ? { audience: "School", audienceId: "", includeStaff: true, includeStudents: true, includeParents: true } : {}) })} />School closed for this event</label><p className="text-xs text-slate-500">School closure applies to everyone and excludes these dates from school-day totals and staff leave. An ordinary event keeps the existing school-day status.</p>
        </fieldset><div className="flex gap-3"><button disabled={busy || !form.title.trim()} className={button}>{busy ? "Saving…" : form.id ? "Save and notify recipients" : "Publish and notify recipients"}</button><button disabled={busy} type="button" className="text-sm text-slate-600" onClick={() => setForm(null)}>Close</button></div></form>}
        {cancel && <form onSubmit={cancelEvent} className="space-y-3 rounded-xl border border-red-200 bg-white p-5"><h3 className="font-semibold">Cancel {cancel.title}?</h3><p className="text-sm">Recipients will be notified. Any school closure created by this event will be removed; other holidays remain.</p><textarea aria-label="Cancellation reason" required maxLength={1000} disabled={busy} className={input} value={reason} onChange={e => setReason(e.target.value)} placeholder="Reason for cancellation" /><button disabled={busy || !reason.trim()} className={button}>Confirm cancellation</button><button disabled={busy} type="button" className="ml-3 text-sm" onClick={() => setCancel(null)}>Keep event</button></form>}
        <fieldset disabled={busy}><EventList events={events} canManage={canManage} onEdit={e => { setForm({ ...e, startTime: e.startTime?.slice(0, 5) || "", endTime: e.endTime?.slice(0, 5) || "" }); setCancel(null); setError(""); setMessage(""); window.scrollTo({ top: 0, behavior: "smooth" }); }} onCancel={e => { setCancel(e); setReason(""); setForm(null); setError(""); setMessage(""); }} /></fieldset>
    </section>;
}
