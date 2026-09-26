import { ArrowLeft, GraduationCap, LockKeyhole } from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { studentRegistrationApi } from "../../api/studentRegistrationApi";

export default function StudentForgotPasswordPage() {
    const [step, setStep] = useState("request");
    const [form, setForm] = useState({
        indexNumber: "",
        email: "",
        otp: "",
        newPassword: "",
        confirmPassword: "",
    });
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [cooldown, setCooldown] = useState(0);

    useEffect(() => {
        if (cooldown <= 0) return;
        const timer = setTimeout(() => setCooldown((seconds) => seconds - 1), 1000);
        return () => clearTimeout(timer);
    }, [cooldown]);

    function change(event) {
        const { name, value } = event.target;
        setForm((current) => ({ ...current, [name]: value }));
        setError("");
    }

    function showError(err) {
        console.error("Student password reset request failed:", err);

        const status = err?.response?.status;
        const data = err?.response?.data;

        if (data?.retryAfterSeconds) {
            setCooldown(data.retryAfterSeconds);
        }

        if (status === 429) {
            setError(
                data?.message || "Too many requests. Please wait a minute and try again."
            );
            return;
        }

        if (!err?.response) {
            setError(
                err?.isAxiosError
                    ? "Cannot reach the backend. Check that the API is running."
                    : "The page could not make the request. Check the browser console and API method."
            );
            return;
        }

        setError(
            (Array.isArray(data?.errors) && data.errors.join(" ")) ||
            data?.message ||
            data?.title ||
            (status === 404 ? "Password reset endpoint was not found." : null) ||
            (status >= 500
                ? "The backend could not process this request. Check its logs."
                : null) ||
            "Unable to process the request."
        );
    }

    async function requestOtp(event) {
        event?.preventDefault();
        if (busy || cooldown > 0) return;

        setError("");
        setMessage("");
        setBusy(true);

        try {
            const response = await studentRegistrationApi.requestPasswordResetOtp({
                indexNumber: form.indexNumber.trim(),
                email: form.email.trim(),
            });

            setForm((current) => ({ ...current, otp: "" }));
            setCooldown(response.data?.resendAfterSeconds ?? 60);
            setMessage(
                response.data?.message || "Check your registered email for the OTP."
            );
            setStep("verify");
        } catch (err) {
            showError(err);
        } finally {
            setBusy(false);
        }
    }

    async function verifyOtp(event) {
        event.preventDefault();
        setError("");
        setMessage("");
        setBusy(true);

        try {
            await studentRegistrationApi.verifyPasswordResetOtp({
                indexNumber: form.indexNumber.trim(),
                email: form.email.trim(),
                otp: form.otp.trim(),
            });
            setStep("reset");
            setMessage("OTP verified. Choose a new password.");
        } catch (err) {
            showError(err);
        } finally {
            setBusy(false);
        }
    }

    async function resetPassword(event) {
        event.preventDefault();
        setError("");
        setMessage("");

        if (form.newPassword !== form.confirmPassword) {
            setError("New password and confirmation do not match.");
            return;
        }

        setBusy(true);
        try {
            const response = await studentRegistrationApi.resetStudentPassword({
                indexNumber: form.indexNumber.trim(),
                email: form.email.trim(),
                newPassword: form.newPassword,
                confirmPassword: form.confirmPassword,
            });
            setForm({
                indexNumber: "",
                email: "",
                otp: "",
                newPassword: "",
                confirmPassword: "",
            });
            setMessage(response.data?.message || "Password reset successfully.");
            setStep("done");
        } catch (err) {
            showError(err);
        } finally {
            setBusy(false);
        }
    }

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

            <main className="mx-auto w-full max-w-md px-5 py-10">
                <Link
                    to="/student/login"
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600 hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" /> Back to Student Login
                </Link>

                <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
                    <div className="mb-5 flex h-12 w-12 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                        <LockKeyhole className="h-6 w-6" />
                    </div>
                    <h1 className="text-2xl font-bold text-slate-950">Forgot Password</h1>
                    <p className="mt-2 text-sm text-slate-500">
                        {step === "request" && "Enter your index number and registered email."}
                        {step === "verify" && "Enter the 6-digit OTP sent to your registered email. It expires in 5 minutes."}
                        {step === "reset" && "Enter and confirm your new password."}
                        {step === "done" && "You can now sign in with your new password."}
                    </p>

                    {error && (
                        <p role="alert" className="mt-5 rounded-xl bg-red-50 p-3 text-sm text-red-700">
                            {error}
                        </p>
                    )}
                    {message && (
                        <p role="status" className="mt-5 rounded-xl bg-emerald-50 p-3 text-sm text-emerald-700">
                            {message}
                        </p>
                    )}

                    {step === "request" && (
                        <form onSubmit={requestOtp} className="mt-6 space-y-5">
                            <Field
                                label="Index Number"
                                name="indexNumber"
                                value={form.indexNumber}
                                onChange={change}
                                autoComplete="username"
                            />
                            <Field
                                label="Registered Email"
                                name="email"
                                type="email"
                                value={form.email}
                                onChange={change}
                                autoComplete="email"
                            />
                            <Submit busy={busy} label="Send OTP" disabled={cooldown > 0} />
                            {cooldown > 0 && (
                                <p className="text-sm text-slate-500">
                                    Try again in {cooldown} seconds.
                                </p>
                            )}
                        </form>
                    )}

                    {step === "verify" && (
                        <form onSubmit={verifyOtp} className="mt-6 space-y-5">
                            <Field
                                label="6-digit OTP"
                                name="otp"
                                value={form.otp}
                                onChange={change}
                                inputMode="numeric"
                                pattern="[0-9]{6}"
                                maxLength={6}
                                autoComplete="one-time-code"
                            />
                            <Submit busy={busy} label="Verify OTP" />
                            <button
                                type="button"
                                onClick={() => requestOtp()}
                                disabled={busy || cooldown > 0}
                                className="cursor-pointer text-sm font-semibold text-blue-600 hover:text-blue-700 disabled:cursor-not-allowed disabled:text-slate-400"
                            >
                                {cooldown > 0 ? `Resend OTP in ${cooldown}s` : "Resend OTP"}
                            </button>
                        </form>
                    )}

                    {step === "reset" && (
                        <form onSubmit={resetPassword} className="mt-6 space-y-5">
                            <Field
                                label="New Password"
                                name="newPassword"
                                type="password"
                                value={form.newPassword}
                                onChange={change}
                                autoComplete="new-password"
                            />
                            <Field
                                label="Confirm New Password"
                                name="confirmPassword"
                                type="password"
                                value={form.confirmPassword}
                                onChange={change}
                                autoComplete="new-password"
                            />
                            <Submit busy={busy} label="Reset Password" />
                        </form>
                    )}

                    {step === "done" && (
                        <Link
                            to="/student/login"
                            className="mt-6 block cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-center text-sm font-semibold text-white hover:bg-blue-700"
                        >
                            Go to Student Login
                        </Link>
                    )}
                </div>
            </main>
        </div>
    );
}

function Field({ label, ...props }) {
    return (
        <label className="block text-sm font-semibold text-slate-700">
            {label}
            <input
                required
                {...props}
                className="mt-2 block w-full rounded-xl border border-slate-200 px-4 py-3 font-normal text-slate-950 outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
            />
        </label>
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