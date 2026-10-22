import { useEffect, useState } from "react";
import { useAuth } from "../../context/AuthContext";
import api from "../../api/axios";

const initial = { assignmentId: "", yearId: "", gradeId: "", classId: "", subjectId: "", classDate: "", startTime: "", endTime: "", room: "", reason: "" };
const input = "mt-2 w-full rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
export default function SpecialClassesPage() {
    const { user } = useAuth();
    const review = user?.roles?.some(role => ["Admin", "Principal", "Deputy Principal", "Section Head"].includes(role));
    const teacher = user?.roles?.includes("Teacher");
    const [assignments, setAssignments] = useState([]);
    const [items, setItems] = useState([]);
    const [pending, setPending] = useState([]);
    const [form, setForm] = useState(initial);
    const [editing, setEditing] = useState(null);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [reload, setReload] = useState(0);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    useEffect(() => {
        let active = true;
        async function load() {
            setLoading(true);
            try {
                const requests = [api.get(review ? "/special-classes/upcoming" : "/special-classes/my")];
                if (review) requests.push(api.get("/special-classes/pending"));
                if (review || teacher) requests.push(api.get(review ? "/special-classes/creation-options" : "/teacher-assignments/my"));
                const data = await Promise.all(requests);
                if (!active) return;
                setItems(data[0].data.specialClasses ?? []);
                if (review) setPending(data[1].data.specialClasses ?? []);
                if (review || teacher) setAssignments(data[data.length - 1].data.assignments ?? []);
            } catch (err) { if (active) setError(err?.response?.data?.message || "Unable to load special classes."); }
            finally { if (active) setLoading(false); }
        }
        load(); return () => { active = false; };
    }, [review, teacher, reload]);
    async function run(action, message) {
        if (busy) return;
        setBusy(true); setError(""); setSuccess("");
        try { await action(); setSuccess(message); setEditing(null); setForm(initial); setReload(value => value + 1); }
        catch (err) { setError(err?.response?.data?.message || "Unable to complete this action."); }
        finally { setBusy(false); }
    }
    function save(event) {
        event.preventDefault();
        const payload = { classDate: form.classDate, startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00`, room: form.room.trim() || null, reason: form.reason.trim() || null };
        if (editing) return run(() => api.put(`/special-classes/${editing.id}/reschedule`, payload), "Special class rescheduled.");
        const assignment = assignments.find(item => String(item.id) === form.assignmentId);
        if (!assignment) { setError("Select a teacher, class and subject assignment."); return; }
        return run(() => api.post(review ? "/special-classes" : "/special-classes/request", { ...payload, academicYearId: assignment.academicYear.id, schoolClassId: assignment.schoolClass.id, subjectId: assignment.subject.id, ...(review ? { staffId: assignment.staff.id } : {}) }), review ? "Special class scheduled. Notifications sent to the assigned teacher, relevant section heads, students and parents." : "Request sent for approval.");
    }
    function decision(item, approve) {
        const remarks = window.prompt(approve ? "Approval remarks (optional)" : "Reason for rejecting this request");
        if (remarks === null) return;
        if (!approve && !remarks.trim()) { setError("Enter a rejection reason."); return; }
        run(() => api.post(`/special-classes/${item.id}/review`, { approve, remarks }), approve ? "Special class approved." : "Request rejected.");
    }
    function cancel(item) {
        const reason = window.prompt("Reason for cancelling this special class");
        if (reason === null) return;
        if (!reason.trim()) { setError("Enter a cancellation reason."); return; }
        run(() => api.post(`/special-classes/${item.id}/cancel`, { reason }), "Special class cancelled.");
    }
    const card = (item, awaiting = false) => <article key={item.id} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <div className="flex justify-between"><h3 className="font-semibold">{item.subject?.name} · Grade {item.schoolClass?.grade} · {item.schoolClass?.name}</h3><span className="text-sm text-blue-600">{item.status || "Approved"}</span></div>
        <p className="text-sm text-slate-600">{item.classDate} · {item.startTime?.slice(0, 5)}–{item.endTime?.slice(0, 5)}{item.teacher ? ` · ${item.teacher.fullName}` : ""}</p>
        {item.reason && <p className="text-sm text-slate-600">Reason: {item.reason}</p>}
        {item.room && <p className="text-sm text-slate-600">Location: {item.room}</p>}
        {item.reviewRemarks && <p className="text-sm text-slate-600">Review: {item.reviewRemarks}</p>}
        <div className="flex gap-4">
            {awaiting ? <><button className={button} disabled={busy} onClick={() => decision(item, true)}>Approve</button><button className="text-sm text-red-600" disabled={busy} onClick={() => decision(item, false)}>Reject</button></>
                : (review || item.status === "Pending") && ["Approved", "Pending"].includes(item.status || "Approved") && <>{review && <button className="text-sm text-blue-600" disabled={busy} onClick={() => { setEditing(item); setForm({ ...initial, classDate: item.classDate, startTime: item.startTime.slice(0, 5), endTime: item.endTime.slice(0, 5), room: item.room || "", reason: item.reason || "" }); window.scrollTo({ top: 0, behavior: "smooth" }); }}>Reschedule</button>}<button className="text-sm text-red-600" disabled={busy} onClick={() => cancel(item)}>Cancel class</button></>}
        </div>
    </article>;
    return <div className="space-y-6 p-6 lg:p-10">
        <div className="flex items-center justify-between"><h1 className="text-2xl font-bold">Special Classes</h1><button className={button} onClick={() => { setError(""); setReload(value => value + 1); }}>Refresh</button></div>
        <p className="text-sm text-slate-500">{review ? "Schedule weekend classes, review requests and manage upcoming sessions." : "Request weekend classes and review their approval status."} Classes and teachers cannot overlap.</p>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-600">{error}</p>}
        {success && <p role="status" className="rounded-xl bg-emerald-50 p-4 text-emerald-700">{success}</p>}
        {(review || teacher || editing) && <form onSubmit={save} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6">
            <h2 className="font-semibold">{editing ? "Reschedule special class" : review ? "Schedule a special class" : "Request a special class"}</h2>
            {!editing && review && <>
                <p className="text-sm text-slate-500">Select the class and subject, then an assigned teacher. This class is confirmed immediately and recipients are notified.</p>
                <div className="grid gap-4 md:grid-cols-2">
                    {[
                        ["Academic year", "yearId", assignments.map(a => a.academicYear), ["gradeId", "classId", "subjectId", "assignmentId"]],
                        ["Grade", "gradeId", assignments.filter(a => String(a.academicYear.id) === form.yearId).map(a => ({ id: a.schoolClass.grade.id, name: `${a.schoolClass.grade.section.name} · ${a.schoolClass.grade.name}` })), ["classId", "subjectId", "assignmentId"]],
                        ["Class", "classId", assignments.filter(a => String(a.academicYear.id) === form.yearId && String(a.schoolClass.grade.id) === form.gradeId).map(a => a.schoolClass), ["subjectId", "assignmentId"]],
                        ["Subject", "subjectId", assignments.filter(a => String(a.academicYear.id) === form.yearId && String(a.schoolClass.id) === form.classId).map(a => a.subject), ["assignmentId"]],
                    ].map(([label, key, options, reset]) => <label key={key} className="text-sm">{label}<span className="required-marker" aria-hidden="true"> *</span><select required disabled={busy || loading || !options.length} className={input} value={form[key]} onChange={e => setForm({ ...form, [key]: e.target.value, ...Object.fromEntries(reset.map(k => [k, ""])) })}><option value="">Select {label.toLowerCase()}</option>{[...new Map(options.map(o => [o.id, o])).values()].map(o => <option key={o.id} value={o.id}>{o.name}</option>)}</select></label>)}
                    <label className="text-sm">Teacher<span className="required-marker" aria-hidden="true"> *</span><select required disabled={busy || !form.subjectId} className={input} value={form.assignmentId} onChange={e => setForm({ ...form, assignmentId: e.target.value })}><option value="">Select teacher</option>{assignments.filter(a => String(a.academicYear.id) === form.yearId && String(a.schoolClass.id) === form.classId && String(a.subject.id) === form.subjectId).map(a => <option key={a.id} value={a.id}>{a.staff.staffNumber} — {a.staff.fullName}</option>)}</select></label>
                </div>
                {!loading && !assignments.length && <p className="text-sm text-amber-700">No active teacher assignments are available. Set up teacher assignments before scheduling a class.</p>}
            </>}
            {!editing && !review && <label className="block text-sm">Class / subject / academic year<span className="required-marker" aria-hidden="true"> *</span><select required className={input} value={form.assignmentId} onChange={event => setForm({ ...form, assignmentId: event.target.value })}><option value="">Select assignment</option>{assignments.map(item => <option key={item.id} value={item.id}>{item.academicYear.name} · {item.schoolClass.grade.name} {item.schoolClass.name} · {item.subject.name}</option>)}</select></label>}
            <div className="grid gap-4 md:grid-cols-3">{[ ["Date (Saturday or Sunday)", "classDate", "date"], ["Start time", "startTime", "time"], ["End time", "endTime", "time"] ].map(([label, key, type]) => <label key={key} className="text-sm">{label}<span className="required-marker" aria-hidden="true"> *</span><input required className={input} type={type} value={form[key]} onChange={event => setForm({ ...form, [key]: event.target.value })} /></label>)}</div>
            <label className="block text-sm">Location (optional)<input maxLength={200} className={input} value={form.room} onChange={event => setForm({ ...form, room: event.target.value })} /></label>
            <label className="block text-sm">Reason<span className="required-marker" aria-hidden="true"> *</span><textarea required className={input} value={form.reason} onChange={event => setForm({ ...form, reason: event.target.value })} /></label>
            <button disabled={busy || loading || !editing && !form.assignmentId} className={button}>{busy ? "Saving…" : editing ? "Save schedule" : review ? "Schedule class" : "Submit request"}</button>
            {editing && <button type="button" className="ml-4 text-sm text-slate-600" onClick={() => { setEditing(null); setForm(initial); }}>Cancel edit</button>}
        </form>}
        {loading ? <p>Loading special classes…</p> : <>
            {review && <section className="space-y-4"><h2 className="font-semibold">Pending requests ({pending.length})</h2>{pending.map(item => card(item, true))}{!pending.length && <p className="text-sm text-slate-500">No pending requests.</p>}</section>}
            <section className="space-y-4"><h2 className="font-semibold">{review ? "Upcoming approved classes" : "My requests and classes"}</h2>{items.map(item => card(item))}{!items.length && <p className="text-sm text-slate-500">No special classes available.</p>}</section>
        </>}
    </div>;
}
