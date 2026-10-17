import { useEffect, useRef, useState } from "react";
import { BookOpen, RefreshCw, Users } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import { academicApi } from "../../api/academicApi";
import { staffManagementApi } from "../../api/staffManagementApi";
import { teacherAssignmentsManagementApi as assignmentsApi } from "../../api/teacherAssignmentsManagementApi";

const inputClass = "mt-2 block w-full rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-slate-50";
const buttonClass = "inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50";
const emptySelection = { academicYearId: "", sectionId: "", gradeId: "", schoolClassId: "", staffId: "", subjectId: "" };

function errorMessage(error) {
    if (error?.response?.status === 401) return "Your session has expired. Sign in again.";
    if (error?.response?.status === 403) return "Your account does not have permission for this assignment action.";
    const data = error?.response?.data;
    if (data?.message) return data.message;
    if (data?.errors) return Object.values(data.errors).flat().join(" ");
    return "Unable to complete the request. Check that the API is running, then refresh and try again.";
}

function asList(response) {
    if (!Array.isArray(response.data)) throw new Error("Unexpected assignment lookup response.");
    return response.data;
}

function sectionOf(row) {
    return row.schoolClass.grade.section ?? row.schoolClass.section;
}

export default function TeacherAssignmentsManagementPage() {
    const { user } = useAuth();
    const allowed = ["Admin", "Principal", "Deputy Principal"].some((role) => user?.roles?.includes(role));
    const [type, setType] = useState("subjects");
    const [years, setYears] = useState([]);
    const [sections, setSections] = useState([]);
    const [grades, setGrades] = useState([]);
    const [classes, setClasses] = useState([]);
    const [teachers, setTeachers] = useState([]);
    const [subjects, setSubjects] = useState([]);
    const [rows, setRows] = useState([]);
    const [selection, setSelection] = useState(emptySelection);
    const [loading, setLoading] = useState(true);
    const [gradesLoading, setGradesLoading] = useState(false);
    const [classesLoading, setClassesLoading] = useState(false);
    const [loadError, setLoadError] = useState("");
    const [gradesError, setGradesError] = useState("");
    const [classesError, setClassesError] = useState("");
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [saving, setSaving] = useState(false);
    const [reload, setReload] = useState(0);
    const [confirmedReplacement, setConfirmedReplacement] = useState(false);
    const [action, setAction] = useState(null);
    const [status, setStatus] = useState("all");
    const [search, setSearch] = useState("");
    const busy = useRef(false);
    const mounted = useRef(false);

    useEffect(() => {
        mounted.current = true;
        return () => { mounted.current = false; };
    }, []);

    useEffect(() => {
        if (!allowed) return;
        let active = true;
        async function load() {
            setLoading(true);
            setLoadError("");
            try {
                const responses = await Promise.all([
                    academicApi.getAcademicYears(), academicApi.getSections(),
                    staffManagementApi.getStaff(), academicApi.getSubjects(),
                    type === "subjects" ? assignmentsApi.getSubjectAssignments() : assignmentsApi.getClassTeachers(),
                ]);
                const list = type === "subjects" ? asList(responses[4]) : responses[4].data?.classTeachers;
                if (!Array.isArray(list)) throw new Error("Unexpected assignments response.");
                if (!active) return;
                setYears(asList(responses[0]));
                setSections(asList(responses[1]));
                setTeachers(asList(responses[2]).filter((staff) => staff.isActive && staff.accountIsActive !== false && staff.applicationUserId && staff.roles?.includes("Teacher")));
                setSubjects(asList(responses[3]));
                setRows(list);
            } catch (err) {
                if (active) setLoadError(errorMessage(err));
            } finally {
                if (active) setLoading(false);
            }
        }
        load();
        return () => { active = false; };
    }, [allowed, type, reload]);

    useEffect(() => {
        if (!allowed || !selection.sectionId) return;
        let active = true;
        async function loadGrades() {
            setGradesLoading(true);
            setGradesError("");
            try {
                const data = asList(await academicApi.getGrades(selection.sectionId));
                if (active) setGrades(data);
            } catch (err) {
                if (active) setGradesError(errorMessage(err));
            } finally {
                if (active) setGradesLoading(false);
            }
        }
        loadGrades();
        return () => { active = false; };
    }, [allowed, selection.sectionId, reload]);

    useEffect(() => {
        if (!allowed || !selection.gradeId) return;
        let active = true;
        async function loadClasses() {
            setClassesLoading(true);
            setClassesError("");
            try {
                const data = asList(await academicApi.getClasses(selection.gradeId));
                if (active) setClasses(data);
            } catch (err) {
                if (active) setClassesError(errorMessage(err));
            } finally {
                if (active) setClassesLoading(false);
            }
        }
        loadClasses();
        return () => { active = false; };
    }, [allowed, selection.gradeId, reload]);

    const year = years.find((item) => String(item.id) === selection.academicYearId);
    const section = sections.find((item) => String(item.id) === selection.sectionId);
    const grade = grades.find((item) => String(item.id) === selection.gradeId);
    const schoolClass = classes.find((item) => String(item.id) === selection.schoolClassId);
    const teacher = teachers.find((item) => String(item.id) === selection.staffId);
    const subject = subjects.find((item) => String(item.id) === selection.subjectId);
    const pending = loading || gradesLoading || classesLoading;
    const dataError = loadError || gradesError || classesError;
    const currentClassTeacher = type === "classes" && rows.find((row) => row.isActive &&
        String(row.academicYear.id) === selection.academicYearId && String(row.schoolClass.id) === selection.schoolClassId);
    const subjectAssignment = type === "subjects" && rows.find((row) =>
        String(row.academicYear.id) === selection.academicYearId && String(row.schoolClass.id) === selection.schoolClassId &&
        String(row.teacher.id) === selection.staffId && String(row.subject.id) === selection.subjectId);
    const replacing = !!currentClassTeacher && !!teacher && currentClassTeacher.teacher.id !== teacher.id;
    const alreadyAssigned = type === "subjects" ? subjectAssignment?.isActive : !!currentClassTeacher && currentClassTeacher.teacher.id === teacher?.id;
    const canSave = year && section && grade && schoolClass && teacher && (type === "classes" || subject) &&
        !alreadyAssigned && (!replacing || confirmedReplacement);
    const query = search.trim().toLowerCase();
    const visibleRows = rows.filter((row) =>
        (!selection.academicYearId || String(row.academicYear.id) === selection.academicYearId) &&
        (!selection.sectionId || String(sectionOf(row).id) === selection.sectionId) &&
        (!selection.gradeId || String(row.schoolClass.grade.id) === selection.gradeId) &&
        (!selection.schoolClassId || String(row.schoolClass.id) === selection.schoolClassId) &&
        (!selection.staffId || String(row.teacher.id) === selection.staffId) &&
        (type === "classes" || !selection.subjectId || String(row.subject.id) === selection.subjectId) &&
        (status === "all" || row.isActive === (status === "active")) &&
        [row.teacher.fullName, row.teacher.staffNumber, row.academicYear.name, sectionOf(row).name,
            row.schoolClass.grade.name, row.schoolClass.name, row.subject?.name].some((value) => value?.toLowerCase().includes(query))
    );

    function changeSelection(event) {
        const { name, value } = event.target;
        setSelection((current) => ({ ...current, [name]: value,
            ...(name === "sectionId" ? { gradeId: "", schoolClassId: "" } : {}),
            ...(name === "gradeId" ? { schoolClassId: "" } : {}),
        }));
        if (name === "sectionId") { setGrades([]); setClasses([]); setGradesLoading(!!value); setClassesLoading(false); setGradesError(""); setClassesError(""); }
        if (name === "gradeId") { setClasses([]); setClassesLoading(!!value); setClassesError(""); }
        setConfirmedReplacement(false);
        setAction(null);
        setError("");
        setSuccess("");
    }

    function clearSelection() {
        setSelection(emptySelection);
        setGrades([]); setClasses([]); setGradesLoading(false); setClassesLoading(false);
        setGradesError(""); setClassesError(""); setConfirmedReplacement(false); setAction(null);
        setSearch(""); setStatus("all"); setError(""); setSuccess("");
    }

    async function mutate(operation, message) {
        if (busy.current || pending || dataError) return;
        busy.current = true;
        setSaving(true); setError(""); setSuccess("");
        try {
            await operation();
            if (!mounted.current) return;
            setSuccess(message); setAction(null); setConfirmedReplacement(false);
            setReload((current) => current + 1);
        } catch (err) {
            if (mounted.current) setError(errorMessage(err));
        } finally {
            busy.current = false;
            if (mounted.current) setSaving(false);
        }
    }

    function assign(event) {
        event.preventDefault();
        if (!canSave || saving) return;
        const payload = { academicYearId: year.id, schoolClassId: schoolClass.id, staffId: teacher.id };
        if (type === "subjects") {
            payload.subjectId = subject.id;
            mutate(() => assignmentsApi.assignSubject(payload), `${teacher.fullName} assigned to ${subject.name}, ${grade.name} ${schoolClass.name}, ${year.name}.`);
        } else {
            payload.replacesAssignmentId = replacing ? currentClassTeacher.id : null;
            mutate(() => assignmentsApi.assignClassTeacher(payload), `${teacher.fullName} is now the class teacher for ${grade.name} ${schoolClass.name}, ${year.name}.`);
        }
    }

    function confirmAction() {
        if (!action) return;
        const row = action;
        mutate(() => row.isActive ? assignmentsApi.deactivateSubject(row.id) : assignmentsApi.assignSubject({
            academicYearId: row.academicYear.id, schoolClassId: row.schoolClass.id,
            staffId: row.teacher.id, subjectId: row.subject.id,
        }), `Subject-teacher assignment ${row.isActive ? "deactivated" : "reactivated"}.`);
    }

    if (!allowed) return <p className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600">Sign in with a school management role to manage teacher assignments.</p>;

    const fields = [
        ["academicYearId", "Academic year", years, loading], ["sectionId", "Section", sections, loading],
        ["gradeId", "Grade", grades, loading || gradesLoading || !section || !!gradesError],
        ["schoolClassId", "Class", classes, loading || classesLoading || gradesLoading || !grade || !!classesError],
        ["staffId", "Teacher", teachers, loading],
        ...(type === "subjects" ? [["subjectId", "Subject", subjects, loading]] : []),
    ];

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div><h1 className="text-2xl font-bold text-slate-950">Teacher Assignments</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">Assign subjects to teachers and choose the class teacher responsible for class attendance.</p></div>
                <button type="button" disabled={saving || pending} onClick={() => { setAction(null); setConfirmedReplacement(false); setReload((current) => current + 1); }} className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}><RefreshCw className="h-4 w-4" />Refresh</button>
            </div>
            <nav aria-label="Assignment categories" className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3">
                {[["subjects", "Subject Teachers", BookOpen], ["classes", "Class Teachers", Users]].map(([id, label, Icon]) => <button key={id} type="button" disabled={saving} aria-current={type === id ? "page" : undefined} onClick={() => { if (id !== type) { clearSelection(); setRows([]); setLoading(true); setType(id); } }} className={`${buttonClass} ${type === id ? "bg-blue-600 text-white" : "text-slate-600 hover:bg-slate-50"}`}><Icon className="h-4 w-4" />{label}</button>)}
            </nav>
            {(dataError || error) && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{dataError || error}</p>}
            {success && <p role="status" className="rounded-xl border border-green-200 bg-green-50 p-4 text-sm text-green-800">{success}</p>}
            <form onSubmit={assign} className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
                <h2 className="text-lg font-semibold text-slate-950">{type === "subjects" ? "Assign a subject teacher" : "Assign a class teacher"}</h2>
                <p className="mt-2 text-sm text-slate-500">Only active staff accounts with the Teacher role are listed. Selections also filter the assignments below.</p>
                <div className="mt-5 grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
                    {fields.map(([name, label, options, disabled]) => <label key={name} className="text-sm font-medium text-slate-700">{label}
                        <select required name={name} value={selection[name]} onChange={changeSelection} disabled={saving || disabled || !!loadError} className={`${inputClass} cursor-pointer`}>
                            <option value="">Select {label.toLowerCase()}</option>
                            {options.map((item) => <option key={item.id} value={item.id}>{name === "staffId" ? `${item.staffNumber} — ${item.fullName}` : item.name}</option>)}
                        </select>
                    </label>)}
                </div>
                {!loading && !loadError && (!years.length || !sections.length || !teachers.length || (type === "subjects" && !subjects.length)) && <p className="mt-4 rounded-xl bg-amber-50 p-4 text-sm text-amber-800">Create the required academic setup records and an active Teacher account before assigning.</p>}
                {section && !gradesLoading && !gradesError && !grades.length && <p className="mt-3 text-sm text-slate-500">Add a grade to this section in Academic Setup.</p>}
                {grade && !classesLoading && !classesError && !classes.length && <p className="mt-3 text-sm text-slate-500">Add a class to this grade in Academic Setup.</p>}
                {type === "classes" && year && schoolClass && !loading && <p className="mt-5 rounded-xl bg-slate-50 p-4 text-sm text-slate-600">Current class teacher: {currentClassTeacher ? `${currentClassTeacher.teacher.staffNumber} — ${currentClassTeacher.teacher.fullName}` : "No active assignment"}</p>}
                {replacing && <label className="mt-4 flex cursor-pointer items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"><input type="checkbox" checked={confirmedReplacement} disabled={saving || pending} onChange={(event) => setConfirmedReplacement(event.target.checked)} className="mt-0.5 h-4 w-4 shrink-0" /><span>Replace {currentClassTeacher.teacher.fullName} with {teacher.fullName} for {year?.name}, {grade?.name} {schoolClass?.name}. The previous assignment will become inactive.</span></label>}
                <div className="mt-5 flex flex-wrap gap-3">
                    <button type="submit" disabled={saving || pending || !!dataError || !canSave} className={`${buttonClass} w-full bg-blue-600 text-white hover:bg-blue-700 sm:w-auto`}>{saving ? "Saving…" : alreadyAssigned ? "Already assigned" : replacing ? "Replace class teacher" : subjectAssignment && !subjectAssignment.isActive ? "Reactivate assignment" : "Assign teacher"}</button>
                    <button type="button" disabled={saving} onClick={clearSelection} className={`${buttonClass} border border-slate-200 text-slate-700 hover:bg-slate-50`}>Clear selections</button>
                </div>
            </form>
            {action && <section aria-label="Confirm assignment change" className="rounded-2xl border border-amber-200 bg-amber-50 p-5">
                <h2 className="font-semibold text-amber-950">{action.isActive ? "Deactivate" : "Reactivate"} this subject assignment?</h2>
                <p className="mt-2 text-sm text-amber-900">{action.teacher.fullName} · {action.subject.name} · {action.schoolClass.grade.name} {action.schoolClass.name} · {action.academicYear.name}. {action.isActive && "The assignment record and existing marks are kept."}</p>
                <div className="mt-4 flex flex-wrap gap-3"><button type="button" disabled={saving || pending || !!dataError} onClick={confirmAction} className={`${buttonClass} bg-blue-600 text-white hover:bg-blue-700`}>Confirm {action.isActive ? "deactivation" : "reactivation"}</button><button type="button" disabled={saving} onClick={() => setAction(null)} className={`${buttonClass} border border-amber-200 bg-white text-slate-700`}>Cancel</button></div>
            </section>}
            <section aria-label="Existing teacher assignments" className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
                <h2 className="text-lg font-semibold text-slate-950">Existing assignments</h2>
                <div className="mt-4 grid gap-4 sm:grid-cols-[1fr_200px]">
                    <label className="text-sm font-medium text-slate-700">Search<input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Teacher, class, subject or year" className={inputClass} /></label>
                    <label className="text-sm font-medium text-slate-700">Status<select value={status} onChange={(event) => setStatus(event.target.value)} className={`${inputClass} cursor-pointer`}><option value="all">All assignments</option><option value="active">Active</option><option value="inactive">Inactive</option></select></label>
                </div>
                {loading ? <p role="status" className="py-8 text-sm text-slate-500">Loading assignments…</p> : loadError ? <p className="py-8 text-sm text-slate-500">Refresh to load the current assignments.</p> : !visibleRows.length ? <p className="py-8 text-sm text-slate-500">No assignments match these selections. Clear selections to see all records.</p> : <>
                    <p className="mt-5 text-sm text-slate-500">{visibleRows.length} of {rows.length} assignments</p>
                    <div className="mt-4 grid gap-4 xl:grid-cols-2">{visibleRows.map((row) => <article key={row.id} className="min-w-0 rounded-xl border border-slate-200 p-4">
                        <div className="flex flex-wrap items-start justify-between gap-3"><div><h3 className="break-words font-semibold text-slate-950">{row.teacher.fullName}</h3><p className="mt-1 text-sm text-slate-500">{row.teacher.staffNumber}</p></div><span className={`rounded-full px-3 py-1 text-xs font-semibold ${row.isActive ? "bg-green-50 text-green-700" : "bg-slate-100 text-slate-500"}`}>{row.isActive ? "Active" : "Inactive"}</span></div>
                        <dl className="mt-4 grid grid-cols-2 gap-4 text-sm">{[["Academic year", row.academicYear.name], ["Section", sectionOf(row).name], ["Class", `${row.schoolClass.grade.name} ${row.schoolClass.name}`], [type === "subjects" ? "Subject" : "Assigned on", type === "subjects" ? row.subject.name : row.assignedAt?.slice(0, 10)]].map(([label, value]) => <div key={label}><dt className="text-slate-500">{label}</dt><dd className="mt-1 break-words text-slate-800">{value || "—"}</dd></div>)}</dl>
                        {type === "subjects" && <button type="button" disabled={saving || pending || !!dataError} onClick={() => { setAction(row); setError(""); setSuccess(""); }} className={`${buttonClass} mt-4 ${row.isActive ? "text-red-600 hover:bg-red-50" : "text-blue-600 hover:bg-blue-50"}`}>{row.isActive ? "Deactivate" : "Reactivate"}</button>}
                    </article>)}</div>
                </>}
            </section>
        </div>
    );
}
