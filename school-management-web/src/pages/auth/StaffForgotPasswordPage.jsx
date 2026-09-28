import {
    ArrowLeft,
    Eye,
    EyeOff,
    KeyRound,
    Mail,
    UserRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { staffForgotPasswordApi } from "../../api/staffForgotPasswordApi";

function getError(err) {
    const data = err?.response?.data;
    const errors = data?.errors;

    return (
        (Array.isArray(errors) ? errors.join(" ") : null) ||
        data?.message ||
        "The request could not be completed. Please try again."
    );
}

export default function StaffForgotPasswordPage() {
    const [step, setStep] = useState("request");
    const [staffNumber, setStaffNumber] = useState("");
    const [email, setEmail] = useState("");
    const [otp, setOtp] = useState("");
    const [newPassword, setNewPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");
    const [showNew, setShowNew] = useState(false);
    const [showConfirm, setShowConfirm] = useState(false);
    const [busy, setBusy] = useState(false);
    const [cooldown, setCooldown] = useState(0);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");

    useEffect(() => {
        if (cooldown <= 0) return;

        const timer = window.setTimeout(
            () => setCooldown((value) => value - 1),
            1000
        );

        return () => window.clearTimeout(timer);
    }, [cooldown]);

    async function sendOtp(event) {
        event?.preventDefault();
        if (busy || cooldown > 0) return;

        setError("");
        setMessage("");
        setBusy(true);

        try {
            const { data } = await staffForgotPasswordApi.requestOtp(
                staffNumber.trim(),
                email.trim()
            );

            setOtp("");
            setStep("verify");
            setCooldown(data?.resendAfterSeconds ?? 60);
            setMessage(
                data?.message || "Check your registered email for the OTP."
            );
        } catch (err) {
            setError(getError(err));

            if (err?.response?.status === 429) {
                setCooldown(
                    err.response.data?.retryAfterSeconds ?? 60
                );
            }
        } finally {
            setBusy(false);
        }
    }

    async function verifyOtp(event) {
        event.preventDefault();
        if (busy) return;

        setError("");
        setMessage("");

        if (!/^\d{6}$/.test(otp.trim())) {
            setError("Enter the 6-digit OTP from your email.");
            return;
        }

        setBusy(true);

        try {
            await staffForgotPasswordApi.verifyOtp(
                staffNumber.trim(),
                email.trim(),
                otp.trim()
            );

            setStep("reset");
            setMessage("OTP verified. Choose a new password.");
        } catch (err) {
            setError(getError(err));
        } finally {
            setBusy(false);
        }
    }

    async function resetPassword(event) {
        event.preventDefault();
        if (busy) return;

        setError("");
        setMessage("");

        if (newPassword !== confirmPassword) {
            setError("New password and confirmation do not match.");
            return;
        }

        setBusy(true);

        try {
            const { data } =
                await staffForgotPasswordApi.resetPassword(
                    staffNumber.trim(),
                    email.trim(),
                    newPassword,
                    confirmPassword
                );

            setNewPassword("");
            setConfirmPassword("");
            setOtp("");
            setStep("done");
            setMessage(
                data?.message || "Password reset successfully."
            );
        } catch (err) {
            setError(getError(err));
        } finally {
            setBusy(false);
        }
    }

    return (
        <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-10 sm:px-6">
            <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
                <Link
                    to="/login"
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-blue-600 hover:text-blue-700"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to staff login
                </Link>

                <div className="mt-7 flex h-12 w-12 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                    <KeyRound className="h-6 w-6" />
                </div>

                <h1 className="mt-5 text-2xl font-bold text-slate-950 sm:text-3xl">
                    Reset staff password
                </h1>

                <p className="mt-2 text-sm text-slate-600">
                    {step === "request" &&
                        "Enter your staff number and registered email to receive an OTP."}
                    {step === "verify" &&
                        "Enter the 6-digit OTP. It expires in 5 minutes."}
                    {step === "reset" &&
                        "Choose a new password for your staff account."}
                    {step === "done" &&
                        "Your password has been reset. You can sign in now."}
                </p>

                {error && (
                    <p
                        role="alert"
                        className="mt-5 rounded-xl bg-red-50 p-3 text-sm text-red-700"
                    >
                        {error}
                    </p>
                )}

                {message && (
                    <p
                        role="status"
                        className="mt-5 rounded-xl bg-emerald-50 p-3 text-sm text-emerald-700"
                    >
                        {message}
                    </p>
                )}

                {step === "request" && (
                    <form
                        onSubmit={sendOtp}
                        className="mt-6 space-y-4"
                    >
                        <Field
                            label="Staff number"
                            icon={UserRound}
                            value={staffNumber}
                            onChange={(event) =>
                                setStaffNumber(event.target.value)
                            }
                            autoComplete="username"
                            required
                        />

                        <Field
                            label="Registered email"
                            icon={Mail}
                            type="email"
                            value={email}
                            onChange={(event) =>
                                setEmail(event.target.value)
                            }
                            autoComplete="email"
                            required
                        />

                        <Submit
                            busy={busy}
                            disabled={cooldown > 0}
                            label="Send OTP"
                        />

                        {cooldown > 0 && (
                            <p className="text-center text-sm text-slate-500">
                                Try again in {cooldown}s
                            </p>
                        )}
                    </form>
                )}

                {step === "verify" && (
                    <form
                        onSubmit={verifyOtp}
                        className="mt-6 space-y-4"
                    >
                        <p className="text-sm text-slate-600">
                            OTP sent to {email}
                        </p>

                        <Field
                            label="6-digit OTP"
                            value={otp}
                            onChange={(event) =>
                                setOtp(
                                    event.target.value
                                        .replace(/\D/g, "")
                                        .slice(0, 6)
                                )
                            }
                            inputMode="numeric"
                            pattern="[0-9]{6}"
                            maxLength={6}
                            autoComplete="one-time-code"
                            required
                        />

                        <Submit
                            busy={busy}
                            label="Verify OTP"
                        />

                        <button
                            type="button"
                            onClick={() => sendOtp()}
                            disabled={busy || cooldown > 0}
                            className="w-full cursor-pointer text-sm font-semibold text-blue-600 hover:text-blue-700 disabled:cursor-not-allowed disabled:text-slate-400"
                        >
                            {cooldown > 0
                                ? `Resend OTP in ${cooldown}s`
                                : "Resend OTP"}
                        </button>
                    </form>
                )}

                {step === "reset" && (
                    <form
                        onSubmit={resetPassword}
                        className="mt-6 space-y-4"
                    >
                        <PasswordField
                            label="New password"
                            value={newPassword}
                            onChange={(event) =>
                                setNewPassword(event.target.value)
                            }
                            visible={showNew}
                            toggle={() =>
                                setShowNew((value) => !value)
                            }
                            required
                            autoComplete="new-password"
                        />

                        <PasswordField
                            label="Confirm new password"
                            value={confirmPassword}
                            onChange={(event) =>
                                setConfirmPassword(event.target.value)
                            }
                            visible={showConfirm}
                            toggle={() =>
                                setShowConfirm((value) => !value)
                            }
                            required
                            autoComplete="new-password"
                        />

                        <Submit
                            busy={busy}
                            label="Reset password"
                        />
                    </form>
                )}

                {step === "done" && (
                    <Link
                        to="/login"
                        className="mt-6 block cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-center text-sm font-semibold text-white hover:bg-blue-700"
                    >
                        Go to staff login
                    </Link>
                )}
            </div>
        </main>
    );
}

function Field({
    label,
    icon: Icon,
    type = "text",
    ...props
}) {
    return (
        <label className="block text-sm font-semibold text-slate-700">
            {label}

            <span className="relative mt-2 block">
                {Icon && (
                    <Icon className="absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400" />
                )}

                <input
                    type={type}
                    {...props}
                    className={`block w-full rounded-xl border border-slate-200 px-4 py-3 text-base text-slate-950 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 ${Icon ? "pl-12" : ""
                        }`}
                />
            </span>
        </label>
    );
}

function PasswordField({
    label,
    value,
    onChange,
    visible,
    toggle,
    ...props
}) {
    const inputId =
        label === "New password"
            ? "staff-new-password"
            : "staff-confirm-password";

    return (
        <div className="block text-sm font-semibold text-slate-700">
            <label htmlFor={inputId}>{label}</label>

            <span className="relative mt-2 block">
                <input
                    id={inputId}
                    type={visible ? "text" : "password"}
                    value={value}
                    onChange={onChange}
                    {...props}
                    className="block w-full rounded-xl border border-slate-200 px-4 py-3 pr-12 text-base text-slate-950 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />

                <button
                    type="button"
                    onClick={toggle}
                    aria-label={
                        visible ? `Hide ${label}` : `Show ${label}`
                    }
                    className="absolute right-3 top-1/2 -translate-y-1/2 cursor-pointer p-1 text-slate-500 hover:text-slate-800"
                >
                    {visible ? (
                        <EyeOff className="h-5 w-5" />
                    ) : (
                        <Eye className="h-5 w-5" />
                    )}
                </button>
            </span>
        </div>
    );
}

function Submit({ busy, label, disabled = false }) {
    return (
        <button
            type="submit"
            disabled={busy || disabled}
            className="w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
            {busy ? "Please wait..." : label}
        </button>
    );
}