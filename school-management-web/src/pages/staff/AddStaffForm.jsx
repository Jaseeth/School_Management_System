import { useEffect, useRef, useState } from "react";
import { staffManagementApi, staffErrorMessage } from "../../api/staffManagementApi";

const emptyForm = { staffNumber: "", fullName: "", email: "", designation: "", roleId: "", temporaryPassword: "", confirmPassword: "" };
const inputClass = "mt-2 w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50";
const buttonClass = "cursor-pointer rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50";

export default function AddStaffForm({ onCreated, onCancel }) {
    const [form, setForm] = useState({ ...emptyForm });
    const [roles, setRoles] = useState([]);
    const [loading, setLoading] = useState(true);
    const [rolesError, setRolesError] = useState("");
    const [error, setError] = useState("");
    const [saving, setSaving] = useState(false);
    const [reload, setReload] = useState(0);
    const busy = useRef(false);
    const mounted = useRef(false);

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
                setRoles(response.data.filter((role) => role.name && !["student", "parent"].includes(role.name.toLowerCase())));
            } catch (err) {
                if (active) setRolesError(staffErrorMessage(err, "Unable to load staff roles. Check that the API is running."));
            } finally {
                if (active) setLoading(false);
            }
        }
        loadRoles();
        return () => { active = false; };
    }, [reload]);

    function change(event) {
        setForm((current) => ({ ...current, [event.target.name]: event.target.value }));
        setError("");
    }

    async function submit(event) {
        event.preventDefault();
        if (busy.current || loading || rolesError) return;
        const data = {
            staffNumber: form.staffNumber.trim(), fullName: form.fullName.trim(),
            email: form.email.trim(), designation: form.designation.trim() || null,
            roleId: form.roleId, temporaryPassword: form.temporaryPassword,
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
        setSaving(true);
        setError("");
        try {
            const response = await staffManagementApi.createStaff(data);
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
            {rolesError && <div role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">{rolesError}<button type="button" onClick={() => setReload((current) => current + 1)} className="ml-3 cursor-pointer font-semibold underline">Retry roles</button></div>}
            {error && <p role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">{error}</p>}
            <fieldset disabled={saving} className="mt-5 grid min-w-0 gap-5 md:grid-cols-2">
                <label className="text-sm font-medium text-slate-700">Staff number
                    <input required maxLength={450} name="staffNumber" value={form.staffNumber} onChange={change} autoComplete="off" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Full name
                    <input required maxLength={200} name="fullName" value={form.fullName} onChange={change} autoComplete="off" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Email
                    <input required maxLength={256} type="email" name="email" value={form.email} onChange={change} autoComplete="off" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Designation (optional)
                    <input maxLength={200} name="designation" value={form.designation} onChange={change} className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700 md:col-span-2">Role
                    <select required name="roleId" value={form.roleId} onChange={change} disabled={loading || !!rolesError || saving} className={`${inputClass} cursor-pointer disabled:cursor-not-allowed`}>
                        <option value="">{loading ? "Loading roles…" : "Select staff role"}</option>
                        {roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
                    </select>
                </label>
                <label className="text-sm font-medium text-slate-700">Temporary password
                    <input required type="password" name="temporaryPassword" value={form.temporaryPassword} onChange={change} autoComplete="new-password" className={inputClass} />
                </label>
                <label className="text-sm font-medium text-slate-700">Confirm temporary password
                    <input required type="password" name="confirmPassword" value={form.confirmPassword} onChange={change} autoComplete="new-password" className={inputClass} />
                </label>
            </fieldset>
            <p className="mt-3 text-xs text-slate-500">Use a strong temporary password with uppercase and lowercase letters, a number and a symbol. The server validates the password policy.</p>
            <div className="mt-5 flex flex-col gap-3 sm:flex-row">
                <button type="submit" disabled={saving || loading || !!rolesError || !roles.length} className={`${buttonClass} bg-blue-600 text-white hover:bg-blue-700`}>{saving ? "Creating…" : "Create Staff"}</button>
                <button type="button" disabled={saving} onClick={onCancel} className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}>Cancel</button>
            </div>
        </form>
    );
}
