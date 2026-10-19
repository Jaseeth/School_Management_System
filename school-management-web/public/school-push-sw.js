import { bindingStore, matchesBinding, notificationPath } from "./school-push-store.js";

self.addEventListener("install", event => event.waitUntil(self.skipWaiting()));
// Dedicated push scope, no fetch interception or page caching.
self.addEventListener("push", event => {
    event.waitUntil((async () => {
        let payload;
        try { payload = event.data?.json(); } catch { return; }
        const data = payload?.data;
        const path = notificationPath(data?.portal);
        if (!path) return;
        const binding = await bindingStore(data.portal);
        if (!matchesBinding(data, binding)) return; // Signed out or replaced account: discard delayed messages.
        const windows = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
        for (const client of windows) client.postMessage({ kind: "school-browser-update", portal: data.portal, recipientUserId: data.recipientUserId });
        // Keep school/leave/student details off the lock screen. Open the authenticated inbox for details.
        await self.registration.showNotification("School Management", {
            body: "You have a new school update. Open your notifications to view it.",
            icon: "/favicon.svg", tag: `school-update-${data.portal}`,
            data: { portal: data.portal, recipientUserId: data.recipientUserId }
        });
    })());
});
self.addEventListener("notificationclick", event => {
    event.notification.close();
    event.waitUntil((async () => {
        const data = event.notification.data;
        const binding = data?.portal ? await bindingStore(data.portal) : null;
        if (!binding?.enabled || binding.userId !== data?.recipientUserId) return;
        const path = notificationPath(data.portal);
        if (path) await self.clients.openWindow(new URL(path, self.location.origin).href);
    })());
});
self.addEventListener("message", event => {
    if (event.data?.kind !== "school-clear-notifications") return;
    event.waitUntil((async () => {
        const notifications = await self.registration.getNotifications({ tag: `school-update-${event.data.portal}` });
        notifications.forEach(n => n.close());
    })());
});
