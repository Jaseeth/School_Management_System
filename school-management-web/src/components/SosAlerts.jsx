import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import api from "../api/axios";
import { portalIdentity, browserPushSupported, enableBrowserPush } from "../services/browserPush";
import { getPortalToken } from "../services/portalSession";

const action = "rounded-xl border px-4 py-3 text-sm font-semibold disabled:opacity-50";
export default function SosAlerts() {
    const { pathname } = useLocation();
    const portal = pathname.startsWith("/student/") ? "student" : pathname.startsWith("/parent/") ? "parent" : "staff";
    const [, refresh] = useState(0);
    const identity = portalIdentity(portal);
    const signedIn = Boolean(identity && pathname !== "/" && !/login|forgot-password|reset-password|register/.test(pathname));
    const [alerts, setAlerts] = useState([]), [ready, setReady] = useState(false), [mutedAlert, setMutedAlert] = useState(null), [offline, setOffline] = useState(false);
    const [error, setError] = useState(""), [busy, setBusy] = useState(false), [enabling, setEnabling] = useState(false);
    const audio = useRef(null), dialog = useRef(null);
    const acknowledged = useRef(new Set());
    const alert = signedIn && alerts[0]?.owner === identity && alerts[0]?.portal === portal ? alerts[0] : null;
    const alertId = alert?.id, expiry = alert?.expiresAt, muted = mutedAlert === alertId;
    useEffect(() => {
        const update = () => refresh(v => v + 1);
        window.addEventListener("auth-session-changed", update); window.addEventListener("storage", update);
        return () => { window.removeEventListener("auth-session-changed", update); window.removeEventListener("storage", update); };
    }, []);
    useEffect(() => {
        if (!signedIn) return;
        let stopped = false, pending = false;
        const controller = new AbortController();
        async function check() {
            if (pending || stopped || portalIdentity(portal) !== identity) return;
            pending = true;
            try {
                const response = await api.get("/sos-alerts", { params: { portal }, portalOverride: portal, signal: controller.signal });
                if (!stopped && portalIdentity(portal) === identity) { setAlerts(response.data.filter(a => !acknowledged.current.has(`${portal}:${identity}:${a.id}`)).map(a => ({ ...a, owner: identity, portal }))); setOffline(false); }
            } catch (e) {
                // Never show private messages after a session is no longer valid.
                if (!stopped && e.response?.status === 401) setAlerts([]);
                if (!stopped) setOffline(true);
            } finally { pending = false; }
        }
        const message = e => { if (e.data?.kind === "school-browser-update" && e.data.portal === portal && e.data.recipientUserId === identity) check(); };
        check(); const timer = setInterval(check, 10000);
        window.addEventListener("focus", check); window.addEventListener("online", check); window.addEventListener("sos-changed", check);
        navigator.serviceWorker?.addEventListener("message", message);
        return () => { stopped = true; controller.abort(); clearInterval(timer); setAlerts([]); window.removeEventListener("focus", check); window.removeEventListener("online", check); window.removeEventListener("sos-changed", check); navigator.serviceWorker?.removeEventListener("message", message); };
    }, [signedIn, portal, identity]);
    useEffect(() => {
        if (alertId && dialog.current && !dialog.current.open) dialog.current.showModal();
        if (!alertId || !expiry) return;
        const timer = setTimeout(() => setAlerts(current => current.filter(a => a.id !== alertId)), Math.max(0, new Date(expiry + (/Z$/.test(expiry) ? "" : "Z")).getTime() - Date.now()));
        return () => clearTimeout(timer);
    }, [alertId, expiry]);
    useEffect(() => {
        const context = audio.current;
        if (!alertId || !ready || muted || context?.state !== "running") return;
        const nodes = new Set();
        function beep() {
            if (context.state !== "running") { setReady(false); return; }
            [660, 880, 660].forEach((frequency, i) => {
                const oscillator = context.createOscillator(), gain = context.createGain(), at = context.currentTime + i * 0.25;
                oscillator.type = "sine"; oscillator.frequency.value = frequency;
                gain.gain.setValueAtTime(0, at); gain.gain.linearRampToValueAtTime(0.18, at + 0.025); gain.gain.setValueAtTime(0.18, at + 0.16); gain.gain.linearRampToValueAtTime(0, at + 0.22);
                oscillator.connect(gain); gain.connect(context.destination); nodes.add(oscillator);
                oscillator.onended = () => { nodes.delete(oscillator); oscillator.disconnect(); gain.disconnect(); };
                oscillator.start(at); oscillator.stop(at + 0.24);
            });
        }
        beep(); const timer = setInterval(beep, 1600);
        return () => { clearInterval(timer); for (const node of nodes) { try { node.stop(); } catch { /* Already finished. */ } } };
    }, [alertId, ready, muted]);
    useEffect(() => () => { audio.current?.close().catch(() => {}); }, []);
    async function enable() {
        setEnabling(true); setError("");
        try {
            const Audio = window.AudioContext || window.webkitAudioContext;
            if (!Audio) throw new Error("This browser cannot play SOS sound. Use a supported browser.");
            if (!audio.current || audio.current.state === "closed") audio.current = new Audio();
            audio.current.onstatechange = () => setReady(audio.current?.state === "running");
            const resumed = audio.current.resume();
            const permission = browserPushSupported() && Notification.permission === "default" ? Notification.requestPermission() : Promise.resolve();
            let resumeTimer;
            try { await Promise.race([resumed, new Promise((_, reject) => { resumeTimer = setTimeout(() => reject(new Error("This browser could not start audio. Check sound permissions and device volume, then try again.")), 5000); })]); }
            finally { clearTimeout(resumeTimer); }
            setReady(audio.current.state === "running"); setMutedAlert(null);
            await permission;
            if (browserPushSupported()) {
                if (Notification.permission !== "granted") throw new Error("SOS sound is enabled in this tab. Browser notifications are blocked; allow them in your browser settings for background alerts.");
                const token = getPortalToken(portal), { data } = await api.get("/browser-push/config", { portalOverride: portal });
                if (token === getPortalToken(portal) && data.configured) await enableBrowserPush(portal, data);
                else if (!data.configured) throw new Error("SOS sound is enabled in this tab. Background notifications still need administrator configuration.");
            }
        } catch(e) { setError(e.message || "Unable to enable SOS alerts."); }
        finally { setEnabling(false); }
    }
    async function acknowledge() {
        setBusy(true); setError("");
        try {
            await api.post(`/sos-alerts/${alert.id}/acknowledge`, {}, { params: { portal }, portalOverride: portal });
            acknowledged.current.add(`${portal}:${identity}:${alert.id}`);
            if ("serviceWorker" in navigator) navigator.serviceWorker.getRegistration("/school-push/").then(r => r?.active?.postMessage({ kind: "school-clear-sos", portal, alertId: alert.id })).catch(() => {});
            setAlerts(current => current.filter(a => a.id !== alert.id)); window.dispatchEvent(new Event("sos-changed"));
            window.dispatchEvent(new Event(`${portal}-notifications-changed`));
        } catch { setError("Acknowledgement was not saved. Check your connection and try again. You can mute the sound while retrying."); }
        finally { setBusy(false); }
    }
    if (!signedIn) return null;
    return <>
        {!alert && (!ready || error) && <aside className="relative z-40 flex flex-wrap items-center justify-center gap-3 border-b border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-950" aria-label="SOS sound setup"><p>Enable SOS sound for urgent school alerts in this tab. Background sound follows device settings.</p>{!ready && <button className={action + " bg-amber-900 text-white"} onClick={enable} disabled={enabling}>{enabling ? "Enabling…" : "Enable SOS sound"}</button>}{error && <p role="status">{error}</p>}</aside>}
        {alert && <dialog ref={dialog} onCancel={e => { e.preventDefault(); setMutedAlert(alertId); }} className="fixed inset-0 z-[200] m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-xl overflow-auto rounded-2xl border-2 border-red-600 bg-white p-6 shadow-xl backdrop:bg-slate-950/70" aria-labelledby="sos-title" aria-describedby="sos-message">
            <p className="font-bold tracking-widest text-red-700">SOS · URGENT SCHOOL ALERT</p><h2 id="sos-title" className="mt-3 break-words text-2xl font-bold">{alert.title}</h2><p id="sos-message" className="mt-4 whitespace-pre-wrap break-words text-lg">{alert.message}</p>
            <p className="mt-4 text-sm text-slate-500">Sent by {alert.sentBy}. {new Date(alert.publishAt + (/Z$/.test(alert.publishAt) ? "" : "Z")).toLocaleString()}</p>
            {alerts.length > 1 && <p className="mt-2 text-sm">{alerts.length - 1} more urgent alert(s) waiting.</p>}
            {!ready && <p className="mt-4 text-sm text-red-700">Alarm sound is not enabled in this tab. Enable sound below.</p>}
            {offline && <p className="mt-3 text-amber-900">Connection unavailable. This alert may have changed. Reconnect to check its status and save your acknowledgement.</p>}{error && <p role="alert" className="mt-3 text-red-700">{error}</p>}
            <div className="mt-6 flex flex-wrap gap-3"><button className={action + " bg-red-700 text-white"} disabled={busy} onClick={acknowledge}>{busy ? "Saving…" : "I have seen this — acknowledge"}</button>
                {!ready ? <button className={action} onClick={enable} disabled={enabling}>Enable SOS sound</button> : <button className={action} onClick={() => setMutedAlert(muted ? null : alertId)}>{muted ? "Resume alarm" : "Mute alarm"}</button>}</div>
            <p className="mt-4 text-xs text-slate-500">Muting only stops this tab's sound. Acknowledge to let management know you have seen the message.</p>
        </dialog>}
    </>;
}

