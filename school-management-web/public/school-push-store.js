// Shared by the page and the push worker. No passwords or access tokens are stored here.
const databaseName = "school-browser-alerts";
export const portals = ["staff", "student", "parent"];
export function notificationPath(portal) {
    return portal === "staff" ? "/notifications" : portals.includes(portal) ? `/${portal}/notifications` : null;
}
export function matchesBinding(data, binding) {
    return Boolean(data?.kind === "school-update" && portals.includes(data.portal) && binding?.enabled && binding.portal === data.portal && binding.userId === data.recipientUserId);
}
export function bindingStore(portal, value, expectedRevision = undefined) {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore("bindings", { keyPath: "portal" });
        request.onerror = () => reject(new Error("Browser alert storage is unavailable."));
        request.onsuccess = () => {
            const db = request.result;
            const tx = db.transaction("bindings", value === undefined ? "readonly" : "readwrite");
            const store = tx.objectStore("bindings");
            let result;
            if (value !== undefined && expectedRevision !== undefined) {
                const read = store.get(portal);
                read.onsuccess = () => {
                    if ((read.result?.revision ?? null) !== expectedRevision) { result = false; return; }
                    const write = value === null ? store.delete(portal) : store.put(value);
                    write.onsuccess = () => { result = true; };
                };
            } else {
                const operation = value === undefined ? store.get(portal) : value === null ? store.delete(portal) : store.put(value);
                operation.onsuccess = () => { result = operation.result; };
            }
            tx.oncomplete = () => { db.close(); resolve(result ?? null); };
            tx.onerror = tx.onabort = () => { db.close(); reject(new Error("Unable to update browser alert settings.")); };
        };
    });
}
