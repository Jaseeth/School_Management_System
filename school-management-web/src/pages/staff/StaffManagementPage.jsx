import { useEffect, useState } from "react";
import { Plus, RefreshCw, Search, Users } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import { staffManagementApi, staffErrorMessage } from "../../api/staffManagementApi";
import AddStaffForm from "./AddStaffForm";

const buttonClass = "inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50";
const inputClass = "w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100";

function isActive(person) {
    return person.isActive && person.accountIsActive !== false;
}

export default function StaffManagementPage() {
    const { user } = useAuth();
    const allowed = ["Admin", "Principal", "Deputy Principal"].some((role) => user?.roles?.includes(role));
    const [staff, setStaff] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [reload, setReload] = useState(0);
    const [search, setSearch] = useState("");
    const [status, setStatus] = useState("all");
    const [creating, setCreating] = useState(false);
    const [page, setPage] = useState(1);
    const pageSize = 12;

    useEffect(() => {
        if (!allowed) return;
        let active = true;
        async function loadStaff() {
            setLoading(true);
            setError("");
            try {
                const response = await staffManagementApi.getStaff();
                if (!active) return;
                if (!Array.isArray(response.data)) {
                    setError("Unexpected staff list response. Refresh and try again.");
                    return;
                }
                setStaff(response.data);
            } catch (err) {
                if (active) setError(staffErrorMessage(err, "Unable to load staff. Check that the API is running."));
            } finally {
                if (active) setLoading(false);
            }
        }
        loadStaff();
        return () => { active = false; };
    }, [allowed, reload]);

    const query = search.trim().toLowerCase();
    const filtered = staff.filter((person) => {
        const matchesSearch = [person.staffNumber, person.fullName, person.email, person.designation, ...(person.roles ?? [])].some((value) => value?.toLowerCase().includes(query));
        const matchesStatus = status === "all" || (status === "active" ? isActive(person) : status === "inactive" ? !isActive(person) : person.mustChangePassword);
        return matchesSearch && matchesStatus;
    });
    const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
    const currentPage = Math.min(page, totalPages);
    const visible = filtered.slice((currentPage - 1) * pageSize, currentPage * pageSize);

    function created(data) {
        setCreating(false);
        setSearch("");
        setStatus("all");
        setPage(1);
        setSuccess(`Staff account ${data.staffNumber} — ${data.fullName} created. A password change is required at first sign-in.`);
        setReload((current) => current + 1);
    }

    if (!allowed) return <p className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600">Sign in with a staff management role to view this page.</p>;

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div><h1 className="text-2xl font-bold text-slate-950">Staff Management</h1><p className="mt-2 text-sm text-slate-500">Manage staff accounts and check their sign-in status.</p></div>
                <div className="flex flex-wrap gap-3">
                    <button type="button" disabled={loading || creating} onClick={() => setReload((current) => current + 1)} className={`${buttonClass} border border-slate-200 bg-white text-slate-700 hover:bg-slate-50`}><RefreshCw className="h-4 w-4" />Refresh</button>
                    <button type="button" disabled={creating} onClick={() => { setCreating(true); setSuccess(""); }} className={`${buttonClass} bg-blue-600 text-white hover:bg-blue-700`}><Plus className="h-4 w-4" />Add Staff</button>
                </div>
            </div>
            {success && <p role="status" className="rounded-xl border border-green-200 bg-green-50 p-4 text-sm text-green-800">{success}</p>}
            {creating && <AddStaffForm onCreated={created} onCancel={() => setCreating(false)} />}
            <section className="rounded-2xl border border-slate-200 bg-white p-5 sm:p-6" aria-label="Staff accounts">
                <div className="flex items-center gap-2"><Users className="h-5 w-5 text-blue-600" /><h2 className="text-lg font-bold text-slate-950">Staff accounts</h2></div>
                <div className="mt-5 grid gap-4 sm:grid-cols-[1fr_220px]">
                    <label className="text-sm font-medium text-slate-700">Search staff
                        <div className="relative mt-2"><Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-slate-400" /><input type="search" value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder="Staff number, name, email or role" className={`${inputClass} pl-10`} /></div>
                    </label>
                    <label className="text-sm font-medium text-slate-700">Account status
                        <select value={status} onChange={(event) => { setStatus(event.target.value); setPage(1); }} className={`${inputClass} mt-2 cursor-pointer`}><option value="all">All accounts</option><option value="active">Active</option><option value="inactive">Inactive</option><option value="password">Password change required</option></select>
                    </label>
                </div>
                {error && <p role="alert" className="mt-5 rounded-xl bg-red-50 p-4 text-sm text-red-700">{error}</p>}
                {loading ? <p role="status" className="py-8 text-sm text-slate-500">Loading staff…</p> : error ? <p className="py-8 text-sm text-slate-500">Refresh to load the current staff list.</p> : !filtered.length ? <p className="py-8 text-sm text-slate-500">{staff.length ? "No staff match these filters." : "No staff accounts found."}</p> : (
                    <>
                        <p className="mt-5 text-sm text-slate-500">Showing {(currentPage - 1) * pageSize + 1}–{Math.min(currentPage * pageSize, filtered.length)} of {filtered.length}</p>
                        <div className="mt-4 grid gap-4 lg:grid-cols-2">
                            {visible.map((person) => (
                                <article key={person.id} className="min-w-0 rounded-xl border border-slate-200 p-4">
                                    <div className="flex flex-wrap items-start justify-between gap-3"><div className="min-w-0"><h3 className="break-words font-semibold text-slate-950">{person.fullName}</h3><p className="mt-1 break-all text-sm text-slate-500">{person.staffNumber}</p></div><span className={`rounded-full px-3 py-1 text-xs font-semibold ${isActive(person) ? "bg-green-50 text-green-700" : "bg-slate-100 text-slate-600"}`}>{isActive(person) ? "Active" : "Inactive"}</span></div>
                                    <dl className="mt-4 space-y-3 text-sm"><div><dt className="text-slate-500">Email</dt><dd className="mt-1 break-all text-slate-800">{person.email || "—"}</dd></div><div><dt className="text-slate-500">Designation</dt><dd className="mt-1 break-words text-slate-800">{person.designation || "—"}</dd></div><div><dt className="text-slate-500">Roles</dt><dd className="mt-1 break-words text-slate-800">{person.roles?.join(", ") || "—"}</dd></div></dl>
                                    <p className={`mt-4 rounded-lg px-3 py-2 text-xs font-medium ${person.mustChangePassword ? "bg-amber-50 text-amber-800" : "bg-slate-50 text-slate-600"}`}>{person.mustChangePassword ? "Password change required at next sign-in" : "Password change completed"}</p>
                                </article>
                            ))}
                        </div>
                        {totalPages > 1 && <div className="mt-5 flex flex-wrap items-center justify-between gap-3"><button type="button" disabled={currentPage <= 1} onClick={() => setPage(currentPage - 1)} className={`${buttonClass} border border-slate-200 text-slate-700 hover:bg-slate-50`}>Previous</button><p className="text-sm text-slate-500">Page {currentPage} of {totalPages}</p><button type="button" disabled={currentPage >= totalPages} onClick={() => setPage(currentPage + 1)} className={`${buttonClass} border border-slate-200 text-slate-700 hover:bg-slate-50`}>Next</button></div>}
                    </>
                )}
            </section>
        </div>
    );
}
