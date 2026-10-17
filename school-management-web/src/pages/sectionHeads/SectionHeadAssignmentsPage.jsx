import { useEffect, useRef, useState } from "react";
import { RefreshCw, ShieldCheck } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import { academicApi } from "../../api/academicApi";
import { sectionHeadAssignmentsApi } from "../../api/sectionHeadAssignmentsApi";

const inputClass = "mt-2 w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-3 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-slate-50";
const buttonClass = "inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50";

function errorMessage(error) {
    if (error?.response?.status === 401) return "Your session has expired. Please sign in again.";
    if (error?.response?.data?.message) return error.response.data.message;
    if (error?.response?.status === 403) return "Your account needs the existing SectionHeads.View, SectionHeads.Manage, Staff.View and academic lookup permissions to manage assignments.";
    if (error?.response?.data?.errors) return Object.values(error.response.data.errors).flat().join(" ");
    if (!error?.response) return "Unable to load or save assignments. Check that the API is running and try again.";
    return "Unable to complete the request. Please try again.";
}

export default function SectionHeadAssignmentsPage() {
    const { hasRole } = useAuth();
    const allowed = hasRole("Admin");
    const [staff, setStaff] = useState([]);
    const [sections, setSections] = useState([]);
    const [years, setYears] = useState([]);
    const [assignments, setAssignments] = useState([]);
    const [statusFilter, setStatusFilter] = useState("all");
    const [form, setForm] = useState({ staffId: "", sectionId: "", academicYearId: "" });
    const [editingId, setEditingId] = useState(null);
    const [removing, setRemoving] = useState(null);
    const [loadError, setLoadError] = useState("");
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [reload, setReload] = useState(0);
    const busy = useRef(false);
    const requestVersion = useRef(0);

    useEffect(() => {
        if (!allowed) return;
        let active = true;
        const version = ++requestVersion.current;

        async function load() {
            setLoading(true);
            setLoadError("");
            try {
                const responses = await Promise.all([
                    sectionHeadAssignmentsApi.getStaff(),
                    academicApi.getSections(),
                    academicApi.getAcademicYears(),
                    sectionHeadAssignmentsApi.getAssignments(),
                ]);
                if (!active || version !== requestVersion.current) return;
                if (responses.some((response) => !Array.isArray(response.data))) {
                    setLoadError("Unexpected assignment or lookup response. Refresh and try again.");
                    return;
                }
                setStaff(responses[0].data.filter((person) => person.isActive && person.applicationUserId && person.accountIsActive !== false && person.roles?.includes("Section Head")));
                setSections(responses[1].data);
                setYears(responses[2].data);
                setAssignments(responses[3].data);
            } catch (err) {
                if (active && version === requestVersion.current) setLoadError(errorMessage(err));
            } finally {
                if (active && version === requestVersion.current) setLoading(false);
            }
        }
        load();
        return () => {
            active = false;
            requestVersion.current = version + 1;
        };
    }, [allowed, reload]);

    const selectedStaff = staff.find((person) => String(person.id) === form.staffId);
    const selectedSection = sections.find((section) => String(section.id) === form.sectionId);
    const selectedYear = years.find((year) => String(year.id) === form.academicYearId);
    const existing = assignments.find((assignment) =>
        String(assignment.staff.id) === form.staffId &&
        String(assignment.section.id) === form.sectionId &&
        String(assignment.academicYear.id) === form.academicYearId
    );
    const visibleAssignments = assignments.filter((assignment) =>
        (statusFilter === "all" || assignment.isActive === (statusFilter === "active")) &&
        (!form.staffId || String(assignment.staff.id) === form.staffId) &&
        (!form.sectionId || String(assignment.section.id) === form.sectionId) &&
        (!form.academicYearId || String(assignment.academicYear.id) === form.academicYearId)
    );
    const canSave = selectedStaff && selectedSection && selectedYear && (editingId !== null ? !existing || existing.id === editingId : !existing?.isActive);

    function changeSelection(event) {
        setForm((current) => ({ ...current, [event.target.name]: event.target.value }));
        setSuccess(""); setError("");
    }

    async function assign(event) {
        event.preventDefault();
        if (busy.current || loading || loadError || !allowed || !canSave) return;
        busy.current = true;
        setSaving(true);
        setError("");
        setSuccess("");
        const version = requestVersion.current;
        try {
            const payload = {
                staffId: Number(form.staffId),
                sectionId: Number(form.sectionId),
                academicYearId: Number(form.academicYearId),
            };
            if (editingId === null) await sectionHeadAssignmentsApi.assign(payload);
            else await sectionHeadAssignmentsApi.update(editingId, payload);
            if (version !== requestVersion.current) return;
            setSuccess(`Section Head assignment ${editingId === null ? "saved" : "updated"} for ${selectedStaff.fullName}.`);
            setEditingId(null); setForm({ staffId: "", sectionId: "", academicYearId: "" });
            setReload((current) => current + 1);
        } catch (err) {
            if (version === requestVersion.current) setError(errorMessage(err));
        } finally {
            busy.current = false;
            setSaving(false);
        }
    }

        function edit(assignment) {
        setEditingId(assignment.id); setRemoving(null); setError(""); setSuccess("");
        setForm({ staffId: String(assignment.staff.id), sectionId: String(assignment.section.id), academicYearId: String(assignment.academicYear.id) });
        window.scrollTo({ top: 0, behavior: "smooth" });
    }
    function cancelEdit() { setEditingId(null); setForm({ staffId: "", sectionId: "", academicYearId: "" }); setError(""); }
    async function remove() {
        if (!removing || busy.current || loading) return;
        busy.current = true; setSaving(true); setError(""); setSuccess("");
        try {
            await sectionHeadAssignmentsApi.remove(removing.id);
            setRemoving(null); cancelEdit(); setSuccess("Section Head assignment removed. Its history is retained.");
            setReload((current) => current + 1);
        } catch (err) { setError(errorMessage(err)); }
        finally { busy.current = false; setSaving(false); }
    }

    if (!allowed) return (
        <div className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600">
            Sign in as Admin to manage Section Head assignments.
        </div>
    );

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-slate-950">Section Head Assignments</h1>
                    <p className="mt-2 max-w-2xl text-sm text-slate-500">Assign review access to a specific section and academic year.</p>
                </div>
                <button type="button" disabled={loading || saving} onClick={() => { setSuccess(""); setReload((current) => current + 1); }} className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}>
                    <RefreshCw className="h-4 w-4" /> Refresh
                </button>
            </div>

            {(error || loadError) && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error || loadError}</div>}
            {success && <div role="status" className="rounded-xl border border-green-200 bg-green-50 p-4 text-sm text-green-800">{success}</div>}

            <form onSubmit={assign} className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6">
                <div className="flex items-center gap-2 text-lg font-semibold text-slate-950"><ShieldCheck className="h-5 w-5 text-blue-600" /> {editingId === null ? "Assign a Section Head" : "Edit Section Head assignment"}</div>
                <p className="mt-2 text-sm text-slate-500">Select a staff account that already has the Section Head role. This assignment does not change account roles.</p>
                <div className="mt-5 grid gap-5 lg:grid-cols-3">
                    <label className="text-sm font-medium text-slate-700">Staff account
                        <select required name="staffId" value={form.staffId} onChange={changeSelection} disabled={loading || saving || !!loadError} className={inputClass}>
                            <option value="">Select staff</option>
                            {staff.map((person) => <option key={person.id} value={person.id}>{person.staffNumber} — {person.fullName}{person.email ? ` (${person.email})` : ""}</option>)}
                        </select>
                    </label>
                    <label className="text-sm font-medium text-slate-700">Section
                        <select required name="sectionId" value={form.sectionId} onChange={changeSelection} disabled={loading || saving || !!loadError} className={inputClass}>
                            <option value="">Select section</option>
                            {sections.map((section) => <option key={section.id} value={section.id}>{section.name}</option>)}
                        </select>
                    </label>
                    <label className="text-sm font-medium text-slate-700">Academic year
                        <select required name="academicYearId" value={form.academicYearId} onChange={changeSelection} disabled={loading || saving || !!loadError} className={inputClass}>
                            <option value="">Select academic year</option>
                            {years.map((year) => <option key={year.id} value={year.id}>{year.name}</option>)}
                        </select>
                    </label>
                </div>
                {selectedStaff && selectedSection && selectedYear && (
                    <p className="mt-5 rounded-xl bg-slate-50 p-4 text-sm text-slate-600">
                        {selectedStaff.fullName} · {selectedSection.name} · {selectedYear.name}
                        {editingId !== null ? " — editing this assignment." : existing?.isActive ? " — already active." : existing ? " — inactive assignment will be reactivated." : " — no existing assignment."}
                    </p>
                )}
                <button type="submit" disabled={!canSave || loading || saving || !!loadError} className={`${buttonClass} mt-5 w-full bg-blue-600 text-white hover:bg-blue-700 sm:w-auto`}>
                    {saving ? "Saving…" : editingId !== null ? "Save changes" : existing?.isActive ? "Already assigned" : existing ? "Reactivate assignment" : "Assign Section Head"}
                </button>
                {editingId !== null && <button type="button" disabled={saving} onClick={cancelEdit} className={`${buttonClass} ml-3 border border-slate-200 text-slate-700`}>Cancel edit</button>}
            </form>

            {removing && <section className="rounded-xl border border-amber-200 bg-amber-50 p-5"><h2 className="font-semibold text-amber-950">Remove this Section Head assignment?</h2><p className="mt-2 text-sm text-amber-900">{removing.staff.fullName} · {removing.section.name} · {removing.academicYear.name}. Access for this assignment will be disabled; the account and review history are retained.</p><div className="mt-4 flex gap-3"><button type="button" disabled={saving} onClick={remove} className={`${buttonClass} bg-red-600 text-white`}>Confirm removal</button><button type="button" disabled={saving} onClick={() => setRemoving(null)} className={`${buttonClass} border border-amber-200 bg-white`}>Cancel</button></div></section>}
            <section className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6" aria-label="Existing assignments">
                <div className="flex flex-wrap items-center justify-between gap-3">
                    <h2 className="text-lg font-semibold text-slate-950">Existing assignments ({visibleAssignments.length})</h2>
                    <button type="button" disabled={loading || saving} onClick={() => { cancelEdit(); setStatusFilter("all"); setSuccess(""); }} className={`${buttonClass} text-blue-600 hover:bg-blue-50`}>Clear filters</button>
                </div>
                <p className="mt-1 text-sm text-slate-500">The selections above also filter this list. Assignments for other years remain unchanged.</p>
                <label className="mt-4 block max-w-xs text-sm font-medium text-slate-700">Status
                    <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)} disabled={loading || saving} className={inputClass}>
                        <option value="all">All</option>
                        <option value="active">Active</option>
                        <option value="inactive">Inactive</option>
                    </select>
                </label>
                {loading ? <p role="status" className="py-8 text-sm text-slate-500">Loading assignments…</p> : loadError ? <p className="py-8 text-sm text-slate-500">Refresh to load the current assignments.</p> : !visibleAssignments.length ? <p className="py-8 text-sm text-slate-500">No assignments match these selections.</p> : (
                    <div className="mt-5 grid gap-4 xl:grid-cols-2">
                        {visibleAssignments.map((assignment) => (
                            <article key={assignment.id} className="min-w-0 rounded-xl border border-slate-200 p-4">
                                <div className="flex flex-wrap items-start justify-between gap-3">
                                    <div className="min-w-0"><h3 className="break-words font-semibold text-slate-950">{assignment.staff.fullName}</h3><p className="mt-1 text-sm text-slate-500">{assignment.staff.staffNumber}</p></div>
                                    <span className={`rounded-full px-3 py-1 text-xs font-semibold ${assignment.isActive ? "bg-green-50 text-green-700" : "bg-slate-100 text-slate-500"}`}>{assignment.isActive ? "Active" : "Inactive"}</span>
                                </div>
                                <dl className="mt-4 grid grid-cols-2 gap-3 text-sm"><div><dt className="text-slate-500">Section</dt><dd className="mt-1 break-words font-medium text-slate-800">{assignment.section.name}</dd></div><div><dt className="text-slate-500">Academic year</dt><dd className="mt-1 break-words font-medium text-slate-800">{assignment.academicYear.name}</dd></div></dl>
                                <div className="mt-4 flex gap-3">{assignment.isActive ? <><button type="button" disabled={saving || loading} onClick={() => edit(assignment)} className={`${buttonClass} text-blue-600 hover:bg-blue-50`}>Edit</button><button type="button" disabled={saving || loading} onClick={() => { setRemoving(assignment); setError(""); setSuccess(""); }} className={`${buttonClass} text-red-600 hover:bg-red-50`}>Remove</button></> : <button type="button" disabled={saving || loading} onClick={() => { setEditingId(null); setForm({ staffId: String(assignment.staff.id), sectionId: String(assignment.section.id), academicYearId: String(assignment.academicYear.id) }); setError(""); setSuccess(""); window.scrollTo({ top: 0, behavior: "smooth" }); }} className={`${buttonClass} text-blue-600 hover:bg-blue-50`}>Reactivate</button>}</div>
                            </article>
                        ))}
                    </div>
                )}
            </section>
        </div>
    );
}
