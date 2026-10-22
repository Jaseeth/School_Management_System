import { useEffect, useState } from "react";
import api from "../../api/axios";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import BackupsPage from "./BackupsPage";

export default function SchoolSettingsPage() {
    const { user } = useAuth();
    const [params, setParams] = useSearchParams();
    const admin = user?.roles?.includes("Admin");
    const backups = admin && params.get("tab") === "backups";
    return <div className="space-y-5">
        <h1 className="text-2xl font-bold">School Settings</h1>
        <nav aria-label="School settings sections" className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3">
            <button type="button" aria-current={!backups ? "page" : undefined} onClick={() => setParams({})} className={`rounded-xl px-4 py-3 text-sm font-semibold ${!backups ? "bg-blue-600 text-white" : "text-slate-600"}`}>School details</button>
            {admin && <button type="button" aria-current={backups ? "page" : undefined} onClick={() => setParams({ tab: "backups" })} className={`rounded-xl px-4 py-3 text-sm font-semibold ${backups ? "bg-blue-600 text-white" : "text-slate-600"}`}>Backup and Restore</button>}
        </nav>
        {backups ? <BackupsPage /> : <SchoolDetails />}
    </div>;
}

function SchoolDetails() {
    const [form, setForm] = useState(null);
    const [options, setOptions] = useState(null);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [busy, setBusy] = useState(false);
    useEffect(() => {
        Promise.all([api.get("/school-settings"), api.get("/school-settings/options")])
            .then(([profile, choices]) => { setForm(profile.data); setOptions(choices.data); })
            .catch(() => setError("Unable to load school settings. Refresh to try again."));
    }, []);
    const update = (key, value) => { setForm(previous => ({ ...previous, [key]: value })); setMessage(""); };
    async function logo(file) {
        if (!file) return;
        setError("");
        if (!["image/png", "image/jpeg"].includes(file.type) || file.size > 256000) { setError("Choose a PNG or JPEG logo, up to 250 KB."); return; }
        const reader = new FileReader();
        reader.onload = () => update("logoDataUrl", reader.result);
        reader.onerror = () => setError("Unable to read this logo. Please select it again.");
        reader.readAsDataURL(file);
    }
    async function save(event) {
        event.preventDefault(); setBusy(true); setError(""); setMessage("");
        try {
            const response = await api.put("/school-settings", form);
            setForm(response.data); setMessage("School settings saved.");
            window.dispatchEvent(new Event("school-settings-changed"));
        } catch (e) { setError(e.response?.data?.message || "Unable to save settings. Check the fields and try again."); }
        finally { setBusy(false); }
    }
    const inputClass = "mt-2 block w-full rounded-xl border border-slate-200 bg-white p-3";
    return <main className="max-w-4xl space-y-5">
        <p className="text-sm text-slate-600">Manage your school’s identity and contact details. Timezone controls school dates and attendance times. Set yearly holidays and weekends in School Calendar.</p>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</p>}
        {message && <p role="status" className="rounded-xl bg-green-50 p-4 text-green-800">{message}</p>}
        {!form || !options ? <p>Loading school settings…</p> : <form onSubmit={save} className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6">
            <fieldset disabled={busy} className="space-y-5">
                <legend className="mb-4 font-semibold">School details</legend>
                <div className="grid gap-5 sm:grid-cols-2">
                    {[["name", "School name", "text", 160], ["phone", "Phone number", "tel", 100], ["email", "Email address", "email", 254], ["website", "Website", "url", 300]].map(([key, label, type, max]) => <label className="text-sm" key={key}>{label}{key === "name" && <span className="required-marker" aria-hidden="true"> *</span>}<input className={inputClass} required={key === "name"} maxLength={max} type={type} value={form[key]} onChange={e => update(key, e.target.value)} /></label>)}
                </div>
                <label className="block text-sm">Address<textarea className={inputClass} rows={3} maxLength={500} value={form.address} onChange={e => update("address", e.target.value)} /></label>
                <div className="grid gap-5 sm:grid-cols-2">
                    <label className="text-sm">Country<select className={inputClass} value={form.countryCode} onChange={e => update("countryCode", e.target.value)}>{options.countries.map(c => <option value={c.code} key={c.code}>{c.name}</option>)}</select></label>
                    <label className="text-sm">School timezone<select className={inputClass} value={form.timeZoneId} onChange={e => update("timeZoneId", e.target.value)}>{!options.timeZones.some(z => z.id === form.timeZoneId) && <option value={form.timeZoneId}>{form.timeZoneId}</option>}{options.timeZones.map(z => <option value={z.id} key={z.id}>{z.name}</option>)}</select></label>
                </div>
                <div className="space-y-3"><label className="block text-sm">School logo<input className="mt-2 block text-sm" type="file" accept="image/png,image/jpeg" onChange={e => logo(e.target.files?.[0])} /></label><p className="text-xs text-slate-500">PNG or JPEG, up to 250 KB.</p>{form.logoDataUrl && <div className="flex items-center gap-4"><img className="h-20 w-20 object-contain" src={form.logoDataUrl} alt="School logo preview" /><button type="button" className="text-sm text-red-700" onClick={() => update("logoDataUrl", "")}>Remove logo</button></div>}</div>
            </fieldset>
            <button disabled={busy} className="rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50">{busy ? "Saving…" : "Save school settings"}</button>
        </form>}
    </main>;
}
