import axios from "axios";
import api from "../api/axios";
import { getPortalToken } from "./portalSession";

export const pushPortals = ["staff", "student", "parent"];
const running = new Map();
let tokenRequest;
const storeModule = () => { const url = new URL("/school-push-store.js", window.location.origin).href; return import(/* @vite-ignore */ url); };
export function browserPushSupported() {
    return typeof window !== "undefined" && window.isSecureContext && "Notification" in window && "serviceWorker" in navigator && "PushManager" in window && "indexedDB" in window;
}
export function portalIdentity(portal) {
    try {
        const payload = JSON.parse(atob(getPortalToken(portal).split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
        return payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] || payload.nameid || payload.sub || null;
    } catch { return null; }
}
export async function readBrowserBinding(portal) {
    if (!browserPushSupported()) return null;
    return (await storeModule()).bindingStore(portal);
}
async function worker() {
    const registration = await navigator.serviceWorker.register("/school-push-sw.js", { scope: "/school-push/", type: "module", updateViaCache: "none" });
    const pending = registration.installing || registration.waiting;
    if (!registration.active && pending) await new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error("Browser alert setup timed out. Try again.")), 15000);
        pending.addEventListener("statechange", () => {
            if (pending.state === "activated") { clearTimeout(timer); resolve(); }
            if (pending.state === "redundant") { clearTimeout(timer); reject(new Error("Unable to activate browser alerts.")); }
        });
    });
    return registration;
}
export async function enableBrowserPush(portal, settings) {
    if (!pushPortals.includes(portal) || !browserPushSupported()) throw new Error("This browser does not support alerts. Use a supported browser on HTTPS or localhost.");
    if (!settings?.configured) throw new Error("The school administrator must configure Firebase browser alerts first.");
    const userId = portalIdentity(portal);
    if (!userId) throw new Error("Sign in before enabling browser alerts.");
    // Invoke permission directly from the user's button press, before asynchronous SDK loading.
    const permission = Notification.permission === "granted" ? "granted" : await Notification.requestPermission();
    if (permission !== "granted") throw new Error("Browser alerts are blocked. You can allow notifications in this site's browser settings.");
    return renewBrowserPush(portal, settings, userId);
}
export async function renewBrowserPush(portal, settings, expectedUserId = portalIdentity(portal)) {
    const revision = localStorage.getItem(`browserPushRevision:${portal}`);
    const pending = running.get(portal);
    if (pending) return pending.revision === revision ? pending.task : pending.task.catch(() => {}).then(() => renewBrowserPush(portal, settings, expectedUserId));
    const task = (async () => {
        const store = await storeModule();
        const previous = await store.bindingStore(portal);
        const expectedBindingRevision = previous?.revision ?? null;
        const [{ initializeApp, getApps }, { getMessaging, getToken, isSupported }] = await Promise.all([import("firebase/app"), import("firebase/messaging")]);
        if (!await isSupported()) throw new Error("Browser alerts are unavailable in this browser or private browsing mode.");
        const registration = await worker();
        const app = getApps().find(a => a.name === "school-browser-alerts") || initializeApp(settings.config, "school-browser-alerts");
        if (!tokenRequest) tokenRequest = getToken(getMessaging(app), { vapidKey: settings.vapidKey, serviceWorkerRegistration: registration }).finally(() => { tokenRequest = null; });
        const token = await tokenRequest;
        if (!token || portalIdentity(portal) !== expectedUserId) throw new Error("Your account changed. Enable alerts again after signing in.");
        if (localStorage.getItem(`browserPushRevision:${portal}`) !== revision) return;
        await api.post("/browser-push/register", { portal, token }, { portalOverride: portal });
        if (portalIdentity(portal) !== expectedUserId || localStorage.getItem(`browserPushRevision:${portal}`) !== revision) return;
        if (portalIdentity(portal) !== expectedUserId || localStorage.getItem(`browserPushRevision:${portal}`) !== revision) return;
        const saved = await store.bindingStore(portal, { portal, userId: expectedUserId, token, enabled: true, revision: crypto.randomUUID() }, expectedBindingRevision);
        if (!saved) return; // A concurrent disable/account switch owns the newer setting.
        if (previous?.token && previous.token !== token) await unregister(portal, previous.token).catch(() => {});
        window.dispatchEvent(new Event("browser-push-changed"));
    })().finally(() => running.delete(portal));
    running.set(portal, { task, revision }); return task;
}
async function unregister(portal, token, authorization = getPortalToken(portal)) {
    if (!authorization) return;
    await axios.post(`${api.defaults.baseURL}/browser-push/unregister`, { portal, token }, { headers: { Authorization: `Bearer ${authorization}` }, timeout: 3000 });
}
export async function disableBrowserPush(portal) {
    const revision = crypto.randomUUID();
    localStorage.setItem(`browserPushRevision:${portal}`, revision);
    const authorization = getPortalToken(portal);
    const store = "indexedDB" in window ? await storeModule() : null;
    const binding = store ? await store.bindingStore(portal) : null;
    if (store) await store.bindingStore(portal, { portal, enabled: false, revision }); // Block locally even if the server is temporarily unreachable.
    if ("serviceWorker" in navigator) {
        const registration = await navigator.serviceWorker.getRegistration("/school-push/");
        registration?.active?.postMessage({ kind: "school-clear-notifications", portal });
    }
    window.dispatchEvent(new Event("browser-push-changed"));
    if (binding?.token) await unregister(portal, binding.token, authorization).catch(() => {});
}
