import {
    ArrowLeft,
    GraduationCap,
    UserRound,
    UsersRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

export default function ParentProfilePage() {
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [form, setForm] = useState({
        fullName: "",
        phoneNumber: "",
        email: "",
        currentPassword: "",
    });
    const [isEditing, setIsEditing] = useState(false);
    const [saving, setSaving] = useState(false);
    const [saveError, setSaveError] = useState("");
    const [success, setSuccess] = useState("");

    useEffect(() => {
        let active = true;

        parentPortalApi
            .getChildren()
            .then(({ data: response }) => {
                if (!active) return;

                setData(response);
                setForm({
                    fullName: response.parent?.fullName ?? "",
                    phoneNumber: response.parent?.phoneNumber ?? "",
                    email: response.parent?.email ?? "",
                    currentPassword: "",
                });
            })
            .catch((err) => {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load your parent profile."
                    );
                }
            })
            .finally(() => {
                if (active) setLoading(false);
            });

        return () => {
            active = false;
        };
    }, []);

    const parent = data?.parent;
    const emailChanged =
        form.email.trim().toLowerCase() !==
        (parent?.email ?? "").toLowerCase();

    function startEditing() {
        setSaveError("");
        setSuccess("");
        setIsEditing(true);
    }

    function cancelEditing() {
        setForm({
            fullName: parent.fullName ?? "",
            phoneNumber: parent.phoneNumber ?? "",
            email: parent.email ?? "",
            currentPassword: "",
        });
        setSaveError("");
        setSuccess("");
        setIsEditing(false);
    }

    async function saveProfile(event) {
        event.preventDefault();

        if (!isEditing || saving) return;

        setSaveError("");
        setSuccess("");

        try {
            setSaving(true);

            const { data: response } =
                await parentPortalApi.updateMyProfile({
                    fullName: form.fullName.trim(),
                    phoneNumber: form.phoneNumber.trim(),
                    email: form.email.trim(),
                    currentPassword: emailChanged
                        ? form.currentPassword
                        : null,
                });

            setData((current) => ({
                ...current,
                parent: response.parent,
            }));

            setForm({
                fullName: response.parent.fullName,
                phoneNumber: response.parent.phoneNumber ?? "",
                email: response.parent.email ?? "",
                currentPassword: "",
            });

            setSuccess(
                response.message || "Profile updated successfully."
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
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex max-w-6xl items-center gap-3 px-5 py-4">
                    <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-white">
                        <GraduationCap className="h-6 w-6" />
                    </span>

                    <div>
                        <p className="font-bold text-slate-950">
                            School Management
                        </p>
                        <p className="text-xs text-slate-500">
                            Parent Portal
                        </p>
                    </div>
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-5 py-9">
                <Link
                    to="/parent/dashboard"
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600 hover:text-blue-700"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to My Children
                </Link>

                <div className="mt-7 flex items-center gap-3 text-blue-600">
                    <UserRound className="h-6 w-6" />
                    <span className="text-sm font-semibold">
                        Parent Account
                    </span>
                </div>

                <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
                    <h1 className="text-3xl font-bold text-slate-950">
                        My Profile
                    </h1>

                    {!loading && !error && parent && !isEditing && (
                        <button
                            type="button"
                            onClick={startEditing}
                            className="cursor-pointer rounded-xl border border-blue-600 bg-white px-5 py-2.5 text-sm font-semibold text-blue-600 transition hover:bg-blue-50"
                        >
                            Edit Profile
                        </button>
                    )}
                </div>

                <p className="mt-2 text-sm text-slate-600">
                    Your parent account and linked children.
                </p>

                {loading && (
                    <p role="status" className="mt-8 text-slate-600">
                        Loading profile...
                    </p>
                )}

                {error && (
                    <p
                        role="alert"
                        className="mt-7 rounded-xl border border-red-100 bg-red-50 p-4 text-sm text-red-700"
                    >
                        {error}
                    </p>
                )}

                {!loading && !error && parent && (
                    <>
                        <form onSubmit={saveProfile} className="mt-7">
                            <div className="grid gap-4 sm:grid-cols-2">
                                {isEditing ? (
                                    <label className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-500 shadow-sm">
                                        Full Name
                                        <input
                                            type="text"
                                            value={form.fullName}
                                            onChange={(event) => {
                                                setForm((current) => ({
                                                    ...current,
                                                    fullName:
                                                        event.target.value,
                                                }));
                                                setSuccess("");
                                            }}
                                            required
                                            maxLength={200}
                                            disabled={saving}
                                            autoComplete="name"
                                            className="mt-2 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-base font-semibold text-slate-950 outline-none focus:border-blue-500"
                                        />
                                    </label>
                                ) : (
                                    <ProfileField
                                        label="Full Name"
                                        value={parent.fullName}
                                    />
                                )}

                                <ProfileField
                                    label="Parent Number"
                                    value={parent.parentNumber}
                                />

                                {isEditing ? (
                                    <label className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-500 shadow-sm">
                                        Login and contact email
                                        <input
                                            type="email"
                                            value={form.email}
                                            onChange={(event) => {
                                                setForm((current) => ({
                                                    ...current,
                                                    email:
                                                        event.target.value,
                                                }));
                                                setSuccess("");
                                            }}
                                            required
                                            maxLength={256}
                                            disabled={saving}
                                            autoComplete="email"
                                            className="mt-2 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-base font-semibold text-slate-950 outline-none focus:border-blue-500"
                                        />
                                    </label>
                                ) : (
                                    <ProfileField
                                        label="Login and contact email"
                                        value={parent.email}
                                    />
                                )}

                                {isEditing ? (
                                    <label className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-500 shadow-sm">
                                        Phone Number
                                        <input
                                            type="tel"
                                            value={form.phoneNumber}
                                            onChange={(event) => {
                                                setForm((current) => ({
                                                    ...current,
                                                    phoneNumber:
                                                        event.target.value,
                                                }));
                                                setSuccess("");
                                            }}
                                            maxLength={50}
                                            disabled={saving}
                                            autoComplete="tel"
                                            className="mt-2 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-base font-semibold text-slate-950 outline-none focus:border-blue-500"
                                        />
                                    </label>
                                ) : (
                                    <ProfileField
                                        label="Phone Number"
                                        value={parent.phoneNumber}
                                    />
                                )}
                            </div>

                            {isEditing && emailChanged && (
                                <label className="mt-4 block rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-500 shadow-sm sm:max-w-md">
                                    Current password (required to change email)
                                    <input
                                        type="password"
                                        value={form.currentPassword}
                                        onChange={(event) => {
                                            setForm((current) => ({
                                                ...current,
                                                currentPassword:
                                                    event.target.value,
                                            }));
                                            setSuccess("");
                                        }}
                                        required
                                        disabled={saving}
                                        autoComplete="current-password"
                                        className="mt-2 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-base font-semibold text-slate-950 outline-none focus:border-blue-500"
                                    />
                                </label>
                            )}

                            {isEditing && (
                                <p className="mt-4 text-sm text-slate-600">
                                    If you change your email, use the new
                                    address the next time you log in. Check
                                    it carefully before saving.
                                </p>
                            )}

                            {saveError && (
                                <p
                                    role="alert"
                                    className="mt-4 rounded-xl border border-red-100 bg-red-50 p-3 text-sm text-red-700"
                                >
                                    {saveError}
                                </p>
                            )}

                            {success && (
                                <p
                                    role="status"
                                    className="mt-4 rounded-xl border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-700"
                                >
                                    {success}
                                </p>
                            )}

                            {isEditing && (
                                <div className="mt-5 flex flex-col gap-3 sm:flex-row">
                                    <button
                                        type="submit"
                                        disabled={saving}
                                        className="w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                                    >
                                        {saving
                                            ? "Saving..."
                                            : "Save Changes"}
                                    </button>

                                    <button
                                        type="button"
                                        onClick={cancelEditing}
                                        disabled={saving}
                                        className="w-full cursor-pointer rounded-xl border border-slate-300 bg-white px-5 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
                                    >
                                        Cancel
                                    </button>
                                </div>
                            )}
                        </form>

                        <section className="mt-9">
                            <div className="flex items-center gap-2">
                                <UsersRound className="h-5 w-5 text-blue-600" />
                                <h2 className="text-xl font-bold text-slate-950">
                                    Linked Children (
                                    {data.totalChildren ?? 0})
                                </h2>
                            </div>

                            {data.children?.length ? (
                                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                                    {data.children.map((entry) => (
                                        <div
                                            key={entry.relationshipId}
                                            className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
                                        >
                                            <p className="break-words font-semibold text-slate-950">
                                                {
                                                    entry.student
                                                        ?.fullName
                                                }
                                            </p>
                                            <p className="mt-2 break-words text-sm text-slate-600">
                                                Index Number:{" "}
                                                {
                                                    entry.student
                                                        ?.indexNumber
                                                }
                                            </p>
                                            <p className="mt-1 text-sm text-slate-600">
                                                Relationship:{" "}
                                                {entry.relationship}
                                            </p>
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <p className="mt-4 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                                    No children are linked to this account
                                    yet.
                                </p>
                            )}
                        </section>
                    </>
                )}
            </main>
        </div>
    );
}

function ProfileField({ label, value }) {
    return (
        <div className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-sm text-slate-500">{label}</p>
            <p className="mt-2 break-words font-semibold text-slate-950">
                {value || "Not provided"}
            </p>
        </div>
    );
}