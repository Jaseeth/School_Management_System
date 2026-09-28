import {
    ArrowLeft,
    CalendarDays,
    GraduationCap,
    Mail,
    Phone,
    School,
    UserRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { studentPortalApi } from "../../api/studentPortalApi";

export default function StudentProfilePage() {
    const navigate = useNavigate();

    const [profile, setProfile] = useState(null);
    const [form, setForm] = useState({
        fullName: "",
        email: "",
        mobile: "",
        currentPassword: "",
    });
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [saveError, setSaveError] = useState("");
    const [success, setSuccess] = useState("");
    const [isEditing, setIsEditing] = useState(false);
    const [saving, setSaving] = useState(false);

    async function loadProfile() {
        try {
            setLoading(true);
            setError("");

            const { data } = await studentPortalApi.getProfile();

            setProfile(data);
            setForm({
                fullName: data.fullName ?? "",
                email: data.email ?? "",
                mobile: data.mobile ?? "",
                currentPassword: "",
            });
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to load your profile."
            );
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        loadProfile();
    }, []);

    function cancelEditing() {
        setForm({
            fullName: profile.fullName ?? "",
            email: profile.email ?? "",
            mobile: profile.mobile ?? "",
            currentPassword: "",
        });
        setSaveError("");
        setSuccess("");
        setIsEditing(false);
    }

    const emailChanged =
        form.email.trim().toLowerCase() !==
        (profile?.email ?? "").toLowerCase();

    async function saveProfile(event) {
        event.preventDefault();

        if (!isEditing || saving) return;

        setSaveError("");
        setSuccess("");

        try {
            setSaving(true);

            const { data } = await studentPortalApi.updateProfile({
                fullName: form.fullName.trim(),
                email: form.email.trim(),
                mobile: form.mobile.trim(),
                currentPassword: emailChanged
                    ? form.currentPassword
                    : null,
            });

            setProfile((current) => ({
                ...current,
                ...data.profile,
            }));

            setForm({
                fullName: data.profile.fullName,
                email: data.profile.email,
                mobile: data.profile.mobile ?? "",
                currentPassword: "",
            });

            setSuccess(
                data.message || "Profile updated successfully."
            );
            setIsEditing(false);
        } catch (err) {
            const details = err?.response?.data;

            setSaveError(
                (Array.isArray(details?.errors)
                    ? details.errors.join(" ")
                    : null) ||
                details?.message ||
                "Unable to save your profile."
            );
        } finally {
            setSaving(false);
        }
    }

    return (
        <StudentPageShell>
            <div className="mx-auto w-full max-w-6xl">
                <button
                    type="button"
                    onClick={() => navigate("/student/dashboard")}
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>

                {loading ? (
                    <p
                        role="status"
                        className="mt-8 text-sm text-slate-500"
                    >
                        Loading profile...
                    </p>
                ) : error ? (
                    <div className="mt-6 rounded-2xl border border-red-100 bg-red-50 p-6">
                        <p role="alert" className="text-sm text-red-700">
                            {error}
                        </p>
                        <button
                            type="button"
                            onClick={loadProfile}
                            className="mt-4 cursor-pointer rounded-xl bg-red-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-red-700"
                        >
                            Try Again
                        </button>
                    </div>
                ) : profile && (
                    <>
                        <div className="mt-6 flex flex-col gap-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-8">
                            <div className="flex min-w-0 items-center gap-5">
                                <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-blue-50 text-blue-700 sm:h-20 sm:w-20">
                                    <UserRound className="h-9 w-9" />
                                </div>

                                <div className="min-w-0">
                                    <div className="flex flex-wrap items-center gap-3">
                                        <h1 className="break-words text-2xl font-bold tracking-tight text-slate-950 sm:text-3xl">
                                            {profile.fullName}
                                        </h1>
                                        {profile.isActive && (
                                            <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                                                Active
                                            </span>
                                        )}
                                    </div>

                                    <p className="mt-2 text-sm text-slate-500">
                                        Index Number:{" "}
                                        <span className="font-semibold text-slate-700">
                                            {profile.indexNumber}
                                        </span>
                                    </p>
                                </div>
                            </div>

                            {!isEditing && (
                                <button
                                    type="button"
                                    onClick={() => {
                                        setSaveError("");
                                        setSuccess("");
                                        setIsEditing(true);
                                    }}
                                    className="w-full cursor-pointer rounded-xl border border-blue-600 px-5 py-2.5 text-sm font-semibold text-blue-600 hover:bg-blue-50 sm:w-auto"
                                >
                                    Edit Profile
                                </button>
                            )}
                        </div>

                        <div className="mt-6 grid gap-6 lg:grid-cols-2">
                            <section className="min-w-0 rounded-2xl border border-slate-200 bg-white shadow-sm">
                                <SectionHeader
                                    icon={
                                        <UserRound className="h-5 w-5" />
                                    }
                                    title="Personal Information"
                                    subtitle="Your registered student details"
                                />

                                <form
                                    onSubmit={saveProfile}
                                    className="px-6"
                                >
                                    {isEditing ? (
                                        <EditRow
                                            label="Full Name"
                                            type="text"
                                            value={form.fullName}
                                            onChange={(value) =>
                                                setForm((current) => ({
                                                    ...current,
                                                    fullName: value,
                                                }))
                                            }
                                            required
                                            maxLength={200}
                                            disabled={saving}
                                            autoComplete="name"
                                        />
                                    ) : (
                                        <ProfileRow
                                            icon={
                                                <UserRound className="h-4 w-4" />
                                            }
                                            label="Full Name"
                                            value={profile.fullName}
                                        />
                                    )}

                                    <ProfileRow
                                        icon={
                                            <GraduationCap className="h-4 w-4" />
                                        }
                                        label="Index Number"
                                        value={profile.indexNumber}
                                    />

                                    <ProfileRow
                                        icon={
                                            <CalendarDays className="h-4 w-4" />
                                        }
                                        label="Date of Birth"
                                        value={formatDate(
                                            profile.dateOfBirth
                                        )}
                                    />

                                    {isEditing ? (
                                        <EditRow
                                            label="Email"
                                            type="email"
                                            value={form.email}
                                            onChange={(value) =>
                                                setForm((current) => ({
                                                    ...current,
                                                    email: value,
                                                }))
                                            }
                                            required
                                            maxLength={256}
                                            disabled={saving}
                                            autoComplete="email"
                                        />
                                    ) : (
                                        <ProfileRow
                                            icon={
                                                <Mail className="h-4 w-4" />
                                            }
                                            label="Email"
                                            value={profile.email}
                                        />
                                    )}

                                    {isEditing ? (
                                        <EditRow
                                            label="Mobile"
                                            type="tel"
                                            value={form.mobile}
                                            onChange={(value) =>
                                                setForm((current) => ({
                                                    ...current,
                                                    mobile: value,
                                                }))
                                            }
                                            maxLength={50}
                                            disabled={saving}
                                            autoComplete="tel"
                                        />
                                    ) : (
                                        <ProfileRow
                                            icon={
                                                <Phone className="h-4 w-4" />
                                            }
                                            label="Mobile"
                                            value={profile.mobile}
                                        />
                                    )}

                                    {isEditing && emailChanged && (
                                        <EditRow
                                            label="Current password (required to change email)"
                                            type="password"
                                            value={
                                                form.currentPassword
                                            }
                                            onChange={(value) =>
                                                setForm((current) => ({
                                                    ...current,
                                                    currentPassword:
                                                        value,
                                                }))
                                            }
                                            required
                                            disabled={saving}
                                            autoComplete="current-password"
                                        />
                                    )}

                                    {isEditing && (
                                        <p className="py-4 text-sm text-slate-600">
                                            Your student login still uses
                                            your index number. Check the
                                            new email carefully before
                                            saving; it will be used for
                                            account recovery.
                                        </p>
                                    )}

                                    {saveError && (
                                        <p
                                            role="alert"
                                            className="my-4 rounded-xl bg-red-50 p-3 text-sm text-red-700"
                                        >
                                            {saveError}
                                        </p>
                                    )}

                                    {success && (
                                        <p
                                            role="status"
                                            className="my-4 rounded-xl bg-emerald-50 p-3 text-sm text-emerald-700"
                                        >
                                            {success}
                                        </p>
                                    )}

                                    {isEditing && (
                                        <div className="flex flex-col gap-3 border-t border-slate-100 py-5 sm:flex-row">
                                            <button
                                                type="submit"
                                                disabled={saving}
                                                className="cursor-pointer rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                                            >
                                                {saving
                                                    ? "Saving..."
                                                    : "Save Changes"}
                                            </button>

                                            <button
                                                type="button"
                                                onClick={
                                                    cancelEditing
                                                }
                                                disabled={saving}
                                                className="cursor-pointer rounded-xl border border-slate-300 px-5 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                                            >
                                                Cancel
                                            </button>
                                        </div>
                                    )}
                                </form>
                            </section>

                            <section className="min-w-0 rounded-2xl border border-slate-200 bg-white shadow-sm">
                                <SectionHeader
                                    icon={
                                        <School className="h-5 w-5" />
                                    }
                                    title="Academic Placement"
                                    subtitle="Your current academic enrollment"
                                />

                                {profile.currentEnrollment ? (
                                    <div className="divide-y divide-slate-100 px-6">
                                        <ProfileRow
                                            label="Academic Year"
                                            value={
                                                profile
                                                    .currentEnrollment
                                                    .academicYear
                                            }
                                        />
                                        <ProfileRow
                                            label="Section"
                                            value={
                                                profile
                                                    .currentEnrollment
                                                    .section
                                            }
                                        />
                                        <ProfileRow
                                            label="Grade"
                                            value={
                                                profile
                                                    .currentEnrollment
                                                    .grade
                                            }
                                        />
                                        <ProfileRow
                                            label="Class"
                                            value={
                                                profile
                                                    .currentEnrollment
                                                    .class
                                            }
                                        />
                                        <ProfileRow
                                            label="Enrollment Date"
                                            value={formatDate(
                                                profile
                                                    .currentEnrollment
                                                    .enrollmentDate
                                            )}
                                        />
                                    </div>
                                ) : (
                                    <p className="m-6 rounded-xl bg-amber-50 p-4 text-sm text-amber-700">
                                        No current academic enrollment
                                        is available.
                                    </p>
                                )}
                            </section>
                        </div>

                        <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                            <h2 className="font-semibold text-slate-950">
                                Student Status
                            </h2>

                            <div className="mt-5 grid gap-4 sm:grid-cols-2">
                                <StatusCard
                                    label="Account Status"
                                    value={
                                        profile.isActive
                                            ? "Active"
                                            : "Inactive"
                                    }
                                />
                                <StatusCard
                                    label="Graduation Status"
                                    value={
                                        profile.isGraduated
                                            ? "Graduated"
                                            : "Not Graduated"
                                    }
                                />
                            </div>
                        </section>
                    </>
                )}
            </div>
        </StudentPageShell>
    );
}

function StudentPageShell({ children }) {
    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex min-h-20 max-w-7xl items-center gap-3 px-5 sm:px-7 lg:px-8">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-600 text-white">
                        <GraduationCap className="h-5 w-5" />
                    </div>
                    <div>
                        <p className="font-bold text-slate-950">
                            School Management
                        </p>
                        <p className="text-xs text-slate-500">
                            Student Portal
                        </p>
                    </div>
                </div>
            </header>

            <main className="px-5 py-8 sm:px-7 lg:px-8 lg:py-10">
                {children}
            </main>
        </div>
    );
}

function SectionHeader({ icon, title, subtitle }) {
    return (
        <div className="flex items-center gap-3 border-b border-slate-100 px-6 py-5">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                {icon}
            </div>
            <div>
                <h2 className="font-semibold text-slate-950">
                    {title}
                </h2>
                <p className="text-xs text-slate-500">
                    {subtitle}
                </p>
            </div>
        </div>
    );
}

function ProfileRow({ icon, label, value }) {
    return (
        <div className="flex min-w-0 items-start justify-between gap-4 border-b border-slate-100 py-4 last:border-0">
            <span className="flex min-w-0 items-center gap-3 text-sm text-slate-500">
                {icon && (
                    <span className="text-slate-400">{icon}</span>
                )}
                {label}
            </span>

            <span className="max-w-[60%] break-words text-right text-sm font-semibold text-slate-900">
                {value || "—"}
            </span>
        </div>
    );
}

function EditRow({
    label,
    type,
    value,
    onChange,
    ...inputProps
}) {
    return (
        <label className="block border-b border-slate-100 py-4 text-sm font-semibold text-slate-700">
            {label}
            <input
                type={type}
                value={value}
                onChange={(event) =>
                    onChange(event.target.value)
                }
                {...inputProps}
                className="mt-2 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-base font-normal text-slate-900 outline-none focus:border-blue-500"
            />
        </label>
    );
}

function StatusCard({ label, value }) {
    return (
        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>
            <p className="mt-2 font-semibold text-slate-950">
                {value}
            </p>
        </div>
    );
}

function formatDate(value) {
    if (!value) return "—";

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) return value;

    return date.toLocaleDateString(undefined, {
        year: "numeric",
        month: "short",
        day: "numeric",
    });
}