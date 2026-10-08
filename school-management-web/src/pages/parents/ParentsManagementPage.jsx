import { useEffect, useState } from "react";
import { useAuth } from "../../context/AuthContext";
import { parentManagementApi } from "../../api/parentManagementApi";
import ParentManagementActions from "./ParentManagementActions";
import ParentStudentLinks from "./ParentStudentLinks";

const emptyForm = {
    parentNumber: "",
    fullName: "",
    email: "",
    phoneNumber: "",
};

const buttonClass =
    "cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50";

const primaryButtonClass =
    "cursor-pointer rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50";

export default function ParentsManagementPage() {
    const { user } = useAuth();

    const allowed = ["Admin", "Principal", "Deputy Principal"].some(
        (role) => user?.roles?.includes(role)
    );

    const [searchInput, setSearchInput] = useState("");
    const [query, setQuery] = useState({ search: "", page: 1 });
    const [refreshKey, setRefreshKey] = useState(0);
    const [parents, setParents] = useState([]);
    const [totalCount, setTotalCount] = useState(0);
    const [loading, setLoading] = useState(true);
    const [listError, setListError] = useState("");

    const [creating, setCreating] = useState(false);
    const [form, setForm] = useState({ ...emptyForm });
    const [saving, setSaving] = useState(false);
    const [formError, setFormError] = useState("");
    const [success, setSuccess] = useState("");

    const [selectedId, setSelectedId] = useState(null);
    const [details, setDetails] = useState(null);
    const [detailsLoading, setDetailsLoading] = useState(false);
    const [detailsError, setDetailsError] = useState("");

    const pageSize = 20;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

    useEffect(() => {
        if (!allowed) return;

        let active = true;

        async function loadParents() {
            setLoading(true);
            setListError("");

            try {
                const response = await parentManagementApi.getParents({
                    search: query.search || undefined,
                    page: query.page,
                    pageSize,
                });

                if (!active) return;

                if (!Array.isArray(response.data?.parents)) {
                    throw new Error("Unexpected parent list response.");
                }

                setParents(response.data.parents);
                setTotalCount(response.data.totalCount ?? 0);
            } catch (error) {
                if (active) {
                    setListError(
                        getErrorMessage(error, "Unable to load parents.")
                    );
                }
            } finally {
                if (active) setLoading(false);
            }
        }

        loadParents();

        return () => {
            active = false;
        };
    }, [allowed, query, refreshKey]);

    useEffect(() => {
        if (!allowed || selectedId === null) return;

        let active = true;

        async function loadDetails() {
            setDetails(null);
            setDetailsError("");
            setDetailsLoading(true);

            try {
                const response =
                    await parentManagementApi.getParentStudents(selectedId);

                if (!active) return;

                if (!response.data?.parent) {
                    throw new Error("Unexpected parent details response.");
                }

                setDetails(response.data);
            } catch (error) {
                if (active) {
                    setDetailsError(
                        getErrorMessage(error, "Unable to load parent details.")
                    );
                }
            } finally {
                if (active) setDetailsLoading(false);
            }
        }

        loadDetails();

        return () => {
            active = false;
        };
    }, [allowed, selectedId, refreshKey]);

    function openCreate() {
        setForm({ ...emptyForm });
        setFormError("");
        setSuccess("");
        setSelectedId(null);
        setCreating(true);
    }

    async function handleCreate(event) {
        event.preventDefault();
        if (saving) return;

        setFormError("");
        setSuccess("");
        setSaving(true);

        try {
            const response = await parentManagementApi.createParent({
                parentNumber: form.parentNumber.trim(),
                fullName: form.fullName.trim(),
                email: form.email.trim() || null,
                phoneNumber: form.phoneNumber.trim() || null,
            });

            setSuccess(
                response.data?.message || "Parent created successfully."
            );
            setCreating(false);
            setForm({ ...emptyForm });
            setSearchInput("");
            setQuery({ search: "", page: 1 });
            setRefreshKey((value) => value + 1);

            if (response.data?.parent?.id) {
                setSelectedId(response.data.parent.id);
            }
        } catch (error) {
            setFormError(
                getErrorMessage(
                    error,
                    "Unable to create parent. Refresh the list before retrying."
                )
            );
        } finally {
            setSaving(false);
        }
    }

    function updateField(event) {
        const { name, value } = event.target;
        setForm((current) => ({ ...current, [name]: value }));
        setFormError("");
    }

    if (!allowed) {
        return (
            <div className="p-6">
                <p role="alert" className="text-sm text-red-700">
                    You do not have access to Parents Management.
                </p>
            </div>
        );
    }

    return (
        <main className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-slate-950">
                        Parents & Guardians
                    </h1>
                    <p className="mt-1 text-sm text-slate-500">
                        Manage parent records and view their linked children.
                    </p>
                </div>

                <button
                    type="button"
                    onClick={openCreate}
                    disabled={saving}
                    className={primaryButtonClass}
                >
                    Create Parent
                </button>
            </div>

            {success && (
                <p
                    role="status"
                    className="mt-5 rounded-xl bg-emerald-50 p-4 text-sm text-emerald-700"
                >
                    {success}
                </p>
            )}

            {creating && (
                <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                    <h2 className="text-lg font-bold text-slate-950">
                        Create Parent / Guardian
                    </h2>
                    <p className="mt-1 text-sm text-slate-500">
                        An email address is needed when creating a login account.
                    </p>

                    <form onSubmit={handleCreate} className="mt-5">
                        <fieldset disabled={saving}>
                            <div className="grid gap-4 sm:grid-cols-2">
                                <Field
                                    label="Parent Number"
                                    name="parentNumber"
                                    value={form.parentNumber}
                                    onChange={updateField}
                                    required
                                />
                                <Field
                                    label="Full Name"
                                    name="fullName"
                                    value={form.fullName}
                                    onChange={updateField}
                                    required
                                    maxLength={200}
                                />
                                <Field
                                    label="Email"
                                    name="email"
                                    type="email"
                                    value={form.email}
                                    onChange={updateField}
                                    maxLength={256}
                                />
                                <Field
                                    label="Phone Number"
                                    name="phoneNumber"
                                    type="tel"
                                    value={form.phoneNumber}
                                    onChange={updateField}
                                    maxLength={50}
                                />
                            </div>
                        </fieldset>

                        {formError && (
                            <p
                                role="alert"
                                className="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-700"
                            >
                                {formError}
                            </p>
                        )}

                        <div className="mt-5 flex flex-wrap gap-3">
                            <button
                                type="submit"
                                disabled={saving}
                                className={primaryButtonClass}
                            >
                                {saving ? "Creating..." : "Create Parent"}
                            </button>
                            <button
                                type="button"
                                disabled={saving}
                                onClick={() => setCreating(false)}
                                className={buttonClass}
                            >
                                Cancel
                            </button>
                        </div>
                    </form>
                </section>
            )}

            <form
                onSubmit={(event) => {
                    event.preventDefault();
                    setQuery({ search: searchInput.trim(), page: 1 });
                    setRefreshKey((value) => value + 1);
                    setSelectedId(null);
                }}
                className="mt-6 flex flex-col gap-3 sm:flex-row"
            >
                <input
                    type="search"
                    aria-label="Search parents"
                    placeholder="Search parent number, name or email"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                    className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />
                <button type="submit" className={buttonClass}>
                    Search
                </button>
                <button
                    type="button"
                    disabled={loading}
                    onClick={() => setRefreshKey((value) => value + 1)}
                    className={buttonClass}
                >
                    Refresh
                </button>
            </form>

            {listError && (
                <p
                    role="alert"
                    className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700"
                >
                    {listError}
                </p>
            )}

            {loading ? (
                <p role="status" className="mt-6 text-sm text-slate-500">
                    Loading parents...
                </p>
            ) : !listError && (
                <>
                    <p className="mt-5 text-sm text-slate-500">
                        {totalCount} parent / guardian records
                    </p>

                    {parents.length === 0 ? (
                        <div className="mt-4 rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
                            No parents found.
                        </div>
                    ) : (
                        <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                            {parents.map((parent) => (
                                <article
                                    key={parent.id}
                                    className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
                                >
                                    <div className="flex items-start justify-between gap-3">
                                        <div className="min-w-0">
                                            <h2 className="break-words font-bold text-slate-950">
                                                {parent.fullName}
                                            </h2>
                                            <p className="mt-1 text-sm text-slate-500">
                                                {parent.parentNumber}
                                            </p>
                                        </div>
                                        <span className={`rounded-full px-2 py-1 text-xs font-semibold ${parent.isActive
                                                ? "bg-emerald-50 text-emerald-700"
                                                : "bg-slate-100 text-slate-600"
                                            }`}>
                                            {parent.isActive ? "Active" : "Inactive"}
                                        </span>
                                    </div>

                                    <p className="mt-4 break-all text-sm text-slate-600">
                                        {parent.email || "Email not provided"}
                                    </p>
                                    <p className="mt-1 text-sm text-slate-600">
                                        {parent.phoneNumber || "Phone not provided"}
                                    </p>
                                    <p className="mt-3 text-sm text-slate-600">
                                        Linked children: {parent.linkedStudents}
                                    </p>
                                    <p className="mt-1 text-sm text-slate-600">
                                        Login account: {parent.hasLoginAccount
                                            ? "Created"
                                            : "Not created"}
                                    </p>

                                    <button
                                        type="button"
                                        onClick={() => {
                                            setCreating(false);
                                            setSelectedId(parent.id);
                                            setRefreshKey((value) => value + 1);
                                        }}
                                        className={`${buttonClass} mt-4 w-full`}
                                    >
                                        View Details
                                    </button>
                                </article>
                            ))}
                        </div>
                    )}

                    <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
                        <p className="text-sm text-slate-500">
                            Page {query.page} of {totalPages}
                        </p>
                        <div className="flex gap-2">
                            <button
                                type="button"
                                disabled={query.page <= 1}
                                onClick={() => setQuery((current) => ({
                                    ...current,
                                    page: current.page - 1,
                                }))}
                                className={buttonClass}
                            >
                                Previous
                            </button>
                            <button
                                type="button"
                                disabled={query.page >= totalPages}
                                onClick={() => setQuery((current) => ({
                                    ...current,
                                    page: current.page + 1,
                                }))}
                                className={buttonClass}
                            >
                                Next
                            </button>
                        </div>
                    </div>
                </>
            )}

            {selectedId !== null && (
                <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                    <div className="flex items-center justify-between gap-3">
                        <h2 className="text-lg font-bold text-slate-950">
                            Parent Details
                        </h2>
                        <button
                            type="button"
                            onClick={() => setSelectedId(null)}
                            className={buttonClass}
                        >
                            Close
                        </button>
                    </div>

                    {detailsLoading && (
                        <p role="status" className="mt-4 text-sm text-slate-500">
                            Loading details...
                        </p>
                    )}

                    {detailsError && (
                        <p role="alert" className="mt-4 text-sm text-red-700">
                            {detailsError}
                        </p>
                    )}

                    {!detailsLoading && !detailsError && details && (
                        <>
                            <dl className="mt-5 grid gap-4 sm:grid-cols-2">
                                <Detail label="Parent Number" value={details.parent.parentNumber} />
                                <Detail label="Full Name" value={details.parent.fullName} />
                                <Detail label="Email" value={details.parent.email} />
                                <Detail label="Phone" value={details.parent.phoneNumber} />
                                <Detail label="Status" value={details.parent.isActive ? "Active" : "Inactive"} />
                                <Detail label="Login Account" value={details.parent.applicationUserId ? "Created" : "Not created"} />
                            </dl>

                            <ParentManagementActions
                                key={`${details.parent.id}-${refreshKey}`}
                                parent={details.parent}
                                onUpdated={(message) => {
                                    setSuccess(message);
                                    setRefreshKey((value) => value + 1);
                                }}
                            />

                            <ParentStudentLinks
                                key={`links-${details.parent.id}-${refreshKey}`}
                                parent={details.parent}
                                links={details.students ?? []}
                                onUpdated={(message) => {
                                    setSuccess(message);
                                    setRefreshKey((value) => value + 1);
                                }}
                            />


                        </>
                    )}
                </section>
            )}
        </main>
    );
}

function Field({ label, ...props }) {
    return (
        <label className="block text-sm font-semibold text-slate-700">
            {label}
            <input
                {...props}
                className="mt-2 block w-full rounded-xl border border-slate-200 px-4 py-3 font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50"
            />
        </label>
    );
}

function Detail({ label, value }) {
    return (
        <div className="min-w-0">
            <dt className="text-xs font-semibold uppercase text-slate-500">
                {label}
            </dt>
            <dd className="mt-1 break-words text-sm text-slate-900">
                {value || "—"}
            </dd>
        </div>
    );
}

function getErrorMessage(error, fallback) {
    const body = error?.response?.data;

    const errors = Array.isArray(body?.errors)
        ? body.errors
        : Object.values(body?.errors ?? {}).flat();

    if (errors.length) return errors.join(" ");

    if (body?.message) return body.message;

    if (error?.response?.status === 403) {
        return "Your account does not have permission for this action.";
    }

    if (!error?.response) {
        return "Cannot reach the backend. Check that the API is running.";
    }

    return fallback;
}