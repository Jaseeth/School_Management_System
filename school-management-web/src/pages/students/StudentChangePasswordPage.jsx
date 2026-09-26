import { ArrowLeft, GraduationCap, LockKeyhole } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { studentAuthApi } from "../../api/studentAuthApi";

export default function StudentChangePasswordPage() {
    const navigate = useNavigate();
    const [form, setForm] = useState({
        currentPassword: "",
        newPassword: "",
        confirmPassword: "",
    });
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");

    const updateField = (event) => {
        const { name, value } = event.target;
        setForm((current) => ({ ...current, [name]: value }));
        setError("");
        setSuccess("");
    };

    const handleSubmit = async (event) => {
        event.preventDefault();
        setError("");
        setSuccess("");

        if (!form.currentPassword || !form.newPassword || !form.confirmPassword) {
            setError("Please fill in all three password fields.");
            return;
        }

        if (form.newPassword !== form.confirmPassword) {
            setError("New password and confirmation do not match.");
            return;
        }

        try {
            setSaving(true);
            const response = await studentAuthApi.changePassword(form);
            setForm({
                currentPassword: "",
                newPassword: "",
                confirmPassword: "",
            });
            setSuccess(response.data?.message || "Password changed successfully.");
        } catch (err) {
            const details = err?.response?.data;
            setError(
                details?.errors?.join(" ") ||
                details?.message ||
                "Unable to change password. Please try again."
            );
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex min-h-20 max-w-7xl items-center gap-3 px-5 sm:px-7 lg:px-8">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-600 text-white">
                        <GraduationCap className="h-5 w-5" />
                    </div>
                    <div>
                        <p className="font-bold text-slate-950">School Management</p>
                        <p className="text-xs text-slate-500">Student Portal</p>
                    </div>
                </div>
            </header>

            <main className="mx-auto w-full max-w-7xl px-5 py-8 sm:px-7 lg:px-8 lg:py-10">
                <button
                    type="button"
                    onClick={() => navigate("/student/dashboard")}
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>

                <div className="mx-auto mt-6 max-w-xl">
                    <p className="text-sm font-semibold text-blue-600">Account Security</p>
                    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                        Change Password
                    </h1>
                    <p className="mt-2 text-sm text-slate-500">
                        Enter your current password and choose a new one.
                    </p>

                    <form
                        onSubmit={handleSubmit}
                        className="mt-7 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"
                    >
                        <div className="mb-6 flex h-12 w-12 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                            <LockKeyhole className="h-6 w-6" />
                        </div>

                        <PasswordField
                            label="Current Password"
                            name="currentPassword"
                            value={form.currentPassword}
                            onChange={updateField}
                            autoComplete="current-password"
                        />
                        <PasswordField
                            label="New Password"
                            name="newPassword"
                            value={form.newPassword}
                            onChange={updateField}
                            autoComplete="new-password"
                        />
                        <PasswordField
                            label="Confirm New Password"
                            name="confirmPassword"
                            value={form.confirmPassword}
                            onChange={updateField}
                            autoComplete="new-password"
                        />

                        {error && (
                            <p role="alert" className="mb-5 rounded-xl border border-red-100 bg-red-50 p-3 text-sm text-red-700">
                                {error}
                            </p>
                        )}
                        {success && (
                            <p role="status" className="mb-5 rounded-xl border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-700">
                                {success}
                            </p>
                        )}

                        <button
                            type="submit"
                            disabled={saving}
                            className="w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                            {saving ? "Saving..." : "Change Password"}
                        </button>
                    </form>
                </div>
            </main>
        </div>
    );
}

function PasswordField({ label, name, value, onChange, autoComplete }) {
    return (
        <label className="mb-5 block text-sm font-semibold text-slate-700">
            {label}
            <input
                type="password"
                name={name}
                value={value}
                onChange={onChange}
                autoComplete={autoComplete}
                required
                className="mt-2 block w-full rounded-xl border border-slate-200 bg-white px-4 py-3 font-normal text-slate-900 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
        </label>
    );
}