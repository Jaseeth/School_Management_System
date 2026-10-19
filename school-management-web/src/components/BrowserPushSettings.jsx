import { useEffect, useState } from "react";
import api from "../api/axios";
import { browserPushSupported, enableBrowserPush, portalIdentity, readBrowserBinding } from "../services/browserPush";

export default function BrowserPushSettings({ portal }) {
    const [settings, setSettings] = useState(null);
    const [binding, setBinding] = useState(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const supported = browserPushSupported();
    useEffect(() => {
        let active = true;
        let loadingSettings = false;
        const loadSettings = async () => {
            if (loadingSettings) return;
            loadingSettings = true;
            try {
                const r = await api.get("/browser-push/config", { portalOverride: portal, headers: { "Cache-Control": "no-cache" } });
                if (active) { setSettings(r.data); setError(""); }
            } catch { if (active) setError("Unable to load browser alert settings. Refresh and try again."); }
            finally { loadingSettings = false; }
        };
        loadSettings();
        const sync = () => readBrowserBinding(portal).then(value => { if (active) setBinding(value); }).catch(() => { if (active) setError("Browser storage is unavailable. Try a regular browser window."); });
        sync(); window.addEventListener("browser-push-changed", sync); window.addEventListener("storage", sync); window.addEventListener("focus", sync);
        const retry = setInterval(loadSettings, 30000);
        window.addEventListener("focus", loadSettings); window.addEventListener("browser-push-config-refresh", loadSettings);
        return () => { active = false; clearInterval(retry); window.removeEventListener("focus", loadSettings); window.removeEventListener("browser-push-config-refresh", loadSettings); window.removeEventListener("browser-push-changed", sync); window.removeEventListener("storage", sync); window.removeEventListener("focus", sync); };
    }, [portal]);
    const connected = binding?.enabled && binding.userId === portalIdentity(portal);
    const enabled = supported && Notification.permission === "granted" && connected;
    async function change() {
        setBusy(true); setError("");
        try {
            const userId = portalIdentity(portal);
            await enableBrowserPush(portal, settings);
            const saved = await readBrowserBinding(portal);
            setBinding(saved);
            if (saved?.enabled && saved.userId === userId && portalIdentity(portal) === userId) {
                // Only a deliberate enable sends confirmation; background renewals never do.
                await api.post("/browser-push/test", { portal, token: saved.token }, { portalOverride: portal });
            }
        } catch(e) { setError(e.message || "Unable to update browser alerts."); }
        finally { setBusy(false); }
    }
    if (enabled && !busy) return null;
    return <section aria-label="Browser alerts" className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5">
        <h2 className="font-semibold">Browser alerts</h2>
        <p className="text-sm text-slate-600">Receive alerts when you are away from this page. Alerts open your notifications; private school details stay inside the portal.</p>
        {!supported ? <p className="text-sm text-slate-500">This browser cannot receive alerts here. Use a supported browser on HTTPS or localhost. In-app notifications still work.</p>
            : !settings ? <p className="text-sm text-slate-500">Checking browser alert settings…</p>
            : !settings.configured ? <p className="text-sm text-amber-800">Browser alerts are not configured for this school yet. Your administrator needs to connect Firebase web messaging.</p>
            : <>{Notification.permission === "denied" && <p className="text-sm text-slate-600">Notifications are blocked in your browser’s site settings. Allow them there, then try again.</p>}<button disabled={busy} className="rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" onClick={change}>{busy ? "Enabling notifications…" : "Enable notifications"}</button></>}
        {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
    </section>;
}
