import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";
import { indexedDB } from "fake-indexeddb";
import { bindingStore, matchesBinding, notificationPath } from "../public/school-push-store.js";

globalThis.indexedDB = indexedDB;
const delivery = { kind: "school-update", portal: "student", recipientUserId: "student-1" };
const binding = { enabled: true, portal: "student", userId: "student-1", token: "test-token" };

test("consent is isolated by portal and deleting one binding preserves the others", async () => {
    await bindingStore("student", binding);
    await bindingStore("parent", { enabled: true, portal: "parent", userId: "parent-1" });
    assert.equal((await bindingStore("student")).userId, "student-1");
    await bindingStore("student", null);
    assert.equal(await bindingStore("student"), null);
    assert.equal((await bindingStore("parent")).userId, "parent-1");
});
test("signed-out, replaced and wrong-portal deliveries cannot match a binding", () => {
    assert.equal(matchesBinding(delivery, binding), true);
    for (const invalid of [null, { ...binding, enabled: false }, { ...binding, userId: "other" }, { ...binding, portal: "parent" }]) assert.equal(matchesBinding(delivery, invalid), false);
    assert.equal(notificationPath("https://untrusted.example"), null);
    assert.equal(notificationPath("staff"), "/notifications");
});
test("a delayed token renewal cannot overwrite a newer sign-out or replacement account", async () => {
    await bindingStore("staff", { portal: "staff", enabled: true, userId: "old", revision: "before-signout" });
    await bindingStore("staff", { portal: "staff", enabled: false, revision: "signed-out" });
    assert.equal(await bindingStore("staff", { portal: "staff", enabled: true, userId: "old", revision: "delayed" }, "before-signout"), false);
    assert.equal((await bindingStore("staff")).enabled, false);
    assert.equal(await bindingStore("staff", { portal: "staff", enabled: true, userId: "new", revision: "replacement" }, "signed-out"), true);
    assert.equal((await bindingStore("staff")).userId, "new");
});
async function worker(currentBinding = binding) {
    const handlers = {}; const shown = []; const opened = []; const messages = [];
    const self = { addEventListener: (name, handler) => { handlers[name] = handler; }, skipWaiting: async () => {}, location: { origin: "https://school.example" }, registration: { showNotification: async (...args) => shown.push(args), getNotifications: async () => [] }, clients: { matchAll: async () => [{ postMessage: data => messages.push(data) }], openWindow: async url => opened.push(url) } };
    const source = (await readFile(new URL("../public/school-push-sw.js", import.meta.url), "utf8")).replace(/^import[^\n]+\n/, "");
    vm.runInNewContext(source, { self, URL, bindingStore: async () => currentBinding, matchesBinding, notificationPath });
    async function event(name, input) { let work; handlers[name]({ ...input, waitUntil: promise => { work = promise; } }); await work; }
    return { event, shown, opened, messages };
}
test("real worker discards malformed and delayed messages after logout", async () => {
    const signedOut = await worker(null);
    await signedOut.event("push", { data: { json: () => ({ data: delivery }) } }); assert.equal(signedOut.shown.length, 0);
    await signedOut.event("push", { data: { json: () => { throw new Error("malformed"); } } }); assert.equal(signedOut.shown.length, 0);
    const replaced = await worker({ ...binding, userId: "replacement" }); await replaced.event("push", { data: { json: () => ({ data: delivery }) } }); assert.equal(replaced.shown.length, 0);
});
test("real worker displays a generic alert and opens only the matching portal", async () => {
    const active = await worker();
    await active.event("push", { data: { json: () => ({ data: { ...delivery, url: "https://untrusted.example" }, notification: { body: "Private leave reason" } }) } });
    assert.equal(active.shown.length, 1); assert.equal(active.messages.length, 1);
    assert.equal(JSON.stringify(active.shown).includes("Private leave reason"), false);
    await active.event("notificationclick", { notification: { data: delivery, close() {} } });
    assert.deepEqual(active.opened, ["https://school.example/student/notifications"]);
    const signedOut = await worker(null); await signedOut.event("notificationclick", { notification: { data: delivery, close() {} } }); assert.equal(signedOut.opened.length, 0);
});
