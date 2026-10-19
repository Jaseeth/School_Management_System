import { useCallback, useEffect, useRef, useState } from "react";
import { AssessmentList } from "../exams/ExamsTestsPage";
import SchoolEvents, { EventList } from "./SchoolEvents";
import api from "../../api/axios";
import PublicHolidayImport from "./PublicHolidayImport";
import GovernmentCalendarImport from "./GovernmentCalendarImport";

const control = "mt-1 block rounded-xl border border-slate-200 bg-white p-3 text-sm";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
const weekdays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const names = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
const iso = (year, month, day) => `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;

export default function SchoolCalendarPage({ student = false, parent = false }) {
    const [year, setYear] = useState(new Date().getFullYear());
    const [month, setMonth] = useState(new Date().getMonth());
    const [assessments, setAssessments] = useState([]);
    const [eventData, setEventData] = useState({ events: [], canManage: false });
    const [data, setData] = useState(null);
    const [calendar, setCalendar] = useState(null);
    const [selected, setSelected] = useState("");
    const [holiday, setHoliday] = useState({ date: "", toDate: "", name: "" });
    const [remove, setRemove] = useState(null);
    const [tab, setTab] = useState(() => ["events", "assessments"].includes(new URLSearchParams(window.location.search).get("view")) ? new URLSearchParams(window.location.search).get("view") : "calendar");
    const [method, setMethod] = useState("manual");
    const [quickAdd, setQuickAdd] = useState(false);
    const dialog = useRef(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const load = useCallback(async signal => {
        const [assessmentResponse, eventResponse, r] = await Promise.all([api.get("/assessments", { signal, params: { year } }), api.get("/school-events", { signal, params: { year } }), api.get(parent ? "/parent-guardians/my/school-calendar" : student ? "/student-portal/school-calendar" : "/staff-leave/school-calendar", { signal, params: { year } })]);
        if (!signal?.aborted) { setAssessments(assessmentResponse.data.schedules); setEventData(eventResponse.data); setData(r.data); setCalendar({ year, countryCode: r.data.countryCode, region: r.data.region, weekendDays: r.data.weekendDays, schoolStart: r.data.schoolStart, schoolEnd: r.data.schoolEnd }); setError(""); }
    }, [year, student, parent]);
    useEffect(() => { const controller = new AbortController(); Promise.resolve().then(() => load(controller.signal)).catch(err => { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load school calendar."); }); return () => controller.abort(); }, [load]);
    useEffect(() => {
        if (quickAdd && dialog.current && !dialog.current.open) dialog.current.showModal();
        else if (!quickAdd && dialog.current?.open) dialog.current.close();
    }, [quickAdd]);
    async function saveHoliday(event) {
        event.preventDefault();
        if (await save(() => api.post("/staff-leave/settings/holidays", holiday))) {
            setMonth(Number(holiday.date.slice(5, 7)) - 1); setTab("calendar"); setQuickAdd(false); setSelected(holiday.date);
        }
    }
    function chooseDate(value) {
        const item = byDate.get(value);
        setSelected(value); setHoliday({ date: value, toDate: value, name: item?.holiday || "" }); setRemove(null); setError("");
        if (data.canManage) setQuickAdd(true);
    }
    async function save(work) {
        if (busy) return; setBusy(true); setError(""); setMessage("");
        try { const r = await work(); setMessage(r.data.message); setRemove(null); await load(); return true; }
        catch (err) { setError(err.response?.data?.message || "Unable to update school calendar."); return false; }
        finally { setBusy(false); }
    }
    function changeYear(value) { setYear(Number(value)); setQuickAdd(false); setData(null); setSelected(""); setRemove(null); setHoliday({ date: "", toDate: "", name: "" }); }
    function move(offset) { const next = month + offset; if (next < 0) { changeYear(year - 1); setMonth(11); } else if (next > 11) { changeYear(year + 1); setMonth(0); } else setMonth(next); }
    const first = new Date(year, month, 1).getDay();
    const length = new Date(year, month + 1, 0).getDate();
    const byDate = new Map(data?.days.map(d => [d.date, d]));
    const monthEvents = eventData.events.filter(e => e.fromDate <= iso(year, month, length) && e.throughDate >= iso(year, month, 1));
    const chosen = byDate.get(selected);
    const monthSchoolDays = data?.days.filter(d => d.date.startsWith(`${year}-${String(month + 1).padStart(2, "0")}`) && d.schoolDay).length;
    return <main className="space-y-5"><header><h1 className="text-2xl font-bold">School Calendar</h1><p className="mt-2 text-sm text-slate-500">School days and holidays for staff and students.</p></header>
        <label className="block text-sm">Calendar year<input type="number" min="1900" max="2100" disabled={busy} className={control} value={year} onChange={e => changeYear(e.target.value)} /></label>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{error}</p>}{message && <p role="status" className="rounded-xl bg-blue-50 p-3 text-blue-700">{message}</p>}
        {!data ? !error && <p>Loading calendar…</p> : <>{!data.configured && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800">Calendar not yet saved: totals currently assume the full year with Saturday/Sunday weekends. Admin should confirm the school dates and holidays.</p>}
            <nav aria-label="School calendar views" className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3">{[["calendar", "Calendar"], ["events", "School Events"], ["assessments", "Exams & Tests"], ...(data.canManage ? [["holidays", "Add holidays"], ["settings", "Calendar settings"]] : [])].map(([id, label]) => <button key={id} disabled={busy} aria-current={tab === id ? "page" : undefined} className={tab === id ? button : "rounded-xl px-4 py-3 text-sm font-semibold text-slate-600 hover:bg-slate-50"} onClick={() => { setTab(id); setRemove(null); setError(""); }}>{label}</button>)}</nav>
            {tab === "assessments" && <section className="space-y-3"><h2 className="text-lg font-semibold">Exams &amp; Tests · {year}</h2><AssessmentList schedules={assessments} /></section>}
            {tab === "events" && <SchoolEvents key={year} year={year} events={eventData.events} canManage={eventData.canManage} onChanged={load} />}
            {tab === "holidays" && data.canManage && <section className="space-y-3"><h2 className="text-lg font-semibold">How would you like to add holidays?</h2><div className="grid gap-3 sm:grid-cols-3">{[["manual", "Add manually", "One holiday or a vacation range"], ["public", "Load public holidays", "Get national holidays for your country"], ["file", "Upload government calendar", "Preview an approved Excel or CSV file"]].map(([id, label, help]) => <button key={id} disabled={busy} aria-pressed={method === id} className={`rounded-xl border p-4 text-left ${method === id ? "border-blue-500 bg-blue-50" : "border-slate-200 bg-white"}`} onClick={() => { setMethod(id); setError(""); }}><strong className="block text-sm">{label}</strong><span className="mt-1 block text-xs text-slate-500">{help}</span></button>)}</div><p className="text-sm text-slate-500">All holidays apply to both staff and students.</p></section>}
            {tab === "calendar" && <div className="flex flex-wrap gap-3">{[["School days this year", data.summary.schoolDays], ["School days this month", monthSchoolDays], ["Total holidays this year", data.summary.holidays]].map(([label, value]) => <div key={label} className="rounded-xl border border-slate-200 bg-white p-4"><p className="text-xs text-slate-500">{label}</p><strong className="text-2xl">{value}</strong></div>)}</div>}
            {tab === "holidays" && method === "public" && data.canManage && <PublicHolidayImport key={`${year}:${data.countryCode}`} year={year} country={data.countryCode} onImported={load} />}
            {tab === "holidays" && method === "file" && data.canManage && <GovernmentCalendarImport key={`file:${year}:${data.countryCode}`} year={year} onImported={load} />}
            {tab === "calendar" && <section className="rounded-2xl border border-slate-200 bg-white p-3 sm:p-6"><div className="mb-4 flex flex-wrap items-center justify-between gap-3"><button disabled={busy || year === 1900 && month === 0} className={button} onClick={() => move(-1)}>Previous</button><label className="text-lg font-semibold"><select aria-label="Calendar month" value={month} onChange={e => setMonth(Number(e.target.value))}>{Array.from({ length: 12 }, (_, m) => <option value={m} key={m}>{new Date(year, m, 1).toLocaleString(undefined, { month: "long" })} {year}</option>)}</select></label><button disabled={busy || year === 2100 && month === 11} className={button} onClick={() => move(1)}>Next</button></div>
                <div className="grid grid-cols-7 gap-1 sm:gap-2">{weekdays.map(name => <div key={name} className="p-2 text-center text-xs font-semibold text-slate-500">{name}</div>)}{Array.from({ length: first }, (_, i) => <div key={`blank${i}`} />)}{Array.from({ length }, (_, i) => { const day = i + 1; const value = iso(year, month, day); const item = byDate.get(value); const dayEvents = [...eventData.events.filter(e => !e.isCancelled && e.fromDate <= value && e.throughDate >= value), ...assessments.filter(e => !e.isCancelled && e.date === value)]; const style = item?.holiday ? "border-rose-200 bg-rose-50 text-rose-800" : item?.schoolDay ? "border-emerald-100 bg-emerald-50 text-emerald-900" : "border-slate-200 bg-slate-50 text-slate-500"; return <button key={value} disabled={busy} aria-pressed={selected === value} aria-label={`${value}: ${item?.holiday || (item?.schoolDay ? "School day" : item?.outsideSchoolPeriod ? "School closed" : "Weekend")}${dayEvents.length ? `, ${dayEvents.length} events` : ""}`} className={`min-h-20 min-w-0 rounded-lg border p-1 text-left sm:min-h-28 sm:p-3 ${style} ${selected === value ? "ring-2 ring-blue-600" : ""}`} onClick={() => chooseDate(value)}><strong className="text-sm sm:text-lg">{day}</strong><span className="mt-1 block break-words text-[10px] sm:text-xs">{item?.holiday || (item?.schoolDay ? "School day" : item?.outsideSchoolPeriod ? "School closed" : "Weekend")}</span>{dayEvents.length > 0 && <span className="mt-1 block text-[10px] font-semibold text-blue-700 sm:text-xs">{dayEvents.length} event{dayEvents.length > 1 ? "s" : ""}</span>}</button>; })}</div>
                <p className="mt-4 text-xs text-slate-500">Green: school day · Pink: holiday · Grey: weekend / school closed. {data.canManage ? "Click a date to add or edit a holiday." : "Click a date to view its details."}</p>
                {chosen && !data.canManage && <div className="mt-4 rounded-xl border border-slate-200 p-3 text-sm"><strong>{selected}</strong><p>{chosen.holiday || (chosen.schoolDay ? "School day" : chosen.outsideSchoolPeriod ? "Outside school opening period" : "Weekly holiday")}</p>{chosen.weekend && chosen.holiday && <p>This holiday also falls on a weekend.</p>}</div>}
            </section>}
            {tab === "calendar" && <section className="space-y-3"><h2 className="text-lg font-semibold">Exams &amp; tests this month</h2><AssessmentList schedules={assessments.filter(e => e.date.startsWith(`${year}-${String(month + 1).padStart(2, "0")}`))} /></section>}
            {tab === "calendar" && <section className="space-y-3"><h2 className="text-lg font-semibold">Events this month</h2><EventList events={monthEvents} /></section>}
            {tab === "settings" && data.canManage && calendar && <form className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5" onSubmit={e => { e.preventDefault(); save(() => api.put("/staff-leave/settings/calendar", calendar)).then(saved => { if (saved) setTab("calendar"); }); }}><h2 className="font-semibold">Set school dates and weekends</h2><div className="flex flex-wrap gap-3"><label className="text-sm">Country code<input required minLength={2} maxLength={2} placeholder="LK" className={control} value={calendar.countryCode} onChange={e => setCalendar({ ...calendar, countryCode: e.target.value.toUpperCase() })} /></label><label className="text-sm">Region (optional)<input maxLength={100} className={control} value={calendar.region} onChange={e => setCalendar({ ...calendar, region: e.target.value })} /></label>{[["schoolStart", "School opens"], ["schoolEnd", "School closes"]].map(([key, label]) => <label key={key} className="text-sm">{label}<input required type="date" min={`${year}-01-01`} max={`${year}-12-31`} className={control} value={calendar[key]} onChange={e => setCalendar({ ...calendar, [key]: e.target.value })} /></label>)}</div><fieldset disabled={busy}><legend className="text-sm">Weekly holidays</legend><div className="mt-2 flex flex-wrap gap-4">{names.map((name, d) => <label key={d} className="flex items-center gap-2 text-sm"><input type="checkbox" checked={calendar.weekendDays.includes(d)} onChange={e => setCalendar({ ...calendar, weekendDays: e.target.checked ? [...calendar.weekendDays, d] : calendar.weekendDays.filter(v => v !== d) })} />{name}</label>)}</div></fieldset><p className="text-xs text-slate-500">Use Add holidays for term breaks. These non-school dates are also excluded from staff leave calculations.</p><button disabled={busy} className={button}>Save settings</button></form>}
            {tab === "holidays" && method === "manual" && data.canManage && <form className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5" onSubmit={saveHoliday}><h2 className="font-semibold">Add / update public holiday or school break</h2><p className="text-sm text-slate-500">Enter one holiday or a vacation range. Both staff and students are included. The saved dates will appear in the calendar.</p><div className="flex flex-wrap gap-3">{[["date", "From"], ["toDate", "Through"]].map(([key, label]) => <label key={key} className="text-sm">{label}<input required type="date" min={`${year}-01-01`} max={`${year}-12-31`} className={control} value={holiday[key]} onChange={e => setHoliday({ ...holiday, [key]: e.target.value })} /></label>)}<label className="text-sm">Holiday / break name<input required maxLength={200} className={control} value={holiday.name} onChange={e => setHoliday({ ...holiday, name: e.target.value })} /></label></div><button disabled={busy} className={button}>Save holiday</button></form>}
            {tab === "calendar" && <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5"><h2 className="font-semibold">Holidays this month</h2>{data.holidays.filter(h => h.date.startsWith(`${year}-${String(month + 1).padStart(2, "0")}`)).map(h => <div key={h.id} className="flex justify-between gap-3 border-t border-slate-100 pt-3 text-sm"><span>{h.date} — {h.name}</span>{data.canManage && <button disabled={busy} className="text-red-600" onClick={() => setRemove(h)}>Remove date</button>}</div>)}{!data.holidays.some(h => h.date.startsWith(`${year}-${String(month + 1).padStart(2, "0")}`)) && <p className="text-sm text-slate-500">No holidays added this month.</p>}</section>}
            {tab === "calendar" && <p className="text-xs text-slate-500">{data.summary.totalDays} calendar days · {data.summary.nonSchoolDays} non-school days. Totals are planned school days, not attendance records.</p>}
            {remove && tab === "calendar" && <div role="alertdialog" aria-label="Remove holiday date" className="space-y-3 rounded-xl bg-amber-50 p-4"><p>Remove {remove.name} on {remove.date}? School-day totals and leave balances will recalculate.</p><button disabled={busy} className={button} onClick={() => save(() => api.delete(`/staff-leave/settings/holidays/${remove.id}`))}>Confirm removal</button><button disabled={busy} className="ml-3 text-sm" onClick={() => setRemove(null)}>Cancel</button></div>}
            {data.canManage && <dialog ref={dialog} aria-labelledby="quick-holiday-title" onCancel={e => { if (busy) e.preventDefault(); else setQuickAdd(false); }} onClose={() => setQuickAdd(false)} className="m-auto w-[calc(100%-2rem)] max-w-lg rounded-2xl bg-white p-6 shadow-xl backdrop:bg-slate-950/40"><form onSubmit={saveHoliday} className="space-y-4"><h2 id="quick-holiday-title" className="text-lg font-semibold">{chosen?.holiday ? "Edit holiday" : "Add holiday"}</h2><p className="text-sm text-slate-500">Choose one date or a range. Applies to both staff and students.</p>{error && <p role="alert" className="text-sm text-red-700">{error}</p>}<fieldset disabled={busy} className="space-y-4"><div className="grid gap-3 sm:grid-cols-2">{[["date", "From date"], ["toDate", "Through date"]].map(([key, label]) => <label key={key} className="text-sm">{label}<input required type="date" min={key === "toDate" ? holiday.date : `${year}-01-01`} max={`${year}-12-31`} className={`${control} w-full`} value={holiday[key]} onChange={e => setHoliday({ ...holiday, [key]: e.target.value })} /></label>)}</div><label className="block text-sm">Holiday or break name<input required maxLength={200} placeholder="e.g. Ramadan vacation" className={`${control} w-full`} value={holiday.name} onChange={e => setHoliday({ ...holiday, name: e.target.value })} /></label></fieldset><div className="flex gap-3"><button disabled={busy || !holiday.name.trim()} className={button}>{busy ? "Saving…" : "Save holiday"}</button><button type="button" disabled={busy} className="rounded-xl border border-slate-200 px-4 py-3 text-sm" onClick={() => setQuickAdd(false)}>Cancel</button></div></form></dialog>}
        </>}
    </main>;
}
