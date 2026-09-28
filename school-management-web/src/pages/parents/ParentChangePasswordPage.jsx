import {
    ArrowLeft,
    Eye,
    EyeOff,
    GraduationCap,
    LockKeyhole,
} from "lucide-react";
import { useState } from "react";
import { Link } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

const emptyForm = {
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
};

export default function ParentChangePasswordPage() {
    const [form, setForm] = useState(emptyForm);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [currentFieldReady, setCurrentFieldReady] = useState(false);

    function updateField(event) {
        setForm((current) => ({
            ...current,
            [event.target.name]: event.target.value,
        }));
        setError("");
        setSuccess("");
    }

    async function handleSubmit(event) {
        event.preventDefault();
        setError("");
        setSuccess("");

        if (form.newPassword !== form.confirmPassword) {
            setError("New password and confirmation do not match.");
            return;
        }

        try {
            setSaving(true);

            const { data } =
                await parentPortalApi.changePassword(form);

            setForm(emptyForm);
            setSuccess(
                data?.message || "Password changed successfully."
            );
        } catch (err) {
            const details = err?.response?.data;

            setError(
                (Array.isArray(details?.errors)
                    ? details.errors.join(" ")
                    : null) ||
                details?.message ||
                "Unable to change password. Please try again."
            );
        } finally {
            setSaving(false);
        }
    }

    function prepareCurrentPassword() {
        if (currentFieldReady) return;

        setForm((current) => ({
            ...current,
            currentPassword: "",
        }));
        setCurrentFieldReady(true);
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

                <div className="mx-auto mt-7 max-w-xl">
                    <div className="flex items-center gap-3 text-blue-600">
                        <LockKeyhole className="h-6 w-6" />
                        <span className="text-sm font-semibold">
                            Account Security
                        </span>
                    </div>

                    <h1 className="mt-2 text-3xl font-bold text-slate-950">
                        Change Password
                    </h1>

                    <p className="mt-2 text-sm text-slate-600">
                        Enter your current password and choose a new one.
                    </p>

                    <form
                        onSubmit={handleSubmit}
                        autoComplete="off"
                        className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8"
                    >
                        <PasswordField
                            label="Current Password"
                            name="currentPassword"
                            value={form.currentPassword}
                            onChange={updateField}
                            onFocus={prepareCurrentPassword}
                            autoComplete="off"
                            readOnly={!currentFieldReady}
                            disabled={saving}
                        />

                        <PasswordField
                            label="New Password"
                            name="newPassword"
                            value={form.newPassword}
                            onChange={updateField}
                            autoComplete="new-password"
                            disabled={saving}
                        />

                        <PasswordField
                            label="Confirm New Password"
                            name="confirmPassword"
                            value={form.confirmPassword}
                            onChange={updateField}
                            autoComplete="new-password"
                            disabled={saving}
                        />

                        {error && (
                            <p
                                role="alert"
                                className="mb-5 rounded-xl border border-red-100 bg-red-50 p-3 text-sm text-red-700"
                            >
                                {error}
                            </p>
                        )}

                        {success && (
                            <p
                                role="status"
                                className="mb-5 rounded-xl border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-700"
                            >
                                {success}
                            </p>
                        )}

                        <button
                            type="submit"
                            disabled={saving}
                            className="w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                            {saving ? "Saving..." : "Change Password"}
                        </button>
                    </form>
                </div>
            </main>
        </div>
    );
}

function PasswordField({
    label,
    name,
    value,
    onChange,
    onFocus,
    autoComplete,
    disabled,
    readOnly = false,
}) {
    const [visible, setVisible] = useState(false);
    const id = `parent-${name}`;

    return (
        <div className="mb-5">
            <label
                htmlFor={id}
                className="block text-sm font-semibold text-slate-700"
            >
                {label}
            </label>

            <div className="relative mt-2">
                <input
                    id={id}
                    type={visible ? "text" : "password"}
                    name={name}
                    value={value}
                    onChange={onChange}
                    onFocus={onFocus}
                    autoComplete={autoComplete}
                    disabled={disabled}
                    readOnly={readOnly}
                    required
                    className="block w-full rounded-xl border border-slate-200 bg-white px-4 py-3 pr-12 font-normal text-slate-900 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />

                <button
                    type="button"
                    onClick={() =>
                        setVisible((current) => !current)
                    }
                    aria-label={
                        visible
                            ? `Hide ${label.toLowerCase()}`
                            : `Show ${label.toLowerCase()}`
                    }
                    aria-pressed={visible}
                    className="absolute inset-y-0 right-0 flex w-12 cursor-pointer items-center justify-center rounded-r-xl text-slate-500 hover:text-blue-700"
                >
                    {visible ? (
                        <Eye className="h-5 w-5" />
                    ) : (
                        <EyeOff className="h-5 w-5" />
                    )}
                </button>
            </div>
        </div>
    );
}