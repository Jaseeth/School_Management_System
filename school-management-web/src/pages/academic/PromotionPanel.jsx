import { useCallback, useEffect, useRef, useState } from "react";
import api from "../../api/axios";
import { Link } from "react-router-dom";

const input = "w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
const defaultRepeatReason = "Repeat grade after school review";
const button = "rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50";
export default function PromotionPanel() {
    const exceptionForm = useRef(null);
    const [data, setData] = useState(null);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [busy, setBusy] = useState(false);
    const [mappings, setMappings] = useState({});
    const [year, setYear] = useState("");
    const [mode, setMode] = useState("RepeatNextYear");
    const [review, setReview] = useState(false);
    const [undo, setUndo] = useState(null);
    const [target, setTarget] = useState("");
    const [search, setSearch] = useState("");
    const [sourceClass, setSourceClass] = useState("");
    const [selected, setSelected] = useState([]);
    const [reason, setReason] = useState("");
    const [issues, setIssues] = useState([]);
    const load = useCallback(async () => {
        const { data: result } = await api.get("/annual-promotion");
        setData(result);
        setMappings(Object.fromEntries(result.rules.map(rule => [rule.fromSchoolClassId, rule.graduate ? "graduate" : String(rule.toSchoolClassId)])));
    }, []);
    useEffect(() => {
        const controller = new AbortController();
        api.get("/annual-promotion", { signal: controller.signal }).then(({ data: result }) => {
            setData(result);
            setMappings(Object.fromEntries(result.rules.map(rule => [rule.fromSchoolClassId, rule.graduate ? "graduate" : String(rule.toSchoolClassId)])));
        }).catch(err => { if (!controller.signal.aborted) setError(err.response?.data?.message || "Unable to load promotion setup."); });
        return () => controller.abort();
    }, []);
    async function action(work) {
        if (busy) return;
        setBusy(true); setError(""); setMessage("");
        try { const result = await work(); setMessage(result.data.message); await load(); }
        catch (err) { setError(err.response?.data?.message || "Unable to save promotion changes."); }
        finally { setBusy(false); }
    }
    if (!data) return <p role="status">{error || "Loading promotion setup…"}</p>;
    const className = id => data.classes.find(item => item.id === id)?.name || "Unassigned";
    const upcomingYears = data.years.filter(item => item.isActive && item.startDate.slice(0, 10) > data.today).sort((a, b) => a.startDate.localeCompare(b.startDate));
    const currentYears = data.years.filter(item => item.isActive && item.startDate.slice(0, 10) <= data.today && item.endDate.slice(0, 10) >= data.today);
    const automaticYear = mode === "RepeatNextYear" ? upcomingYears[0] : currentYears.length === 1 ? currentYears[0] : null;
    const selectedYear = year || (automaticYear ? String(automaticYear.id) : "");
    const chosenYear = data.years.find(item => String(item.id) === selectedYear);
    const priorYear = chosenYear && data.years.filter(item => item.startDate < chosenYear.startDate).sort((a, b) => b.startDate.localeCompare(a.startDate))[0];
    const eligible = data.students.map(student => ({
        ...student,
        placement: mode === "RepeatNextYear"
            ? student.enrollments.find(enrollment => enrollment.isCurrent && enrollment.academicYearId === priorYear?.id)
            : student.enrollments.find(enrollment => enrollment.isCurrent && enrollment.academicYearId === chosenYear?.id)
                || (student.isGraduated && student.enrollments.find(enrollment => enrollment.academicYearId === priorYear?.id)),
    })).filter(student => student.placement);
    const sourceClasses = [...new Set(eligible.map(student => student.placement.schoolClassId))]
        .map(id => ({ id, name: className(id) })).sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }));
    const visible = eligible.filter(student => sourceClass && String(student.placement.schoolClassId) === sourceClass)
        .filter(student => `${student.fullName} ${student.indexNumber}`.toLowerCase().includes(search.toLowerCase()));
    const source = data.classes.find(item => String(item.id) === sourceClass);
    const targets = data.classes.filter(item => source && (mode === "RepeatNextYear" ? item.grade === source.grade : Number(item.grade) <= Number(source.grade)));
    const years = data.years.filter(item => item.isActive && item.endDate.slice(0, 10) >= data.today && (mode === "RepeatNextYear" ? item.startDate.slice(0, 10) > data.today : item.startDate.slice(0, 10) <= data.today));
    const reset = () => { setYear(""); setSourceClass(""); setTarget(""); setSelected([]); setReason(""); setReview(false); setSearch(""); };
    const missingMappings = data.classes.filter(item => data.students.some(student => !student.isGraduated && student.enrollments.some(enrollment => enrollment.isCurrent && enrollment.schoolClassId === item.id)) && !data.rules.some(rule => rule.fromSchoolClassId === item.id));
    const nextSourceYear = upcomingYears[0] && data.years.filter(item => item.startDate < upcomingYears[0].startDate).sort((a, b) => b.startDate.localeCompare(a.startDate))[0];
    const datesOverlap = nextSourceYear && nextSourceYear.endDate.slice(0, 10) >= upcomingYears[0].startDate.slice(0, 10);
    const repeatReason = mode === "RepeatNextYear" ? reason.trim() || defaultRepeatReason : reason.trim();
    const savedRepeats = data.exceptions.filter(item => upcomingYears.some(value => value.id === item.academicYearId));
    const unchanged = mode === "CorrectPlacement" && eligible.some(student => selected.includes(student.id) && !student.isGraduated && String(student.placement.schoolClassId) === target);
    return <div className="space-y-6">
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-700">{error}</p>}
        {message && <p role="status" className="rounded-xl bg-blue-50 p-4 text-blue-700">{message}</p>}
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6">
            <h2 className="text-lg font-semibold">Students move up automatically</h2>
            <p className="text-sm leading-6 text-slate-600">When the new academic year starts, students move to their next class. You only need to select students who must stay in the same grade.</p>
            <dl className="grid gap-4 border-y border-slate-100 py-4 text-sm md:grid-cols-3">
                <div><dt className="text-slate-500">Next academic year</dt><dd className="mt-1 font-semibold">{upcomingYears[0]?.name || "Not created yet"}</dd></div>
                <div><dt className="text-slate-500">Promotion starts</dt><dd className="mt-1 font-semibold">{upcomingYears[0]?.startDate.slice(0, 10) || "Set the next year’s dates"}</dd></div>
                <div><dt className="text-slate-500">Students scheduled to repeat</dt><dd className="mt-1 font-semibold">{savedRepeats.length}</dd></div>
            </dl>
            {!upcomingYears.length && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800">First, <Link className="font-semibold underline" to="/academic-setup">create the next academic year</Link>. Then you can select any students who must repeat.</p>}
            {currentYears.length !== 1 && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800">Check Academic Setup: exactly one active academic year must cover today.</p>}
            {datesOverlap && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800">The next academic year must start after the previous year ends. Update the dates in Academic Setup.</p>}
            {!!missingMappings.length && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800">Setup needed for {missingMappings.length} class(es). Open “One-time class setup” below to choose their next placement.</p>}
            <p className="text-sm text-slate-500">The system must be running to promote students. A student without a saved next placement stays pending. Attendance and results are kept.</p>
            <details id="promotion-class-setup" className="rounded-xl border border-slate-200 p-4">
                <summary className="cursor-pointer font-semibold">One-time class setup — choose where each class goes next</summary>
                <p className="my-3 text-sm text-slate-500">For example: Grade 11 A → Grade 12 A. Save each class once. Choose “Complete school” only for the final grade. Create missing grades/classes in <Link className="text-blue-600 underline" to="/academic-setup">Academic Setup</Link>.</p>
            <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr><th className="p-3">Current class</th><th className="p-3">Next placement</th><th /></tr></thead><tbody>{data.classes.map(item => <tr key={item.id} className="border-t border-slate-100"><td className="p-3">{item.name}</td><td className="p-3"><select aria-label={`Next placement for ${item.name}`} className={input} value={mappings[item.id] || ""} disabled={busy} onChange={event => setMappings({ ...mappings, [item.id]: event.target.value })}><option value="">Not configured</option><option value="graduate">Complete school</option>{data.classes.filter(next => next.id !== item.id && (!/^\d+$/.test(item.grade) || !/^\d+$/.test(next.grade) || Number(next.grade) === Number(item.grade) + 1)).map(next => <option key={next.id} value={next.id}>{next.name}</option>)}</select></td><td className="p-3"><button disabled={busy} className={button} onClick={() => action(() => api.put("/annual-promotion/mapping", { fromSchoolClassId: item.id, toSchoolClassId: mappings[item.id] && mappings[item.id] !== "graduate" ? Number(mappings[item.id]) : null, graduate: mappings[item.id] === "graduate", enabled: !!mappings[item.id] }))}>Save mapping</button></td></tr>)}</tbody></table></div>
            </details>
        </section>
        <section ref={exceptionForm} className="scroll-mt-6 space-y-4 rounded-2xl border border-slate-200 bg-white p-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 className="text-lg font-semibold">{mode === "RepeatNextYear" ? "Who should repeat next year?" : "Correct a student’s current class"}</h2>
                {mode === "CorrectPlacement" && <button disabled={busy || review} className="text-sm font-semibold text-blue-600" onClick={() => { setMode("RepeatNextYear"); reset(); }}>Back to repeat students</button>}
            </div>
            <p className="text-sm text-slate-500">{mode === "RepeatNextYear" ? "If everyone should move up, leave this list empty. Selected students stay in their current grade next year." : "Use this only to fix a current placement. Choose students and the correct class. You can undo the saved correction."}</p>
            <fieldset disabled={busy || review || !selectedYear} className="space-y-4 disabled:opacity-70">
                <div className="grid gap-4 md:grid-cols-2">
                    <label className="space-y-2 text-sm">{mode === "RepeatNextYear" ? "Repeat in academic year" : "Current academic year"}<select className={input} value={selectedYear} onChange={event => { setYear(event.target.value); setSourceClass(""); setTarget(""); setSelected([]); }}><option value="">Select academic year</option>{years.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
                    <label className="space-y-2 text-sm">1. Choose the student’s class<select disabled={!selectedYear} className={input} value={sourceClass} onChange={event => { setSourceClass(event.target.value); setSelected([]); setSearch(""); setTarget(mode === "RepeatNextYear" ? event.target.value : ""); }}><option value="">Select class</option>{sourceClasses.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
                </div>
                {!years.length && <p className="text-sm text-slate-500">{mode === "RepeatNextYear" ? "Set up the next academic year before selecting repeat students." : "Check the current academic-year dates in Academic Setup."}</p>}
                {sourceClass && <>
                    <label className="block space-y-2 text-sm">{mode === "RepeatNextYear" ? "2. Tick students who should repeat" : "2. Select students to correct"}<input className={input} value={search} onChange={event => { setSearch(event.target.value); setSelected([]); }} placeholder="Search index number or name" /></label>
                    <div className="max-h-64 overflow-y-auto rounded-xl border border-slate-200">{visible.map(student => <label key={student.id} className="flex items-center gap-3 border-b border-slate-100 p-3 text-sm last:border-0"><input type="checkbox" checked={selected.includes(student.id)} onChange={event => setSelected(event.target.checked ? [...selected, student.id] : selected.filter(id => id !== student.id))} /><span>{student.indexNumber} — {student.fullName}<span className="block text-xs text-slate-500">{className(student.placement.schoolClassId)}{student.isGraduated ? " · Completed school" : ""}</span></span></label>)}{!visible.length && <p className="p-3 text-sm text-slate-500">No eligible students found.</p>}</div>
                    <p className="text-sm text-slate-500">{selected.length} selected. Changing the class or search clears the selection.</p>
                    {mode === "RepeatNextYear" ? <p className="rounded-xl bg-slate-50 p-3 text-sm">These students will stay in <strong>{source?.name}</strong> for <strong>{chosenYear?.name}</strong>. Everyone else follows normal promotion.</p> : <label className="block space-y-2 text-sm">3. Correct class<select className={input} value={target} onChange={event => setTarget(event.target.value)}><option value="">Select destination class</option>{targets.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
                    {mode === "RepeatNextYear" ? <details><summary className="cursor-pointer text-sm text-slate-600">Add a note (optional)</summary><textarea aria-label="Repeat note" className={`${input} mt-2`} maxLength={500} value={reason} onChange={event => setReason(event.target.value)} placeholder={defaultRepeatReason} /></details> : <label className="block space-y-2 text-sm">Reason<textarea className={input} maxLength={500} value={reason} onChange={event => setReason(event.target.value)} /></label>}
                </>}
            </fieldset>
            {unchanged && <p role="status" className="rounded-xl bg-amber-50 p-3 text-sm text-amber-800">A selected student is already in this class. Choose a different placement, or use “Repeat next academic year” to schedule a repeat.</p>}
            {!review && <button className={button} disabled={busy || unchanged || !selectedYear || !target || !selected.length || !repeatReason} onClick={() => setReview(true)}>{mode === "RepeatNextYear" ? "3. Review repeat students" : "Review correction"}</button>}
            {review && <div className="space-y-3 rounded-xl border border-blue-200 bg-blue-50 p-4">
                <h3 className="font-semibold">Review before saving</h3><p className="text-sm">{mode === "RepeatNextYear" ? "Schedule a repeat for" : "Change the current placement of"} these {selected.length} student(s) to <strong>{className(Number(target))}</strong> in <strong>{chosenYear?.name}</strong>.</p>
                <ul className="max-h-40 overflow-y-auto text-sm">{eligible.filter(student => selected.includes(student.id)).map(student => <li key={student.id}>{student.indexNumber} — {student.fullName}</li>)}</ul><p className="text-sm">Reason: {repeatReason}</p>
                <div className="flex gap-3"><button className={button} disabled={busy} onClick={() => action(async () => { const result = await api.put("/annual-promotion/exceptions", { studentIds: selected, academicYearId: Number(selectedYear), schoolClassId: Number(target), reason: repeatReason, mode }); setSelected([]); setReason(""); setReview(false); return result; })}>{busy ? "Saving…" : mode === "RepeatNextYear" ? "Save repeat students" : "Apply correction"}</button><button disabled={busy} className="rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm" onClick={() => setReview(false)}>Back to edit</button></div>
            </div>}
            <h3 className="pt-4 font-semibold">Saved repeat students and corrections</h3>
            <p className="text-sm text-slate-500">Undo reverses the latest save for that student and year. Later placement changes are protected. Every reversal remains in history.</p>
            {data.exceptions.length > 0 ? <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Student", "Academic year", "Placement", "Reason", "Action"].map(title => <th key={title} className="p-3">{title}</th>)}</tr></thead><tbody>{data.exceptions.map(item => {
                const change = data.undoChanges?.find(entry => entry.studentId === item.studentId && entry.academicYearId === item.academicYearId);
                const student = data.students.find(entry => entry.id === item.studentId);
                return <tr key={item.id} className="border-t border-slate-100"><td className="p-3">{student?.indexNumber} — {student?.fullName}</td><td className="p-3">{data.years.find(value => value.id === item.academicYearId)?.name}</td><td className="p-3">{className(item.schoolClassId)}</td><td className="p-3">{item.reason}</td><td className="p-3">{!change?.undoneAt && <button disabled={busy} className="font-semibold text-blue-600" onClick={() => setUndo({ url: change ? `/annual-promotion/undo/${change.id}` : `/annual-promotion/undo-exception/${item.id}`, name: `${student?.indexNumber} — ${student?.fullName}` })}>Undo</button>}</td></tr>;
            })}</tbody></table></div> : <p className="text-sm text-slate-500">No repeat students or corrections saved. Everyone follows normal promotion once setup is complete.</p>}
            {undo && <div role="alertdialog" aria-label="Undo student exception" className="space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-4"><p className="text-sm">Undo the latest saved exception for <strong>{undo.name}</strong>? Previous settings will be restored; recorded attendance and results will be retained.</p><div className="flex gap-3"><button className={button} disabled={busy} onClick={() => action(async () => { const result = await api.post(undo.url); setUndo(null); setSelected([]); setReview(false); return result; })}>Confirm undo</button><button disabled={busy} className="rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm" onClick={() => setUndo(null)}>Cancel</button></div></div>}
        </section>
        <details className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6">
            <summary className="cursor-pointer font-semibold">Advanced: correct a current class or run a promotion check</summary>
            <p className="text-sm text-slate-500">Use corrections after a student was placed in the wrong class. The promotion check processes students only when their academic-year dates allow it.</p>
            <div className="flex flex-wrap gap-3"><button disabled={busy || review} className={button} onClick={() => { setMode("CorrectPlacement"); reset(); exceptionForm.current?.scrollIntoView({ behavior: "smooth", block: "start" }); }}>Correct current placement</button>
            <button className="rounded-xl border border-slate-300 px-4 py-3 text-sm" disabled={busy} onClick={() => action(async () => { const result = await api.post("/annual-promotion/check"); setIssues(result.data.issues || []); return { data: { message: `${result.data.message} Processed: ${result.data.processed}.` } }; })}>Run promotion check</button></div>
            {!!issues.length && <div role="status" className="rounded-xl bg-amber-50 p-4 text-sm"><p className="font-semibold">Students needing review</p>{issues.map((issue, index) => <p key={index}>{data.students.find(student => student.id === issue.studentId)?.indexNumber || issue.studentId}: {issue.message}</p>)}</div>}
        </details>
        <details className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6"><summary className="cursor-pointer font-semibold">View promotion history</summary><div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{["Student", "Action", "From", "To", "Reason"].map(title => <th key={title} className="p-3">{title}</th>)}</tr></thead><tbody>{data.history.map(item => <tr key={item.id} className="border-t border-slate-100"><td className="p-3">{item.indexNumber} — {item.fullName}</td><td className="p-3">{item.action}</td><td className="p-3">{item.fromYear} · {item.fromClass}</td><td className="p-3">{item.toYear} · {item.toClass}</td><td className="p-3">{item.reason}</td></tr>)}</tbody></table>{!data.history.length && <p className="p-3 text-sm text-slate-500">No promotion history yet.</p>}</div></details>
    </div>;
}
