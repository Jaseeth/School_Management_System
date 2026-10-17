import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../../api/axios";

export default function StaffNotificationsPage() {
    const [items, setItems] = useState([]);
    const [filter, setFilter] = useState("all");
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [reload, setReload] = useState(0);
    const [error, setError] = useState("");
    useEffect(() => {
        let active = true;
        async function load() {
            try {
                const { data } = await api.get("/notifications/my", { params: { limit: 100, unreadOnly: filter === "unread" } });
                if (active) setItems(data.notifications ?? []);
            } catch (err) { if (active) setError(err?.response?.data?.message || "Unable to load notifications."); }
            finally { if (active) setLoading(false); }
        }
        load();
        const timer = window.setInterval(load, 30000);
        return () => { active = false; window.clearInterval(timer); };
    }, [filter, reload]);
    async function mark(item) {
        if (busy) return;
        setBusy(true); setError("");
        try {
            if (item) await api.put(`/notifications/${item.id}/read`, { isRead: !item.isRead });
            else await api.put("/notifications/my/read-all");
            window.dispatchEvent(new Event("staff-notifications-changed"));
            setReload(value => value + 1);
        } catch (err) { setError(err?.response?.data?.message || "Unable to update notification."); }
        finally { setBusy(false); }
    }
    function destination(item) {
        if (item.referenceType === "SpecialClassSession") return "/special-classes";
        if (item.referenceType === "SchoolAnnouncement") return "/announcements";
        return null;
    }
    const button = "rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-semibold text-slate-700 hover:bg-blue-50 disabled:opacity-50";
    return <main className="space-y-6 p-6 lg:p-10">
        <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-bold">Notifications</h1><p className="mt-2 text-sm text-slate-500">Your account alerts, approvals and school updates.</p></div>
            <div className="flex gap-3"><button className={button} onClick={() => { setError(""); setLoading(true); setReload(value => value + 1); window.dispatchEvent(new Event("staff-notifications-changed")); }}>Refresh</button><button className={button} disabled={busy} onClick={() => mark(null)}>Mark all as read</button></div>
        </div>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-600">{error}</p>}
        <label className="block max-w-xs text-sm">Status<select className="mt-2 w-full rounded-xl border border-slate-200 bg-white p-3" value={filter} onChange={event => { setFilter(event.target.value); setLoading(true); }}><option value="all">All recent notifications</option><option value="unread">Unread</option><option value="read">Read</option></select></label>
        <p className="text-xs text-slate-500">Showing up to 100 recent notifications. New alerts refresh automatically.</p>
        {loading ? <p>Loading notifications…</p> : <div className="space-y-4">{items.filter(item => filter !== "read" || item.isRead).map(item => <article key={item.id} className={`rounded-2xl border p-5 shadow-sm ${item.isRead ? "border-slate-200 bg-white" : "border-blue-200 bg-blue-50"}`}>
            <div className="flex flex-wrap justify-between gap-2"><h2 className="font-semibold">{item.title}{!item.isRead && <span className="ml-2 text-xs text-blue-600">Unread</span>}</h2><time className="text-xs text-slate-500">{new Date(/Z$|[+-]\d{2}:\d{2}$/.test(item.createdAt) ? item.createdAt : `${item.createdAt}Z`).toLocaleString()}</time></div>
            <p className="mt-3 whitespace-pre-wrap text-sm text-slate-600">{item.message}</p>
            <div className="mt-4 flex gap-5 text-sm"><button disabled={busy} className="font-semibold text-blue-600" onClick={() => mark(item)}>{item.isRead ? "Mark as unread" : "Mark as read"}</button>{destination(item) && <Link className="text-blue-600" to={destination(item)}>View details</Link>}</div>
        </article>)}{!items.some(item => filter !== "read" || item.isRead) && <p className="text-sm text-slate-500">No notifications for this filter.</p>}</div>}
    </main>;
}
