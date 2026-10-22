import { useEffect, useRef, useState } from "react";
import { useBlocker } from "react-router-dom";
import api from "../api/axios";

const button = "rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold disabled:opacity-50";

export function useRegistrationDraft({ kind, draftId, data, hasData, restore, onSaved, busy = false }) {
    const id = useRef(draftId || crypto.randomUUID());
    const version = useRef(null);
    const [baseline, setBaseline] = useState(null);
    const [completed, setCompleted] = useState(false);
    const finished = useRef(false);
    const [saving, setSaving] = useState(false);
    const [loading, setLoading] = useState(Boolean(draftId));
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [localLeave, setLocalLeave] = useState(null);
    const serialized = JSON.stringify(data);
    const dirty = !completed && (baseline === null ? hasData : serialized !== baseline);
    const blocker = useBlocker(({ currentLocation, nextLocation }) => !finished.current && (dirty || busy || saving) && currentLocation.pathname + currentLocation.search !== nextLocation.pathname + nextLocation.search);
    const latestRestore = useRef(restore);
    useEffect(() => { latestRestore.current = restore; });
    useEffect(() => {
        if (!draftId) return;
        const controller = new AbortController();
        api.get(`/registration-drafts/${draftId}`, { signal: controller.signal }).then(r => {
            if (r.data.kind !== kind) throw new Error("This draft belongs to a different form.");
            version.current = r.data.updatedAt; setBaseline(JSON.stringify(r.data.data));
            latestRestore.current(r.data.data);
        }).catch(e => { if (!controller.signal.aborted) setError(e.response?.data?.message || "Unable to open this draft. Return to the list and try again."); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [draftId, kind]);
    useEffect(() => {
        if (!dirty && !busy && !saving) return;
        const warn = e => { e.preventDefault(); e.returnValue = ""; };
        window.addEventListener("beforeunload", warn); return () => window.removeEventListener("beforeunload", warn);
    }, [dirty, busy, saving]);
    async function save() {
        if (saving || busy || loading) return false;
        setSaving(true); setError("");
        try {
            const r = await api.put(`/registration-drafts/${id.current}`, { kind, data, expectedUpdatedAt: version.current });
            version.current = r.data.updatedAt; setBaseline(serialized); setMessage("Draft saved. You can resume it from the management page."); onSaved?.(); return true;
        } catch(e) { setError(e.response?.data?.message || "Unable to save the draft. Your form is still open."); return false; }
        finally { setSaving(false); }
    }
    function requestLeave(action) { if (dirty || busy || saving) setLocalLeave(() => action); else action(); }
    function leave() {
        finished.current = true;
        setCompleted(true);
        if (blocker.state === "blocked") blocker.proceed();
        else { const action = localLeave; setLocalLeave(null); action?.(); }
    }
    async function complete() {
        finished.current = true;
        setCompleted(true);
        if (version.current) {
            try { await api.delete(`/registration-drafts/${id.current}`); }
            catch { /* The real record is already created; never repeat registration to retry draft cleanup. */ }
        }
    }
    return { save, complete, requestLeave, loading, saving,
        controls: <>
            {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-sm text-red-700">{error}</p>}
            {message && <p role="status" className="rounded-xl bg-blue-50 p-4 text-sm text-blue-700">{message}</p>}
            {loading ? <p role="status">Opening draft…</p> : <button type="button" className={button} disabled={busy || saving || !hasData} onClick={save}>{saving ? "Saving draft…" : "Save draft"}</button>}
            {(blocker.state === "blocked" || localLeave) && <DraftDialog title="Leave this form?">
                <p className="text-sm text-slate-600">Save the entered details as a draft, or discard unsaved changes and leave. {kind === "staff" && "Passwords are not stored in drafts."}</p>
                {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
                {(busy || saving) && <p role="status" className="text-sm">Please wait for the current operation to finish.</p>}
                <div className="flex flex-wrap gap-3"><button type="button" className={button + " bg-blue-600 text-white"} disabled={busy || saving || !hasData} onClick={async () => { if (await save()) leave(); }}>Save as draft and leave</button><button type="button" className={button} disabled={busy || saving} onClick={leave}>Discard and leave</button><button type="button" className={button} disabled={busy || saving} onClick={() => { if (blocker.state === "blocked") blocker.reset(); setLocalLeave(null); }}>Keep editing</button></div>
            </DraftDialog>}
        </> };
}

export function DraftDialog({ title, children }) {
    const ref = useRef(null);
    useEffect(() => { const dialog = ref.current; dialog.showModal(); return () => dialog.close(); }, []);
    return <dialog ref={ref} onCancel={e => e.preventDefault()} aria-label={title} className="fixed inset-0 m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-xl space-y-5 overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 text-slate-900 shadow-xl backdrop:bg-slate-950/50"><h2 className="text-lg font-semibold">{title}</h2>{children}</dialog>;
}

export function RegistrationDraftList({ kind, onResume, refresh = 0 }) {
    const [items, setItems] = useState([]), [error, setError] = useState(""), [deleting, setDeleting] = useState(null), [busy, setBusy] = useState(false);
    const [version, setVersion] = useState(0);
    useEffect(() => {
        const controller = new AbortController();
        api.get("/registration-drafts", { params: { kind }, signal: controller.signal }).then(r => { setItems(r.data); setError(""); })
            .catch(() => { if (!controller.signal.aborted) setError("Unable to load saved drafts."); });
        return () => controller.abort();
    }, [kind, refresh, version]);
    async function remove() {
        setBusy(true); setError("");
        try { await api.delete(`/registration-drafts/${deleting.id}`); setDeleting(null); setVersion(v => v + 1); }
        catch(e) { if (e.response?.status === 404) { setDeleting(null); setVersion(v => v + 1); } else setError("Unable to delete this draft."); }
        finally { setBusy(false); }
    }
    return <section aria-label="Saved drafts" className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5"><h2 className="font-semibold">Saved drafts ({items.length})</h2><p className="text-sm text-slate-500">Your unfinished {kind} forms. Drafts are not active records.</p>{error && <p role="alert" className="text-sm text-red-700">{error}</p>}{!items.length && <p className="text-sm text-slate-500">No saved drafts.</p>}{items.map(item => <article key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 p-4"><div><h3 className="font-medium">{item.title}</h3><p className="text-xs text-slate-500">Draft · Saved {new Date(item.updatedAt + (/Z$/.test(item.updatedAt) ? "" : "Z")).toLocaleString()}</p></div><div className="flex gap-3"><button type="button" className={button} onClick={() => onResume(item.id)}>Edit draft</button><button type="button" className={button + " text-red-700"} onClick={() => setDeleting(item)}>Delete draft</button></div></article>)}{deleting && <DraftDialog title="Delete draft?"><p className="text-sm">Delete “{deleting.title}”? The saved details cannot be recovered.</p>{error && <p role="alert" className="text-red-700">{error}</p>}<div className="flex gap-3"><button type="button" disabled={busy} className={button + " text-red-700"} onClick={remove}>{busy ? "Deleting…" : "Confirm delete"}</button><button type="button" disabled={busy} className={button} onClick={() => setDeleting(null)}>Cancel</button></div></DraftDialog>}</section>;
}
