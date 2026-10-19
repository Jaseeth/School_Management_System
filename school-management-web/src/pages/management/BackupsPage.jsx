import { useEffect, useState } from "react";
import api from "../../api/axios";

export default function BackupsPage() {
    const [data, setData] = useState(null);
    const [page, setPage] = useState(1);
    const [schedule, setSchedule] = useState({ enabled: false, schoolTime: "18:00" });
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [selected, setSelected] = useState(null);
    const [confirmation, setConfirmation] = useState("");
    const [password, setPassword] = useState("");
    const [reason, setReason] = useState("");
    const [restored, setRestored] = useState(false);
    async function load(nextPage = page) {
        const r = await api.get("/backups", { params: { page: nextPage, pageSize: 10 } });
        setData(r.data); setSchedule(r.data.schedule);
    }
    useEffect(() => { let active = true; api.get("/backups", { params: { page, pageSize: 10 } }).then(r => { if (active) { setData(r.data); setSchedule(r.data.schedule); } }).catch(() => { if (active) setError("Unable to load backup history."); }); return () => { active = false; }; }, [page]);
    async function perform(action) {
        setBusy(true); setError(""); setMessage("");
        try { await action(); }
        catch (e) { setError(e.response?.data?.message || "The operation did not complete. Refresh history before trying again."); }
        finally { setBusy(false); }
    }
    async function create() {
        await api.post("/backups", {}, { timeout: 0 });
        await load(); setMessage("Backup created and verified.");
    }
    async function download(item) {
        const r = await api.get(`/backups/${item.id}/download`, { responseType: "blob", timeout: 0 });
        const url = URL.createObjectURL(r.data);
        const link = document.createElement("a"); link.href = url; link.download = `School-backup-${item.id.replaceAll("-", "")}.bak`; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 60000);
    }
    async function restore(e) {
        e.preventDefault();
        await perform(async () => {
            await api.post(`/backups/${selected.id}/restore`, { password, reason, confirmation }, { timeout: 0 });
            setPassword(""); setSelected(null); setRestored(true);
        });
        setPassword("");
    }
    const date = value => new Date(value.endsWith("Z") ? value : `${value}Z`).toLocaleString();
    const button = "rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm disabled:opacity-50";
    if (restored) return <main className="space-y-4"><h1 className="text-2xl font-bold">Database restored</h1><p>Your school records have been restored. Everyone must sign in again using the accounts and passwords from that backup.</p><a className="text-blue-700" href="/login">Sign in again</a></main>;
    return <main className="space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-4"><h1 className="text-2xl font-bold">Backup and Restore</h1><button disabled={busy} className="rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50" onClick={() => perform(create)}>{busy ? "Please wait…" : "Create backup now"}</button></div>
        <p className="text-sm text-slate-600">Backups save the whole school database. Restore returns all records to that point; changes made afterward are lost. This is not an undo for a single student or action.</p>
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">Download copies to secure storage on another device. Backups contain private school information. Uploaded files outside the database and server secrets are not included.</div>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</p>}
        {message && <p role="status" className="rounded-xl bg-green-50 p-4 text-green-800">{message}</p>}
        {busy && <p role="status" className="text-sm text-slate-600">Keep this page open until the operation completes. Restoring temporarily pauses access for everyone.</p>}
        {data && <>
            <form className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5" onSubmit={e => { e.preventDefault(); perform(async () => { await api.put("/backups/schedule", schedule); setMessage("Backup schedule saved."); }); }}>
                <h2 className="font-semibold">Automatic daily backup</h2>
                <p className="text-sm text-slate-600">Runs while the API is running, at the selected school time ({data.timezone}). If it starts later that day, it checks for a missed backup. Failed attempts appear in history; use Create backup now to retry.</p>
                <div className="flex flex-wrap items-center gap-4"><label className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={busy} checked={schedule.enabled} onChange={e => setSchedule({ ...schedule, enabled: e.target.checked })} />Enable daily backup</label><label className="text-sm">Time <input aria-label="Daily backup time" className="rounded-lg border border-slate-200 p-2" type="time" required disabled={busy} value={schedule.schoolTime} onChange={e => setSchedule({ ...schedule, schoolTime: e.target.value })} /></label><button disabled={busy} className={button}>Save schedule</button></div>
                <p className="break-all text-xs text-slate-500">Server storage: {data.storageDirectory}. Backups stay here until removed by the server administrator.</p>
            </form>
            <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5"><div className="flex justify-between"><h2 className="font-semibold">Backup history</h2><button disabled={busy} className={button} onClick={() => perform(() => load())}>Refresh</button></div><div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Created", "Type", "Status", "Size", "Actions"].map(h => <th className="p-3" key={h}>{h}</th>)}</tr></thead><tbody>{data.items.map(item => <tr key={item.id} className="border-t border-slate-200"><td className="p-3">{date(item.createdUtc)}</td><td className="p-3">{item.kind}</td><td className="p-3">{item.status}{item.error && <p className="mt-1 max-w-sm text-xs text-red-700">{item.error}</p>}</td><td className="p-3">{item.bytes ? `${(item.bytes / 1048576).toFixed(1)} MB` : "—"}</td><td className="p-3">{item.status === "Verified" && <div className="flex gap-3"><button className={button} disabled={busy} onClick={() => perform(() => download(item))}>Download</button><button className="text-red-700 disabled:opacity-50" disabled={busy} onClick={() => { setSelected(item); setConfirmation(""); setReason(""); setPassword(""); setError(""); }}>Restore…</button></div>}</td></tr>)}</tbody></table>{!data.totalCount && <p className="py-4 text-sm text-slate-500">No backups yet. Create your first backup.</p>}</div><div className="flex items-center gap-4 text-sm"><button className={button} disabled={busy || page === 1} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page} of {Math.max(1, Math.ceil(data.totalCount / 10))}</span><button className={button} disabled={busy || page * 10 >= data.totalCount} onClick={() => setPage(page + 1)}>Next</button></div></section>
            <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5"><h2 className="font-semibold">Recent restores</h2>{data.restores.length ? data.restores.map(r => <div className="border-t border-slate-200 py-3 text-sm" key={r.id}><p>{date(r.createdUtc)} · {r.status}</p><p className="text-slate-600">{r.reason}</p>{r.safetyBackupId && <p className="text-xs text-slate-500">A safety backup was saved before this restore.</p>}</div>) : <p className="text-sm text-slate-500">No restores performed.</p>}</section>
        </>}
        {selected && <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-950/50 p-5"><form role="dialog" aria-modal="true" aria-labelledby="restore-title" onSubmit={restore} className="w-full max-w-lg space-y-4 rounded-2xl bg-white p-6"><h2 id="restore-title" className="text-xl font-bold">Restore database?</h2><p className="text-sm">Restore the backup from <strong>{date(selected.createdUtc)}</strong>. This replaces everyone’s school records and removes changes made afterward. A safety backup will be taken first. All accounts must sign in again.</p><label className="block text-sm">Reason<textarea required maxLength={1000} disabled={busy} className="mt-2 block w-full rounded-xl border p-3" value={reason} onChange={e => setReason(e.target.value)} /></label><label className="block text-sm">Your current Admin password<input required maxLength={128} type="password" autoComplete="current-password" disabled={busy} className="mt-2 block w-full rounded-xl border p-3" value={password} onChange={e => setPassword(e.target.value)} /></label><label className="block text-sm">Type RESTORE to confirm<input required autoComplete="off" disabled={busy} className="mt-2 block w-full rounded-xl border p-3" value={confirmation} onChange={e => setConfirmation(e.target.value)} /></label>{error && <p role="alert" className="text-sm text-red-700">{error}</p>}<div className="flex gap-3"><button type="button" disabled={busy} className={button} onClick={() => { setSelected(null); setPassword(""); }}>Cancel</button><button disabled={busy || confirmation !== "RESTORE" || !reason.trim()} className="rounded-xl bg-red-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{busy ? "Restoring…" : "Restore all records"}</button></div></form></div>}
    </main>;
}
