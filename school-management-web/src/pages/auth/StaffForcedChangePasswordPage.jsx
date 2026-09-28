import { Eye, EyeOff, KeyRound, LogOut } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import api from "../../api/axios";
import { useAuth } from "../../context/AuthContext";

export default function StaffForcedChangePasswordPage() {
    const navigate = useNavigate();
    const { logout, clearPasswordChangeRequirement } = useAuth();

    const [form, setForm] = useState({
        currentPassword: "",
        newPassword: "",
        confirmPassword: "",
    });
    const [visible, setVisible] = useState({});
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");

    function change(event) {
        const { name, value } = event.target;

        setForm((current) => ({
            ...current,
            [name]: value,
        }));
        setError("");
    }

    async function submit(event) {
        event.preventDefault();
        if (busy) return;

        setError("");

        if (form.newPassword !== form.confirmPassword) {
            setError("New password and confirmation do not match.");
            return;
        }

        setBusy(true);

        try {
            await api.post("/Auth/change-password", form);

            setForm({
                currentPassword: "",
                newPassword: "",
                confirmPassword: "",
            });

            clearPasswordChangeRequirement();
            navigate("/dashboard", { replace: true });
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
            setBusy(false);
        }
    }

    function signOut() {
        logout();
        navigate("/login", { replace: true });
    }

    return (
        <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-10 sm:px-6">
            <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
                <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                    <KeyRound className="h-6 w-6" />
                </div>

                <h1 className="mt-5 text-2xl font-bold text-slate-950 sm:text-3xl">
                    Change your temporary password
                </h1>

                <p className="mt-2 text-sm text-slate-600">
                    Enter your temporary password and choose a new one
                    to continue to your dashboard.
                </p>

                <form onSubmit={submit} className="mt-7 space-y-5">
                    <PasswordField
                        label="Current temporary password"
                        name="currentPassword"
                        autoComplete="current-password"
                        value={form.currentPassword}
                        onChange={change}
                        shown={!!visible.currentPassword}
                        disabled={busy}
                        toggle={() =>
                            setVisible((current) => ({
                                ...current,
                                currentPassword:
                                    !current.currentPassword,
                            }))
                        }
                    />

                    <PasswordField
                        label="New password"
                        name="newPassword"
                        autoComplete="new-password"
                        value={form.newPassword}
                        onChange={change}
                        shown={!!visible.newPassword}
                        disabled={busy}
                        toggle={() =>
                            setVisible((current) => ({
                                ...current,
                                newPassword: !current.newPassword,
                            }))
                        }
                    />

                    <PasswordField
                        label="Confirm new password"
                        name="confirmPassword"
                        autoComplete="new-password"
                        value={form.confirmPassword}
                        onChange={change}
                        shown={!!visible.confirmPassword}
                        disabled={busy}
                        toggle={() =>
                            setVisible((current) => ({
                                ...current,
                                confirmPassword:
                                    !current.confirmPassword,
                            }))
                        }
                    />

                    {error && (
                        <p
                            role="alert"
                            className="rounded-xl bg-red-50 p-3 text-sm text-red-700"
                        >
                            {error}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={busy}
                        className="w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                        {busy ? "Saving..." : "Change password"}
                    </button>
                </form>

                <button
                    type="button"
                    onClick={signOut}
                    className="mt-5 inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600 hover:text-slate-900"
                >
                    <LogOut className="h-4 w-4" />
                    Sign out
                </button>
            </div>
        </main>
    );
}

function PasswordField({
    label,
    name,
    autoComplete,
    value,
    onChange,
    shown,
    toggle,
    disabled,
}) {
    return (
        <div>
            <label
                htmlFor={name}
                className="block text-sm font-semibold text-slate-700"
            >
                {label}
            </label>

            <div className="relative mt-2">
                <input
                    id={name}
                    name={name}
                    type={shown ? "text" : "password"}
                    value={value}
                    onChange={onChange}
                    autoComplete={autoComplete}
                    required
                    disabled={disabled}
                    className="block w-full rounded-xl border border-slate-200 bg-white px-4 py-3 pr-12 text-base text-slate-950 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />

                <button
                    type="button"
                    onClick={toggle}
                    aria-label={`${shown ? "Hide" : "Show"
                        } ${label.toLowerCase()}`}
                    className="absolute right-3 top-1/2 -translate-y-1/2 cursor-pointer p-1 text-slate-500 hover:text-slate-800"
                >
                    {shown ? (
                        <EyeOff className="h-5 w-5" />
                    ) : (
                        <Eye className="h-5 w-5" />
                    )}
                </button>
            </div>
        </div>
    );
}