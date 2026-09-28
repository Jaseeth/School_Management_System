import { useEffect, useState } from "react";
import { staffProfileApi } from "../../api/staffProfileApi";
import { useAuth } from "../../context/AuthContext";

export default function StaffProfilePage() {
    const { updateAccountDetails } = useAuth();

    const [profile, setProfile] = useState(null);
    const [form, setForm] = useState({
        fullName: "",
        email: "",
        currentPassword: "",
    });
    const [loading, setLoading] = useState(true);
    const [loadingError, setLoadingError] = useState("");
    const [saving, setSaving] = useState(false);
    const [editing, setEditing] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    useEffect(() => {
        let active = true;

        staffProfileApi.getProfile()
            .then(({ data }) => {
                if (!active) return;

                setProfile(data);
                setForm({
                    fullName: data.fullName ?? "",
                    email: data.email ?? "",
                    currentPassword: "",
                });
            })
            .catch((err) => {
                if (active) {
                    setLoadingError(
                        err?.response?.data?.message ||
                        "Unable to load staff profile."
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

    const emailChanged =
        form.email.trim().toLowerCase() !==
        (profile?.email ?? "").toLowerCase();

    const cancel = () => {
        setForm({
            fullName: profile.fullName ?? "",
            email: profile.email ?? "",
            currentPassword: "",
        });
        setError("");
        setSuccess("");
        setEditing(false);
    };

    const save = async (event) => {
        event.preventDefault();
        if (!editing || saving) return;

        setError("");
        setSuccess("");

        try {
            setSaving(true);

            const { data } = await staffProfileApi.updateProfile({
                fullName: form.fullName.trim(),
                email: form.email.trim(),
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
                currentPassword: "",
            });

            updateAccountDetails(data.profile);
            setSuccess(data.message || "Profile updated successfully.");
            setEditing(false);
        } catch (err) {
            const details = err?.response?.data;

            setError(
                (Array.isArray(details?.errors)
                    ? details.errors.join(" ")
                    : null) ||
                details?.message ||
                "Unable to save staff profile."
            );
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="mx-auto w-full max-w-5xl px-4 py-7 sm:px-6 lg:px-8">
            <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                    <p className="text-sm font-semibold text-blue-600">
                        Staff Account
                    </p>
                    <h1 className="mt-2 text-3xl font-bold text-slate-950">
                        My Profile
                    </h1>
                    <p className="mt-2 text-sm text-slate-600">
                        Your staff details and login email.
                    </p>
                </div>

                {profile && !editing && (
                    <button
                        type="button"
                        onClick={() => {
                            setError("");
                            setSuccess("");
                            setEditing(true);
                        }}
                        className="w-full cursor-pointer rounded-xl border border-blue-600 bg-white px-5 py-2.5 text-sm font-semibold text-blue-600 hover:bg-blue-50 sm:w-auto"
                    >
                        Edit Profile
                    </button>
                )}
            </div>

            {loading && (
                <p role="status" className="mt-8 text-slate-600">
                    Loading profile...
                </p>
            )}

            {loadingError && (
                <p role="alert" className="mt-6 rounded-xl bg-red-50 p-4 text-sm text-red-700">
                    {loadingError}
                </p>
            )}

            {profile && (
                <form onSubmit={save} className="mt-7">
                    <div className="grid gap-4 sm:grid-cols-2">
                        {editing ? (
                            <Field
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
                            <Detail label="Full Name" value={profile.fullName} />
                        )}

                        <Detail
                            label="Staff Number"
                            value={profile.staffNumber}
                        />

                        {editing ? (
                            <Field
                                label="Login and contact email"
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
                            <Detail
                                label="Login and contact email"
                                value={profile.email}
                            />
                        )}

                        <Detail
                            label="Designation"
                            value={profile.designation}
                        />
                    </div>

                    {editing && emailChanged && (
                        <div className="mt-4 max-w-lg">
                            <Field
                                label="Current password (required to change email)"
                                type="password"
                                value={form.currentPassword}
                                onChange={(value) =>
                                    setForm((current) => ({
                                        ...current,
                                        currentPassword: value,
                                    }))
                                }
                                required
                                disabled={saving}
                                autoComplete="current-password"
                            />
                        </div>
                    )}

                    {error && (
                        <p role="alert" className="mt-4 rounded-xl bg-red-50 p-4 text-sm text-red-700">
                            {error}
                        </p>
                    )}

                    {success && (
                        <p role="status" className="mt-4 rounded-xl bg-emerald-50 p-4 text-sm text-emerald-700">
                            {success}
                        </p>
                    )}

                    {editing && (
                        <div className="mt-5 flex flex-col gap-3 sm:flex-row">
                            <button
                                type="submit"
                                disabled={saving}
                                className="cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                {saving ? "Saving..." : "Save Changes"}
                            </button>

                            <button
                                type="button"
                                onClick={cancel}
                                disabled={saving}
                                className="cursor-pointer rounded-xl border border-slate-300 bg-white px-5 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                Cancel
                            </button>
                        </div>
                    )}
                </form>
            )}
        </div>
    );
}

function Detail({ label, value }) {
    return (
        <div className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <p className="text-sm text-slate-500">{label}</p>
            <p className="mt-2 break-words font-semibold text-slate-950">
                {value || "Not provided"}
            </p>
        </div>
    );
}

function Field({ label, type, value, onChange, ...props }) {
    return (
        <label className="block min-w-0 rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-500 shadow-sm">
            {label}
            <input
                type={type}
                value={value}
                onChange={(event) => onChange(event.target.value)}
                {...props}
                className="mt-2 block w-full rounded-xl border border-slate-200 px-3 py-2.5 text-base font-semibold text-slate-950 outline-none focus:border-blue-500"
            />
        </label>
    );
}