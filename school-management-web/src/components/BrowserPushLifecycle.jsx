import { useEffect } from "react";
import api from "../api/axios";
import { browserPushSupported, disableBrowserPush, portalIdentity, pushPortals, readBrowserBinding, renewBrowserPush } from "../services/browserPush";

export default function BrowserPushLifecycle() {
    useEffect(() => {
        if (!browserPushSupported()) return;
        let active = true;
        const lastRenewed = new Map();
        let checking = false;
        async function sync() {
            if (checking || !active) return;
            checking = true;
            try {
                for (const portal of pushPortals) {
                    const binding = await readBrowserBinding(portal);
                    if (!binding?.enabled) continue;
                    const uid = portalIdentity(portal);
                    if (uid !== binding.userId || Notification.permission !== "granted") { await disableBrowserPush(portal); continue; }
                    if (Date.now() - (lastRenewed.get(portal) || 0) < 10 * 60 * 1000) continue;
                    try {
                        const { data } = await api.get("/browser-push/config", { portalOverride: portal });
                        if (data.configured && active) { await renewBrowserPush(portal, data, uid); lastRenewed.set(portal, Date.now()); }
                    } catch { /* Keep consent and the existing token; retry after a temporary network/API failure. */ }
                }
            } finally { checking = false; }
        }
        function onMessage(event) {
            const data = event.data;
            if (data?.kind === "school-browser-update" && pushPortals.includes(data.portal) && data.recipientUserId === portalIdentity(data.portal)) window.dispatchEvent(new Event(`${data.portal}-notifications-changed`));
        }
        sync();
        const timer = setInterval(sync, 30 * 60 * 1000);
        window.addEventListener("focus", sync); window.addEventListener("auth-session-changed", sync); window.addEventListener("storage", sync);
        navigator.serviceWorker.addEventListener("message", onMessage);
        return () => { active = false; clearInterval(timer); window.removeEventListener("focus", sync); window.removeEventListener("auth-session-changed", sync); window.removeEventListener("storage", sync); navigator.serviceWorker.removeEventListener("message", onMessage); };
    }, []);
    return null;
}
