import { useState } from "react";
import api from "../../api/axios";

const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
export default function GovernmentCalendarImport({ year, onImported }) {
    const [file, setFile] = useState(null);
    const [preview, setPreview] = useState(null);
    const [selected, setSelected] = useState([]);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    async function template(format) {
        setBusy(true); setError("");
        try {
            const response = await api.get("/staff-leave/government-calendar/template", { params: { year, format }, responseType: "blob" });
            const url = URL.createObjectURL(response.data); const link = document.createElement("a");
            link.href = url; link.download = `school-calendar-${year}-template.${format}`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch { setError("Unable to download the calendar template."); }
        finally { setBusy(false); }
    }
    async function upload(e) {
        e.preventDefault(); if (!file || busy) return;
        setError(""); setMessage(""); setPreview(null);
        if (file.size > 2 * 1024 * 1024) { setError("Upload an Excel/CSV file up to 2 MB."); return; }
        setBusy(true);
        try {
            const form = new FormData(); form.append("year", String(year)); form.append("file", file);
            const response = await api.post("/staff-leave/government-calendar/preview", form, { headers: { "Content-Type": undefined } });
            setPreview(response.data); setSelected(response.data.rows.filter(r => !r.error).map(r => r.rowNumber));
        } catch (err) { setError(err.response?.data?.message || "Unable to preview this file. Use the provided Excel/CSV template."); }
        finally { setBusy(false); }
    }
    async function importRows() {
        setBusy(true); setError("");
        try {
            const response = await api.post("/staff-leave/government-calendar/import", { previewToken: preview.previewToken, rows: selected });
            setMessage(response.data.message); setPreview(null); setSelected([]); await onImported();
        } catch (err) { setError(err.response?.data?.message || "Unable to import calendar dates."); }
        finally { setBusy(false); }
    }
    const valid = preview?.rows.filter(r => !r.error) || [];
    return <section className="space-y-4 rounded-2xl border border-blue-200 bg-white p-5"><h2 className="font-semibold">Upload government / school calendar</h2><p className="text-sm text-slate-500">Import approved holiday dates or vacation ranges for {year}. All imported holidays apply to <strong>both staff and students</strong>. They reduce planned school days and are excluded from staff leave deductions.</p>
        <div className="flex flex-wrap gap-3"><button disabled={busy} className={button} onClick={() => template("xlsx")}>Download Excel template</button><button disabled={busy} className="rounded-xl border border-slate-200 px-4 py-3 text-sm disabled:opacity-50" onClick={() => template("csv")}>Download CSV template</button></div>
        <p className="text-sm text-slate-500">Columns: <strong>From, Through, Name</strong>. Use YYYY-MM-DD dates or Excel date cells. Leave Through blank for a single holiday. Each row is a holiday/closure, not a working day. For a PDF government calendar, enter its approved dates into the template first.</p>
        <form className="flex flex-wrap items-center gap-3" onSubmit={upload}><input aria-label="Government calendar file" required type="file" accept=".xlsx,.csv" disabled={busy} onChange={e => { setFile(e.target.files?.[0] || null); setPreview(null); setMessage(""); setError(""); }} /><button disabled={busy || !file} className={button}>{busy ? "Working…" : "Preview file"}</button></form>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-sm text-blue-700">{message}</p>}
        {preview && <><p className="text-sm">Review {preview.rows.length} row(s) · {valid.length} valid · {preview.rows.length - valid.length} with errors. Existing dates—including previously removed holidays—are preserved. This preview expires after {preview.expiresInMinutes} minutes.</p><label className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={busy || !valid.length} checked={valid.length > 0 && selected.length === valid.length} onChange={e => setSelected(e.target.checked ? valid.map(r => r.rowNumber) : [])} />Select all valid rows</label><div className="max-h-96 overflow-auto"><table className="w-full text-left text-sm"><thead><tr>{["Select", "File row", "From", "Through", "Holiday / vacation", "Dates", "Review"].map(label => <th className="p-2" key={label}>{label}</th>)}</tr></thead><tbody>{preview.rows.map(row => <tr key={row.rowNumber} className="border-t border-slate-100"><td className="p-2"><input aria-label={`Select file row ${row.rowNumber}`} type="checkbox" disabled={busy || Boolean(row.error)} checked={selected.includes(row.rowNumber)} onChange={e => setSelected(e.target.checked ? [...selected, row.rowNumber] : selected.filter(n => n !== row.rowNumber))} /></td><td className="p-2">{row.rowNumber}</td><td className="p-2">{row.from || "—"}</td><td className="p-2">{row.through || "—"}</td><td className="p-2">{row.name}</td><td className="p-2">{row.days}</td><td className={`p-2 ${row.error ? "text-red-700" : "text-slate-500"}`}>{row.error || (row.existingDates ? `${row.existingDates} existing date(s) will be preserved` : "Ready — staff and students")}</td></tr>)}</tbody></table></div><button disabled={busy || !selected.length} className={button} onClick={importRows}>Import {selected.length} selected row(s)</button></>}
    </section>;
}
