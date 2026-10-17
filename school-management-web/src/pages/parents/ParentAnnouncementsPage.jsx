import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../../api/axios";

export default function ParentAnnouncementsPage() {
    const [items, setItems] = useState([]);
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(true);
    useEffect(() => {
        let active = true;
        api.get("/school-announcements/my/parent").then(({ data }) => { if (active) setItems(data.announcements); })
            .catch(err => { if (active) setError(err?.response?.data?.message || "Unable to load announcements."); })
            .finally(() => { if (active) setLoading(false); });
        return () => { active = false; };
    }, []);
    return <main className="min-h-screen bg-slate-50 px-6 py-10"><div className="mx-auto max-w-5xl space-y-6">
        <Link to="/parent/dashboard" className="text-sm text-blue-600">Back to My Children</Link>
        <h1 className="text-3xl font-bold text-slate-950">Announcements</h1>
        {error && <p role="alert" className="text-red-600">{error}</p>}
        {loading ? <p>Loading announcements…</p> : items.map(item => <article key={item.id} className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><h2 className="font-semibold">{item.title}</h2><p className="mt-3 whitespace-pre-wrap text-sm text-slate-600">{item.message}</p></article>)}
        {!loading && !error && !items.length && <p>No announcements available.</p>}
    </div></main>;
}
