import { useEffect, useRef, useState } from "react";
import { studentsApi } from "../../api/studentsApi";
import { parentManagementApi } from "../../api/parentManagementApi";

const buttonClass =
    "cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50";

const primaryClass =
    "cursor-pointer rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50";

const inputClass =
    "block w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50";

const emptyRelationship = {
    relationship: "",
    isPrimaryGuardian: false,
    isEmergencyContact: false,
};

export default function ParentStudentLinks({
    parent,
    links = [],
    onUpdated,
}) {
    const [mode, setMode] = useState(null);
    const [selectedStudent, setSelectedStudent] = useState(null);
    const [editingLink, setEditingLink] = useState(null);
    const [form, setForm] = useState({ ...emptyRelationship });
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");

    const [searchInput, setSearchInput] = useState("");
    const [query, setQuery] = useState(null);
    const [students, setStudents] = useState([]);
    const [totalPages, setTotalPages] = useState(1);
    const [loading, setLoading] = useState(false);
    const [searchError, setSearchError] = useState("");

    const busyRef = useRef(false);
    const linkedIds = new Set(links.map((link) => link.student.id));

    useEffect(() => {
        if (mode !== "link" || !query) return;

        let active = true;

        async function searchStudents() {
            setLoading(true);
            setSearchError("");
            setStudents([]);

            try {
                const response = await studentsApi.getManagementStudents({
                    search: query.search,
                    page: query.page,
                    pageSize: 10,
                    isActive: true,
                });

                if (!active) return;

                if (!Array.isArray(response.data?.students)) {
                    throw new Error("Unexpected student list response.");
                }

                setStudents(response.data.students);
                setTotalPages(Math.max(1, response.data.totalPages ?? 1));
            } catch (err) {
                if (active) {
                    setSearchError(getErrorMessage(err));
                }
            } finally {
                if (active) setLoading(false);
            }
        }

        searchStudents();

        return () => {
            active = false;
        };
    }, [mode, query]);

    function openLink() {
        setMode("link");
        setEditingLink(null);
        setSelectedStudent(null);
        setForm({ ...emptyRelationship });
        setError("");
        setSearchInput("");
        setQuery(null);
        setStudents([]);
        setSearchError("");
    }

    function openEdit(link) {
        setMode("edit");
        setEditingLink(link);
        setSelectedStudent(null);
        setForm({
            relationship: link.relationship || "",
            isPrimaryGuardian: link.isPrimaryGuardian,
            isEmergencyContact: link.isEmergencyContact,
        });
        setError("");
    }

    function cancel() {
        setMode(null);
        setSelectedStudent(null);
        setEditingLink(null);
        setForm({ ...emptyRelationship });
        setError("");
    }

    async function handleSubmit(event) {
        event.preventDefault();

        if (busyRef.current) return;

        setError("");

        if (!form.relationship.trim()) {
            setError("Enter the parent's relationship to the student.");
            return;
        }

        if (mode === "link" && !selectedStudent) {
            setError("Search for and select a student first.");
            return;
        }

        busyRef.current = true;
        setSaving(true);

        try {
            let response;

            if (mode === "link") {
                response = await parentManagementApi.linkStudent({
                    parentGuardianId: parent.id,
                    studentId: selectedStudent.id,
                    relationship: form.relationship.trim(),
                    isPrimaryGuardian: form.isPrimaryGuardian,
                    isEmergencyContact: form.isEmergencyContact,
                });
            } else {
                response = await parentManagementApi.updateRelationship(
                    editingLink.relationshipId,
                    {
                        relationship: form.relationship.trim(),
                        isPrimaryGuardian: form.isPrimaryGuardian,
                        isEmergencyContact: form.isEmergencyContact,
                        isActive: true,
                    }
                );
            }

            cancel();
            onUpdated(response.data?.message || "Student link saved.");
        } catch (err) {
            setError(getErrorMessage(err));
        } finally {
            busyRef.current = false;
            setSaving(false);
        }
    }

    async function removeLink(link) {
        if (busyRef.current) return;

        const confirmed = window.confirm(
            `Remove the link between ${parent.fullName} and ` +
            `${link.student.fullName} (${link.student.indexNumber})?`
        );

        if (!confirmed) return;

        busyRef.current = true;
        setSaving(true);
        setError("");

        try {
            const response =
                await parentManagementApi.disableRelationship(
                    link.relationshipId
                );

            cancel();
            onUpdated(response.data?.message || "Student link removed.");
        } catch (err) {
            setError(getErrorMessage(err));
        } finally {
            busyRef.current = false;
            setSaving(false);
        }
    }

    return (
        <section className="mt-6 border-t border-slate-100 pt-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <h3 className="font-bold text-slate-950">
                    Linked Children ({links.length})
                </h3>

                {!mode && (
                    <button
                        type="button"
                        disabled={saving || !parent.isActive}
                        onClick={openLink}
                        className={primaryClass}
                    >
                        Link Student
                    </button>
                )}
            </div>

            {mode === "link" && (
                <div className="mt-4 rounded-xl border border-slate-200 p-4">
                    <h4 className="font-semibold text-slate-950">
                        Find Student
                    </h4>

                    <form
                        onSubmit={(event) => {
                            event.preventDefault();

                            const search = searchInput.trim();

                            if (!search) {
                                setSearchError(
                                    "Enter a student index number or name."
                                );
                                return;
                            }

                            setSelectedStudent(null);
                            setQuery({ search, page: 1 });
                        }}
                        className="mt-3 flex flex-col gap-3 sm:flex-row"
                    >
                        <input
                            type="search"
                            aria-label="Student index number or name"
                            placeholder="Student index number or name"
                            value={searchInput}
                            disabled={saving}
                            onChange={(event) =>
                                setSearchInput(event.target.value)
                            }
                            className={`${inputClass} min-w-0 flex-1`}
                        />

                        <button
                            type="submit"
                            disabled={loading || saving}
                            className={buttonClass}
                        >
                            Search
                        </button>
                    </form>

                    {loading && (
                        <p role="status" className="mt-3 text-sm text-slate-500">
                            Searching students...
                        </p>
                    )}

                    {searchError && (
                        <p role="alert" className="mt-3 text-sm text-red-700">
                            {searchError}
                        </p>
                    )}

                    {!loading && !searchError && query && (
                        <>
                            {students.length === 0 ? (
                                <p className="mt-3 text-sm text-slate-500">
                                    No active students found.
                                </p>
                            ) : (
                                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                                    {students.map((student) => {
                                        const alreadyLinked =
                                            linkedIds.has(student.id);

                                        const selected =
                                            selectedStudent?.id === student.id;

                                        return (
                                            <button
                                                key={student.id}
                                                type="button"
                                                disabled={
                                                    saving ||
                                                    alreadyLinked ||
                                                    !student.isActive
                                                }
                                                onClick={() => {
                                                    setSelectedStudent(student);
                                                    setError("");
                                                }}
                                                className={`min-w-0 cursor-pointer rounded-xl border p-4 text-left disabled:cursor-not-allowed disabled:opacity-50 ${selected
                                                        ? "border-blue-500 bg-blue-50"
                                                        : "border-slate-200 hover:bg-slate-50"
                                                    }`}
                                            >
                                                <span className="block break-words font-semibold text-slate-950">
                                                    {student.fullName}
                                                </span>
                                                <span className="mt-1 block text-sm text-slate-500">
                                                    {student.indexNumber}
                                                </span>
                                                <span className="mt-1 block text-sm text-slate-500">
                                                    Grade {student.grade || "—"}
                                                    {" · Class "}
                                                    {student.class || "—"}
                                                </span>
                                                <span className="mt-2 block text-xs font-semibold text-blue-700">
                                                    {alreadyLinked
                                                        ? "Already linked"
                                                        : selected
                                                            ? "Selected"
                                                            : "Select Student"}
                                                </span>
                                            </button>
                                        );
                                    })}
                                </div>
                            )}

                            <div className="mt-4 flex flex-wrap items-center gap-3">
                                <button
                                    type="button"
                                    disabled={saving || query.page <= 1}
                                    onClick={() => {
                                        setSelectedStudent(null);
                                        setQuery((current) => ({
                                            ...current,
                                            page: current.page - 1,
                                        }));
                                    }}
                                    className={buttonClass}
                                >
                                    Previous
                                </button>

                                <span className="text-sm text-slate-500">
                                    Page {query.page} of {totalPages}
                                </span>

                                <button
                                    type="button"
                                    disabled={
                                        saving || query.page >= totalPages
                                    }
                                    onClick={() => {
                                        setSelectedStudent(null);
                                        setQuery((current) => ({
                                            ...current,
                                            page: current.page + 1,
                                        }));
                                    }}
                                    className={buttonClass}
                                >
                                    Next
                                </button>
                            </div>
                        </>
                    )}
                </div>
            )}

            {mode && (
                <form
                    onSubmit={handleSubmit}
                    className="mt-4 rounded-xl border border-slate-200 p-4"
                >
                    <h4 className="font-semibold text-slate-950">
                        {mode === "edit"
                            ? "Edit Relationship"
                            : "Student Relationship"}
                    </h4>

                    {(selectedStudent || editingLink) && (
                        <p className="mt-2 text-sm text-slate-600">
                            {selectedStudent?.fullName ||
                                editingLink?.student.fullName}
                            {" · "}
                            {selectedStudent?.indexNumber ||
                                editingLink?.student.indexNumber}
                        </p>
                    )}

                    <fieldset disabled={saving} className="mt-4">
                        <label className="block text-sm font-semibold text-slate-700">
                            Relationship
                            <input
                                value={form.relationship}
                                required
                                placeholder="Father, Mother, Guardian..."
                                onChange={(event) => {
                                    setForm((current) => ({
                                        ...current,
                                        relationship: event.target.value,
                                    }));
                                    setError("");
                                }}
                                className={`${inputClass} mt-2`}
                            />
                        </label>

                        <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:gap-6">
                            <Checkbox
                                label="Primary Guardian"
                                checked={form.isPrimaryGuardian}
                                onChange={(event) =>
                                    setForm((current) => ({
                                        ...current,
                                        isPrimaryGuardian:
                                            event.target.checked,
                                    }))
                                }
                            />
                            <Checkbox
                                label="Emergency Contact"
                                checked={form.isEmergencyContact}
                                onChange={(event) =>
                                    setForm((current) => ({
                                        ...current,
                                        isEmergencyContact:
                                            event.target.checked,
                                    }))
                                }
                            />
                        </div>

                        <p className="mt-3 text-sm text-slate-500">
                            Selecting Primary Guardian replaces the student's
                            current primary guardian.
                        </p>
                    </fieldset>

                    <div className="mt-5 flex flex-wrap gap-3">
                        <button
                            type="submit"
                            disabled={
                                saving ||
                                (mode === "link" && !selectedStudent)
                            }
                            className={primaryClass}
                        >
                            {saving
                                ? "Saving..."
                                : mode === "edit"
                                    ? "Save Changes"
                                    : "Link Student"}
                        </button>

                        <button
                            type="button"
                            disabled={saving}
                            onClick={cancel}
                            className={buttonClass}
                        >
                            Cancel
                        </button>
                    </div>
                </form>
            )}

            {error && (
                <p
                    role="alert"
                    className="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-700"
                >
                    {error}
                </p>
            )}

            {links.length === 0 ? (
                <p className="mt-4 text-sm text-slate-500">
                    No active student links.
                </p>
            ) : (
                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    {links.map((link) => (
                        <article
                            key={link.relationshipId}
                            className="min-w-0 rounded-xl border border-slate-200 p-4"
                        >
                            <p className="break-words font-semibold text-slate-950">
                                {link.student.fullName}
                            </p>
                            <p className="mt-1 text-sm text-slate-500">
                                {link.student.indexNumber}
                            </p>
                            <p className="mt-2 text-sm text-slate-600">
                                Grade {link.student.schoolClass?.gradeName}
                                {" · Class "}
                                {link.student.schoolClass?.className}
                            </p>
                            <p className="mt-1 text-sm text-slate-600">
                                {link.relationship}
                                {link.isPrimaryGuardian ? " · Primary" : ""}
                                {link.isEmergencyContact ? " · Emergency" : ""}
                            </p>

                            <div className="mt-4 flex flex-wrap gap-2">
                                <button
                                    type="button"
                                    disabled={saving || mode !== null}
                                    onClick={() => openEdit(link)}
                                    className={buttonClass}
                                >
                                    Edit Relationship
                                </button>

                                <button
                                    type="button"
                                    disabled={saving || mode !== null}
                                    onClick={() => removeLink(link)}
                                    className={`${buttonClass} text-red-700`}
                                >
                                    Remove Link
                                </button>
                            </div>
                        </article>
                    ))}
                </div>
            )}
        </section>
    );
}

function Checkbox({ label, ...props }) {
    return (
        <label className="inline-flex cursor-pointer items-center gap-2 text-sm text-slate-700">
            <input
                type="checkbox"
                {...props}
                className="h-4 w-4 cursor-pointer rounded border-slate-300 accent-blue-600 disabled:cursor-not-allowed"
            />
            {label}
        </label>
    );
}

function getErrorMessage(error) {
    const body = error?.response?.data;

    const errors = Array.isArray(body?.errors)
        ? body.errors
        : Object.values(body?.errors ?? {}).flat();

    if (errors.length) return errors.join(" ");
    if (body?.message) return body.message;

    if (!error?.response) {
        return "Cannot reach the backend. Check that the API is running.";
    }

    return "The request failed. Refresh parent details before retrying.";
}