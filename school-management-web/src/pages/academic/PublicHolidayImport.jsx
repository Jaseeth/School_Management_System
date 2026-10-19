import { useState } from "react";
import api from "../../api/axios";

const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
export default function PublicHolidayImport({ year, country, onImported }) {
    const [preview, setPreview] = useState(null);
    const [selected, setSelected] = useState([]);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    async function load() {
        setBusy(true); setError(""); setMessage(""); setPreview(null);
        try {
            const r = await api.get("/staff-leave/public-holidays/preview", { params: { year } });
            setPreview(r.data); setSelected(r.data.holidays.filter(h => !h.alreadyListed).map(h => h.date));
        } catch (err) { setError(err.response?.data?.message || "Unable to load public holidays. Check the API connection."); }
        finally { setBusy(false); }
    }
    async function importDates() {
        if (busy || !preview) return; setBusy(true); setError(""); setMessage("");
        try {
            const r = await api.post("/staff-leave/public-holidays/import", { year: preview.year, countryCode: preview.countryCode, dates: selected });
            setMessage(r.data.message); setPreview(null); setSelected([]); await onImported();
        } catch (err) { setError(err.response?.data?.message || "Unable to import public holidays."); }
        finally { setBusy(false); }
    }
    return <section className="space-y-3 rounded-2xl border border-blue-200 bg-white p-5"><h2 className="font-semibold">Load public holidays automatically</h2><p className="text-sm text-slate-500">Load nationwide public holidays for <strong>{country || "your saved country"} · {year}</strong>. Review the dates, then import them into the calendar. Existing holidays and school breaks stay unchanged. Add regional holidays or special school closures separately.</p>
        {!country && <p className="text-sm text-amber-800">Save the school calendar’s country code first.</p>}
        <button disabled={busy || !country} className={button} onClick={load}>{busy ? "Working…" : "Load public holidays"}</button>
        {error && <p role="alert" className="rounded-xl bg-amber-50 p-3 text-sm text-amber-900">{error}</p>}{message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-sm text-blue-700">{message}</p>}
        {preview && <><p className="text-sm">Source: {preview.provider} · {preview.holidays.length} nationwide holiday date(s). Review before importing; local government announcements may change dates.</p>{preview.holidays.length ? <><label className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={busy || !preview.holidays.some(h => !h.alreadyListed)} checked={preview.holidays.some(h => !h.alreadyListed) && selected.length === preview.holidays.filter(h => !h.alreadyListed).length} onChange={e => setSelected(e.target.checked ? preview.holidays.filter(h => !h.alreadyListed).map(h => h.date) : [])} />Select all new holiday dates</label><div className="max-h-96 space-y-2 overflow-y-auto rounded-xl border border-slate-200 p-3">{preview.holidays.map(h => <label key={h.date} className="flex items-start gap-3 border-b border-slate-100 p-2 text-sm"><input type="checkbox" className="mt-1" disabled={busy || h.alreadyListed} checked={selected.includes(h.date)} onChange={e => setSelected(e.target.checked ? [...selected, h.date] : selected.filter(d => d !== h.date))} /><span><strong>{h.date}</strong> — {h.name}{h.alreadyListed && <span className="block text-xs text-slate-500">Already saved or previously removed — kept unchanged</span>}</span></label>)}</div><button disabled={busy || !selected.length} className={button} onClick={importDates}>Import {selected.length} selected date(s)</button></> : <p className="text-sm text-slate-500">No nationwide public holidays were returned for this country/year. No dates were changed.</p>}</>}
    </section>;
}
