import { useEffect, useMemo, useState } from "react";
import api from "../../api/axios";
import { academicApi } from "../../api/academicApi";
import { useAuth } from "../../context/AuthContext";

const blank = { title: "", message: "", audienceType: "AllStudents", sectionId: "", gradeId: "", schoolClassId: "", roleName: "Teacher", studentIds: [], staffIds: [], parentIds: [] };
const input = "w-full rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
export default function AnnouncementsPage() {
    const { user } = useAuth();
    const global = user?.roles?.some(role => ["Admin", "Principal", "Deputy Principal"].includes(role));
    const head = user?.roles?.includes("Section Head");
    const manage = global || head || user?.roles?.includes("Teacher");
    const audienceOptions = useMemo(() => global ? ["AllStudents", "AllStaff", "Section", "Grade", "Class", "Role", "SpecificStudents", "SpecificStaff", "SpecificSectionHeads", "SpecificParents"]
        : head ? ["SectionStudents", "SectionTeachers", "SpecificStudents", "SpecificStaff"] : ["ClassStudents", "SubjectStudents", "SpecificStudents"], [global, head]);
    const [items, setItems] = useState([]);
    const [form, setForm] = useState({ ...blank, audienceType: audienceOptions[0] });
    const [editId, setEditId] = useState(null);
    const [recipients, setRecipients] = useState({ students: [], staff: [], parents: [] });
    const [recipientError, setRecipientError] = useState("");
    const [recipientLoading, setRecipientLoading] = useState(false);
    const [scopeOptions, setScopeOptions] = useState({ sections: [], classes: [], subjects: [] });
    const [received, setReceived] = useState([]);
    const [search, setSearch] = useState("");
    const [sections, setSections] = useState([]);
    const [grades, setGrades] = useState([]);
    const [classes, setClasses] = useState([]);
    const [status, setStatus] = useState("all");
    const [reload, setReload] = useState(0);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const fail = err => setError(err?.response?.data?.message || "Unable to load or save announcements.");
    useEffect(() => {
        let active = true;
        async function load() {
            setLoading(true);
            try {
                if (!global) {
                    const feed = await api.get("/school-announcements/my/staff");
                    if (active) setReceived(feed.data.announcements ?? []);
                }
                const response = await api.get(manage ? "/school-announcements?includeInactive=true" : "/school-announcements/my/staff");
                if (active) setItems(response.data.announcements ?? []);
            } catch (err) { if (active) fail(err); }
            finally { if (active) setLoading(false); }
        }
        load(); return () => { active = false; };
    }, [manage, global, reload]);
    useEffect(() => {
        if (!manage) return;
        let active = true;
        setRecipientLoading(true); setRecipientError("");
        api.get("/school-announcements/recipient-options").then(({ data }) => {
            if (active) {
                setRecipients({ students: data.students ?? [], staff: data.staff ?? [], parents: data.parents ?? [] });
                setScopeOptions({ sections: data.sections ?? [], classes: data.classes ?? [], subjects: data.subjects ?? [] });
                setForm(current => ({ ...current, audienceType: audienceOptions.includes(current.audienceType) ? current.audienceType : audienceOptions[0] }));
            }
        }).catch(err => { if (active) setRecipientError(err?.response?.status === 404
            ? "Recipient lookup is unavailable. Restart the updated API, then click Refresh."
            : err?.response?.data?.message || "Could not load recipients. Click Refresh to retry.");
        }).finally(() => { if (active) setRecipientLoading(false); });
        if (global) academicApi.getSections().then(({ data }) => { if (active) setSections(data); }).catch(err => { if (active) fail(err); });
        return () => { active = false; };
    }, [manage, global, audienceOptions, reload]);
    useEffect(() => {
        let active = true;
        if (global && form.sectionId) academicApi.getGrades(form.sectionId).then(({ data }) => { if (active) setGrades(data); }).catch(err => { if (active) fail(err); });
        return () => { active = false; };
    }, [global, form.sectionId]);
    useEffect(() => {
        let active = true;
        if (global && form.gradeId) academicApi.getClasses(form.gradeId).then(({ data }) => { if (active) setClasses(data); }).catch(err => { if (active) fail(err); });
        return () => { active = false; };
    }, [global, form.gradeId]);
    async function save(event) {
        event.preventDefault(); if (busy) return;
        setBusy(true); setError(""); setSuccess("");
        const payload = { teacherAssignmentId: Number(form.teacherAssignmentId) || null, parentIds: form.parentIds, studentIds: form.studentIds, staffIds: form.staffIds, title: form.title.trim(), message: form.message.trim(), audienceType: form.audienceType,
            sectionId: ["Section", "SectionStudents", "SectionTeachers"].includes(form.audienceType) ? Number(form.sectionId) : null,
            gradeId: form.audienceType === "Grade" ? Number(form.gradeId) : null,
            schoolClassId: ["Class", "ClassStudents"].includes(form.audienceType) ? Number(form.schoolClassId) : null,
            roleName: form.audienceType === "Role" ? form.roleName : null };
        try {
            if (editId) await api.put(`/school-announcements/${editId}`, {
                title: payload.title, message: payload.message, isActive: true,
                publishAt: form.publishAt ?? null, expiresAt: form.expiresAt ?? null,
            });
            else await api.post("/school-announcements", payload);
            setSuccess(editId ? "Announcement updated." : "Announcement published.");
            setEditId(null); setForm({ ...blank, audienceType: audienceOptions[0] }); setReload(value => value + 1);
        } catch (err) { fail(err); } finally { setBusy(false); }
    }
    async function disable(item) {
        if (busy || !window.confirm(`Remove announcement “${item.title}” from readers?`)) return;
        setBusy(true); setError("");
        try { await api.patch(`/school-announcements/${item.id}/disable`); setSuccess("Announcement removed from readers."); setReload(value => value + 1); }
        catch (err) { fail(err); } finally { setBusy(false); }
    }
    const select = (label, key, options, extra = {}) => <label className="space-y-2 text-sm">{label}<select required className={input} value={form[key]} onChange={event => setForm(current => ({ ...current, [key]: event.target.value, ...extra }))}><option value="">Select {label.toLowerCase()}</option>{options.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>;
    return <div className="space-y-6 p-6 lg:p-10">
        <div className="flex items-center justify-between"><h1 className="text-2xl font-bold">Announcements</h1><button className={button} onClick={() => { setError(""); setReload(value => value + 1); }}>Refresh</button></div>
        {error && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-red-600">{error}</div>}
        {success && <div role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-emerald-700">{success}</div>}
        {manage && <form onSubmit={save} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="font-semibold">{editId ? "Edit announcement" : "Create announcement"}</h2>
            <label className="block space-y-2 text-sm">Title<input required className={input} value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></label>
            <label className="block space-y-2 text-sm">Message<textarea required rows={4} className={input} value={form.message} onChange={e => setForm({ ...form, message: e.target.value })} /></label>
            {!editId && <label className="block space-y-2 text-sm">Audience<select className={input} value={form.audienceType} onChange={e => setForm({ ...blank, title: form.title, message: form.message, audienceType: e.target.value })}>{audienceOptions.map(type => <option key={type} value={type}>{{ SpecificStudents: "Specific students", SpecificStaff: "Specific staff", SpecificSectionHeads: "Specific Section Heads", SpecificParents: "Specific parents", SectionStudents: "My section students", SectionTeachers: "My section teachers", ClassStudents: "My class students", SubjectStudents: "My subject students" }[type] || type}</option>)}</select></label>}
            {!global && <p className="text-sm text-slate-500">You can send announcements only to recipients in your current assignments.</p>}
            {!editId && ["SectionStudents", "SectionTeachers"].includes(form.audienceType) && select("Section", "sectionId", scopeOptions.sections)}
            {!editId && form.audienceType === "ClassStudents" && select("Class", "schoolClassId", scopeOptions.classes)}
            {!editId && form.audienceType === "SubjectStudents" && select("Class and subject", "teacherAssignmentId", scopeOptions.subjects)}
            {!editId && <div className="grid gap-4 md:grid-cols-3">
                {["Section", "Grade", "Class"].includes(form.audienceType) && select("Section", "sectionId", sections, { gradeId: "", schoolClassId: "" })}
                {["Grade", "Class"].includes(form.audienceType) && select("Grade", "gradeId", grades, { schoolClassId: "" })}
                {form.audienceType === "Class" && select("Class", "schoolClassId", classes)}
                {form.audienceType === "Role" && <label className="space-y-2 text-sm">Role<select className={input} value={form.roleName} onChange={e => setForm({ ...form, roleName: e.target.value })}>{["Teacher", "Section Head", "Admin", "Principal", "Deputy Principal", "Student", "Parent"].map(role => <option key={role}>{role}</option>)}</select></label>}
            </div>}
            {!editId && form.audienceType.startsWith("Specific") && (() => {
                const key = form.audienceType === "SpecificStudents" ? "studentIds" : form.audienceType === "SpecificParents" ? "parentIds" : "staffIds";
                const options = key === "studentIds" ? recipients.students : key === "parentIds" ? recipients.parents : recipients.staff.filter(person => form.audienceType !== "SpecificSectionHeads" || person.isSectionHead);
                const matches = options.filter(person => Object.values(person).filter(value => typeof value === "string").join(" ").toLowerCase().includes(search.trim().toLowerCase()));
                return <fieldset className="space-y-3"><legend className="text-sm font-semibold">Select recipients ({form[key].length} selected)</legend>
                    <input className={input} placeholder="Search by index, staff/parent number, name, section or email" aria-label="Search recipients" value={search} onChange={event => setSearch(event.target.value)} />
                    <div className="max-h-64 overflow-auto rounded-xl border border-slate-200 p-3">
                        {matches.map(person => <label key={person.id} className="flex items-center gap-3 p-2 text-sm">
                            <input type="checkbox" checked={form[key].includes(person.id)} onChange={event => setForm(current => ({ ...current, [key]: event.target.checked ? [...current[key], person.id] : current[key].filter(id => id !== person.id) }))} />{person.code} — {person.name}
                        </label>)}
                        {recipientLoading && <p className="text-sm text-slate-500">Loading recipients…</p>}
                        {recipientError && <p role="alert" className="text-sm text-red-600">{recipientError}</p>}
                        {!recipientLoading && !recipientError && !matches.length && <p className="text-sm text-slate-500">{options.length ? "No matching recipients. Try another name or number." : "No active recipients available."}</p>}
                    </div>
                </fieldset>;
            })()}
            <button className={button} disabled={busy}>{busy ? "Saving…" : editId ? "Save changes" : "Publish announcement"}</button>
            {editId && <button type="button" className="ml-4 text-sm text-slate-600" onClick={() => { setEditId(null); setForm({ ...blank, audienceType: audienceOptions[0] }); }}>Cancel</button>}
        </form>}
        {manage && <label className="block max-w-xs space-y-2 text-sm">Status<select className={input} value={status} onChange={e => setStatus(e.target.value)}><option value="all">All</option><option value="active">Active</option><option value="inactive">Inactive</option></select></label>}
        {!global && <section className="space-y-3"><h2 className="font-semibold">Received announcements</h2>{received.map(item => <article key={item.id} className="rounded-2xl border border-slate-200 bg-white p-6"><h3 className="font-semibold">{item.title}</h3><p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">{item.message}</p></article>)}</section>}
        <h2 className="font-semibold">{global ? "All announcements" : "My announcements"}</h2>
        {loading ? <p>Loading announcements…</p> : <div className="space-y-4">{items.filter(item => status === "all" || item.isActive === (status === "active")).map(item => <article key={item.id} className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="font-semibold">{item.title}</h2><p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">{item.message}</p>
            <p className="mt-3 text-xs text-slate-500">{item.audienceType} {item.sectionName || item.gradeName || item.className || item.roleName} {manage ? item.isActive ? "· Active" : "· Inactive" : ""}</p>
            {manage && item.isActive && <div className="mt-4 flex gap-6 text-sm"><button disabled={busy} className="text-blue-600" onClick={() => { setEditId(item.id); setForm({ ...blank, ...item, sectionId: item.sectionId ?? "", gradeId: item.gradeId ?? "", schoolClassId: item.schoolClassId ?? "" }); }}>Edit</button><button disabled={busy} className="text-red-600" onClick={() => disable(item)}>Remove</button></div>}
        </article>)}{!items.length && <p>No announcements available.</p>}</div>}
    </div>;
}
