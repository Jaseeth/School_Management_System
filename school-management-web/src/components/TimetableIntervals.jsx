import { useEffect, useState } from "react";
import { timetableManagementApi } from "../api/timetableManagementApi";

export default function TimetableIntervals({ refreshKey = 0 }) {
    const [intervals, setIntervals] = useState([]);
    const [error, setError] = useState("");
    useEffect(() => {
        let active = true;
        timetableManagementApi.getIntervals().then((response) => {
            if (active) { setIntervals(response.data); setError(""); }
        }).catch(() => {
            if (active) { setIntervals([]); setError("Unable to load intervals. Refresh the timetable to retry."); }
        });
        return () => { active = false; };
    }, [refreshKey]);
    if (error) return <p role="alert" className="my-5 text-sm text-red-700">{error}</p>;
    if (!intervals.length) return null;
    return <section aria-label="Timetable intervals" className="my-5 rounded-2xl border border-amber-200 bg-amber-50 p-5">
        <h2 className="font-semibold text-amber-950">Intervals</h2>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">{intervals.map((item) => <div key={item.id} className="rounded-xl bg-white p-3 text-sm">
            <p className="font-semibold text-amber-900">{item.day} · {item.startTime.slice(0, 5)}–{item.endTime.slice(0, 5)} · Interval</p>
            <p className="mt-1 text-slate-600">All classes · {item.academicYear.name} · {item.academicTerm.name}</p>
        </div>)}</div>
    </section>;
}
