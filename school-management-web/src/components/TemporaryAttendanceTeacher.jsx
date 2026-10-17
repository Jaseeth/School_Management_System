import { useEffect, useRef, useState } from "react";
import api from "../api/axios";

export default function TemporaryAttendanceTeacher() {
    const [options, setOptions] = useState(null);
    const [selection, setSelection] = useState("");
    const [teacher, setTeacher] = useState("");
    const [reason, setReason] = useState("");
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [saving, setSaving] = useState(false);
    const busy = useRef(false);
    useEffect(() => {
        let active = true;
        api.get("/attendance/windows/management-options").then((response) => {
            if (active) setOptions(response.data);
        }).catch((err) => { if (active) setError(err?.response?.data?.message || "Unable to load temporary teacher options."); });
        return () => { active = false; };
    }, []);
    async function assign(event) {
        event.preventDefault();
        const schoolClass = options?.classes.find((item) => `${item.academicYearId}:${item.schoolClassId}` === selection);
        if (!schoolClass || !teacher || busy.current) return;
        busy.current = true; setSaving(true); setError(""); setSuccess("");
        try {
            const response = await api.post("/class-teacher/temporary/assign", {
                academicYearId: schoolClass.academicYearId, schoolClassId: schoolClass.schoolClassId,
                staffId: Number(teacher), reason: reason.trim() || null,
            });
            setSuccess(`${response.data.message} Access expires at ${new Date(response.data.expiresAt).toLocaleTimeString()}. The teacher can refresh Class Attendance to see the class.`);
        } catch (err) { setError(err?.response?.data?.message || "Unable to assign temporary attendance access."); }
        finally { busy.current = false; setSaving(false); }
    }
    const input = "mt-2 w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
    return <form onSubmit={assign} className="mt-6 rounded-2xl border border-slate-200 bg-white p-5">
        <h2 className="text-lg font-semibold text-slate-950">Temporary attendance teacher</h2>
        <p className="mt-2 text-sm text-slate-500">Assign an active teacher to a class in your section for 2 hours. Attendance deadlines and correction approval still apply.</p>
        {error && <p role="alert" className="mt-3 text-sm text-red-700">{error}</p>}
        {success && <p role="status" className="mt-3 text-sm text-green-700">{success}</p>}
        <fieldset disabled={!options || saving} className="mt-4 grid gap-4 sm:grid-cols-2">
            <label className="text-sm font-medium text-slate-700">Class<select required value={selection} onChange={(event) => setSelection(event.target.value)} className={input}><option value="">Select class</option>{options?.classes.map((item) => <option key={`${item.academicYearId}:${item.schoolClassId}`} value={`${item.academicYearId}:${item.schoolClassId}`}>{item.label}</option>)}</select></label>
            <label className="text-sm font-medium text-slate-700">Teacher<select required value={teacher} onChange={(event) => setTeacher(event.target.value)} className={input}><option value="">Select teacher</option>{options?.teachers.map((item) => <option key={item.id} value={item.id}>{item.staffNumber} — {item.fullName}</option>)}</select></label>
            <label className="text-sm font-medium text-slate-700 sm:col-span-2">Reason (optional)<input value={reason} onChange={(event) => setReason(event.target.value)} maxLength={500} className={input} /></label>
            <button disabled={!selection || !teacher || saving} className="w-fit rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50">{saving ? "Assigning…" : "Assign temporary teacher"}</button>
        </fieldset>
    </form>;
}
