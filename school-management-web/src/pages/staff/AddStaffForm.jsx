import { useAuth } from "../../context/AuthContext";
import { useEffect, useRef, useState } from "react";
import { staffManagementApi, staffErrorMessage } from "../../api/staffManagementApi";
import { useRegistrationDraft } from "../../components/RegistrationDrafts";

const emptyForm = { staffNumber: "", fullName: "", email: "", designation: "", roleId: "", canTeach: "", teachingSubjectIds: [], temporaryPassword: "", confirmPassword: "" };
const inputClass = "mt-2 w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50";
const buttonClass = "cursor-pointer rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50";

export default function AddStaffForm({ onCreated, onCancel, draftId, onDraftSaved }) {
    const { user } = useAuth();
    const admin = user?.roles?.includes("Admin");
    const [form, setForm] = useState({ ...emptyForm });
    const [roles, setRoles] = useState([]);
    const [loading, setLoading] = useState(true);
    const [rolesError, setRolesError] = useState("");
    const [error, setError] = useState("");
    const [saving, setSaving] = useState(false);
    const [reload, setReload] = useState(0);
    const [subjects, setSubjects] = useState([]), [subjectLoading, setSubjectLoading] = useState(false), [subjectError, setSubjectError] = useState(""), [subjectSearch, setSubjectSearch] = useState("");
    const teachingRole = ["Admin", "Section Head", "Teacher"].includes(roles.find(r => r.id === form.roleId)?.name);
    const needsSubjects = teachingRole && form.canTeach === "yes";
    const busy = useRef(false);
    const mounted = useRef(false);
    const draftData = Object.fromEntries(Object.entries(form).filter(([key]) => !["temporaryPassword", "confirmPassword"].includes(key)));
    const draft = useRegistrationDraft({ kind: "staff", draftId, data: draftData,
        hasData: Object.values(form).some(v => Array.isArray(v) ? v.length > 0 : Boolean(v)), busy: saving,
        restore: data => setForm({ ...emptyForm, ...data, canTeach: ["yes", "no"].includes(data.canTeach) ? data.canTeach : "", teachingSubjectIds: Array.isArray(data.teachingSubjectIds) ? data.teachingSubjectIds : [] }), onSaved: onDraftSaved });

    useEffect(() => {
        mounted.current = true;
        return () => { mounted.current = false; };
    }, []);

    useEffect(() => {
        let active = true;
        async function loadRoles() {
            setLoading(true);
            setRolesError("");
            try {
                const response = await staffManagementApi.getRoles();
                if (!active) return;
                if (!Array.isArray(response.data)) {
                    setRolesError("Unexpected role response. Try loading roles again.");
                    return;
                }
                setRoles(response.data.filter((role) => role.name && !["student", "parent"].includes(role.name.toLowerCase()) && (admin || !["Admin", "Principal", "Deputy Principal"].includes(role.name))));
            } catch (err) {
                if (active) setRolesError(staffErrorMessage(err, "Unable to load staff roles. Check that the API is running."));
            } finally {
                if (active) setLoading(false);
            }
        }
        loadRoles();
        return () => { active = false; };
    }, [reload, admin]);

    useEffect(() => {
        if (!needsSubjects) return;
        const c = new AbortController();
        setSubjectLoading(true); setSubjectError("");
        staffManagementApi.getTeachingSubjects({ signal: c.signal }).then(({ data }) => {
            if (!c.signal.aborted) setSubjects(data);
        }).catch(err => { if (!c.signal.aborted) setSubjectError(staffErrorMessage(err, "Unable to load subjects. Try again.")); })
            .finally(() => { if (!c.signal.aborted) setSubjectLoading(false); });
        return () => c.abort();
    }, [needsSubjects, reload]);

    function change(event) {
        const { name, value } = event.target;
        if (name === "roleId" || name === "canTeach") setSubjectSearch("");
        setForm((current) => ({ ...current, [name]: value, ...(name === "roleId" ? { canTeach: "", teachingSubjectIds: [] } : name === "canTeach" && value !== "yes" ? { teachingSubjectIds: [] } : {}) }));
        setError("");
    }

    async function submit(event) {
        event.preventDefault();
        if (busy.current || loading || rolesError) return;
        const data = {
            staffNumber: form.staffNumber.trim(), fullName: form.fullName.trim(),
            email: form.email.trim(), designation: form.designation.trim() || null,
            roleId: form.roleId, temporaryPassword: form.temporaryPassword,
            canTeach: teachingRole ? form.canTeach === "yes" ? true : form.canTeach === "no" ? false : null : null,
            teachingSubjectIds: needsSubjects ? form.teachingSubjectIds : [],
        };
        if (!data.staffNumber || !data.fullName || !data.email || !roles.some((role) => role.id === data.roleId)) {
            setError("Enter the staff number, full name, email and a valid staff role.");
            return;
        }
        if (form.temporaryPassword !== form.confirmPassword) {
            setError("Temporary password and confirmation do not match.");
            return;
        }
        busy.current = true;
        if (teachingRole && !["yes", "no"].includes(form.canTeach)) { busy.current = false; setError("Choose whether this staff member can teach."); return; }
        if (needsSubjects && (subjectLoading || subjectError || !data.teachingSubjectIds.length || data.teachingSubjectIds.some(id => !subjects.some(s => s.id === id)))) {
            busy.current = false; setError("Select one or more available subjects this staff member can teach."); return;
        }
        setSaving(true);
        setError("");
        try {
            const response = await staffManagementApi.createStaff(data);
            await draft.complete();
            if (!mounted.current) return;
            setForm({ ...emptyForm });
            onCreated(response.data);
        } catch (err) {
            if (mounted.current) {
                // Credentials stay out of logs, notifications and persistent storage.
                setForm((current) => ({ ...current, temporaryPassword: "", confirmPassword: "" }));
                setError(staffErrorMessage(err, "Unable to confirm account creation. Refresh the staff list before retrying."));
            }
        } finally {
            busy.current = false;
            if (mounted.current) setSaving(false);
        }
    }

    return (
        <form onSubmit={submit} className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6" aria-label="Add staff">
            <h2 className="text-lg font-bold text-slate-950">Add Staff</h2>
            <p className="mt-2 text-sm text-slate-500">Create a staff account. The staff member must change their temporary password on first sign-in.</p>
            <div className="mt-4 space-y-3">{draft.controls}<p className="text-xs text-slate-500">Drafts do not save passwords. Enter a new temporary password when completing a draft.</p></div>
            {rolesError && <div role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">{rolesError}<button type="button" onClick={() => setReload((current) => current + 1)} className="ml-3 cursor-pointer font-semibold underline">Retry roles</button></div>}
            {error && <p role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">{error}</p>}
            <fieldset disabled={saving || draft.loading} className="mt-5 grid min-w-0 gap-5 md:grid-cols-2">
                <label className="text-sm font-medium text-slate-700">Staff number
                    <span className="required-marker" aria-hidden="true"> *</span><input required maxLength={450} name="staffNumber" value={form.staffNumber} onChange={change} autoComplete="off" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Full name
                    <span className="required-marker" aria-hidden="true"> *</span><input required maxLength={200} name="fullName" value={form.fullName} onChange={change} autoComplete="off" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Email
                    <span className="required-marker" aria-hidden="true"> *</span><input required maxLength={256} type="email" name="email" value={form.email} onChange={change} autoComplete="off" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Designation (optional)
                    <input maxLength={200} name="designation" value={form.designation} onChange={change} className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700 md:col-span-2">Role
                    <span className="required-marker" aria-hidden="true"> *</span><select required name="roleId" value={form.roleId} onChange={change} disabled={loading || !!rolesError || saving} className={`${inputClass} cursor-pointer disabled:cursor-not-allowed`}>
                        <option value="">{loading ? "Loading roles…" : "Select staff role"}</option>
                        {roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
                    </select>
                </label>
                {teachingRole && <label className="text-sm font-medium text-slate-700 md:col-span-2">Can this staff member teach?<span className="required-marker" aria-hidden="true"> *</span><select required name="canTeach" value={form.canTeach} onChange={change} className={inputClass}><option value="">Select Yes or No</option><option value="no">No</option><option value="yes">Yes</option></select></label>}
                {needsSubjects && <fieldset className="min-w-0 space-y-3 rounded-xl border border-slate-200 p-4 md:col-span-2"><legend className="px-1 text-sm font-semibold">Subjects they can teach<span className="required-marker" aria-hidden="true"> *</span></legend><p className="text-sm text-slate-500">Select one or more subjects. Class and timetable assignments are managed separately in Teacher Assignments.</p><label className="block text-sm">Search subjects<input type="search" className={inputClass} value={subjectSearch} onChange={e => setSubjectSearch(e.target.value)} /></label>
                    {subjectLoading ? <p role="status">Loading subjects…</p> : subjectError ? <p role="alert" className="text-sm text-red-700">{subjectError}<button type="button" className="ml-3 underline" onClick={() => setReload(v => v + 1)}>Retry subjects</button></p> : <><p className="text-sm font-medium">{form.teachingSubjectIds.length} selected</p><div className="max-h-60 space-y-1 overflow-auto">{subjects.filter(s => s.name.toLowerCase().includes(subjectSearch.trim().toLowerCase())).map(s => <label key={s.id} className="flex cursor-pointer items-center gap-3 rounded-lg p-2 text-sm hover:bg-slate-50"><input type="checkbox" checked={form.teachingSubjectIds.includes(s.id)} onChange={e => { setForm(current => ({ ...current, teachingSubjectIds: e.target.checked ? [...current.teachingSubjectIds, s.id] : current.teachingSubjectIds.filter(id => id !== s.id) })); setError(""); }} />{s.name}</label>)}</div>{!subjects.length && <p className="text-sm text-amber-800">No active subjects are available. Add subjects in Academic Setup first, then retry.</p>}{!!subjects.length && !subjects.some(s => s.name.toLowerCase().includes(subjectSearch.trim().toLowerCase())) && <p className="text-sm text-slate-500">No matching subjects.</p>}{form.teachingSubjectIds.some(id => !subjects.some(s => s.id === id)) && <p className="text-sm text-red-700">Some saved subjects are no longer available. <button type="button" className="underline" onClick={() => setForm(current => ({ ...current, teachingSubjectIds: current.teachingSubjectIds.filter(id => subjects.some(s => s.id === id)) }))}>Remove unavailable subjects</button></p>}</>}
                </fieldset>}
                <label className="text-sm font-medium text-slate-700">Temporary password
                    <span className="required-marker" aria-hidden="true"> *</span><input required type="password" name="temporaryPassword" value={form.temporaryPassword} onChange={change} autoComplete="new-password" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Confirm temporary password
                    <span className="required-marker" aria-hidden="true"> *</span><input required type="password" name="confirmPassword" value={form.confirmPassword} onChange={change} autoComplete="new-password" className={inputClass} />
                </label>
            </fieldset>
            <p className="mt-3 text-xs text-slate-500">Use a strong temporary password with uppercase and lowercase letters, a number and a symbol. The server validates the password policy.</p>
            <div className="mt-5 flex flex-col gap-3 sm:flex-row">
                <button type="submit" disabled={saving || loading || !!rolesError || !roles.length} className={`${buttonClass} bg-blue-600 text-white hover:bg-blue-700`}>{saving ? "Creating…" : "Create Staff"}</button>
                <button type="button" disabled={saving} onClick={() => draft.requestLeave(onCancel)} className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}>Cancel</button>
            </div>
        </form>
    );
}
