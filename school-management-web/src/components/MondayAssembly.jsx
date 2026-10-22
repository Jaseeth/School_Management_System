import { useEffect, useState } from "react";
import api from "../api/axios";

export default function MondayAssembly({ editable = false, onChanged }) {
    const [saved, setSaved] = useState(null);
    const [form, setForm] = useState({ startTime: "", endTime: "" });
    const [editing, setEditing] = useState(false);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [reload, setReload] = useState(0);
    useEffect(() => {
        const controller = new AbortController();
        api.get("/timetable/assembly", { signal: controller.signal }).then(({ data }) => {
            if (controller.signal.aborted) return;
            setSaved(data);
            onChanged?.(data);
            setError("");
        }).catch(e => { if (!controller.signal.aborted) setError(e.response?.data?.message || "Unable to load assembly time."); });
        return () => controller.abort();
    }, [reload, onChanged]);
    async function save(event) {
        event.preventDefault();
        if (busy || !saved) return;
        setBusy(true); setError(""); setMessage("");
        try {
            const { data } = await api.put("/timetable/assembly", {
                startTime: `${form.startTime}:00`, endTime: `${form.endTime}:00`,
                previousStartTime: saved.startTime, previousEndTime: saved.endTime,
            });
            setSaved(data); onChanged?.(data); setEditing(false);
            setMessage("Monday assembly time updated for all classes.");
        } catch (e) { setError(e.response?.data?.message || "Unable to save assembly time."); }
        finally { setBusy(false); }
    }
    return <section aria-label="Monday assembly" className="my-5 rounded-xl border border-blue-200 bg-blue-50 p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
            <h3 className="font-semibold text-blue-800">Monday - Period 1: Assembly</h3>
            {editable && saved && !editing && <button type="button" className="rounded-xl border border-blue-300 bg-white px-4 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-100" onClick={() => {
                setForm({ startTime: saved.startTime.slice(0, 5), endTime: saved.endTime.slice(0, 5) });
                setEditing(true); setError(""); setMessage("");
            }}>Edit assembly time</button>}
        </div>
        {saved ? <p className="mt-2 text-sm text-blue-700">{saved.startTime.slice(0, 5)}–{saved.endTime.slice(0, 5)} every Monday for all classes. Teaching starts from period 2 at {saved.endTime.slice(0, 5)}.</p>
            : !error && <p role="status" className="mt-2 text-sm text-blue-700">Loading assembly time…</p>}
        {error && <div className="mt-3 text-sm text-red-700"><p role="alert">{error}</p><button type="button" disabled={busy} className="mt-2 underline" onClick={() => setReload(x => x + 1)}>Refresh assembly time</button></div>}
        {message && <p role="status" className="mt-3 text-sm text-emerald-800">{message}</p>}
        {editable && editing && <form onSubmit={save} className="mt-4 flex flex-wrap items-end gap-3">
            {[['startTime', 'Start time'], ['endTime', 'End time']].map(([key, label]) => <label key={key} className="text-sm font-medium text-slate-700">{label}<span className="required-marker" aria-hidden="true"> *</span>
                <input type="time" required disabled={busy} className="mt-1 block rounded-xl border border-slate-300 bg-white p-3" value={form[key]} onChange={e => setForm({ ...form, [key]: e.target.value })} />
            </label>)}
            <button disabled={busy} className="rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50">{busy ? "Saving…" : "Save assembly time"}</button>
            <button type="button" disabled={busy} className="rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm" onClick={() => { setEditing(false); setError(""); }}>Cancel</button>
        </form>}
    </section>;
}
