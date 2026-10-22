import { ArrowLeft, GraduationCap, LockKeyhole } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

const inputClass = "mt-2 block w-full rounded-xl border border-slate-300 bg-white px-4 py-3 font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50";
const buttonClass = "min-h-11 rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-50";

export default function ParentForgotPasswordPage() {
    const [step, setStep] = useState("request");
    const [form, setForm] = useState({ email: "", code: "", newPassword: "", confirmPassword: "" });
    const [challengeId, setChallengeId] = useState("");
    const [grant, setGrant] = useState("");
    const [cooldown, setCooldown] = useState(0);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const pending = useRef(false);
    useEffect(() => {
        if (!cooldown) return;
        const timer = setTimeout(() => setCooldown(value => Math.max(0, value - 1)), 1000);
        return () => clearTimeout(timer);
    }, [cooldown]);
    function change(event) {
        setForm(current => ({ ...current, [event.target.name]: event.target.value }));
        setError("");
    }
    async function run(work) {
        if (pending.current) return;
        pending.current = true; setBusy(true); setError(""); setMessage("");
        try { await work(); }
        catch (e) {
            const data = e.response?.data;
            const details = Array.isArray(data?.errors) ? data.errors.join(" ")
                : data?.errors ? Object.values(data.errors).flat().join(" ") : "";
            setError(details || data?.message || (e.response?.status === 429
                ? "Too many attempts. Please wait a minute before trying again."
                : "Unable to complete the request. Please try again later."));
            if (data?.retryAfterSeconds) setCooldown(data.retryAfterSeconds);
        } finally { pending.current = false; setBusy(false); }
    }
    function requestCode(event) {
        event?.preventDefault();
        if (cooldown) return;
        run(async () => {
            const { data } = await parentPortalApi.requestPasswordResetOtp({ email: form.email.trim() });
            setChallengeId(data.challengeId); setGrant("");
            setForm(current => ({ ...current, code: "", newPassword: "", confirmPassword: "" }));
            setCooldown(data.resendAfterSeconds || 60); setStep("verify"); setMessage(data.message);
        });
    }
    function verify(event) {
        event.preventDefault();
        run(async () => {
            const { data } = await parentPortalApi.verifyPasswordResetOtp({ email: form.email.trim(), challengeId, code: form.code });
            setGrant(data.resetGrant); setStep("reset"); setMessage(data.message);
        });
    }
    function reset(event) {
        event.preventDefault();
        if (form.newPassword !== form.confirmPassword) { setError("New password and confirmation do not match."); return; }
        run(async () => {
            const { data } = await parentPortalApi.resetPassword({ email: form.email.trim(), challengeId,
                resetGrant: grant, newPassword: form.newPassword, confirmPassword: form.confirmPassword });
            setGrant(""); setChallengeId("");
            setForm(current => ({ ...current, code: "", newPassword: "", confirmPassword: "" }));
            localStorage.removeItem("parentAccessToken"); localStorage.removeItem("parentUser");
            window.dispatchEvent(new Event("auth-session-changed"));
            setStep("done"); setMessage(data.message);
        });
    }
    function startOver() {
        setStep("request"); setChallengeId(""); setGrant(""); setError(""); setMessage("");
        setForm(current => ({ ...current, code: "", newPassword: "", confirmPassword: "" }));
    }
    return <div className="min-h-screen bg-slate-50">
        <header className="border-b border-slate-200 bg-white px-5 py-4"><div className="mx-auto flex max-w-6xl items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-600 text-white"><GraduationCap className="h-5 w-5" aria-hidden="true" /></div>
            <div><p className="font-bold text-slate-950">School Management</p><p className="text-xs text-slate-500">Parent Portal</p></div>
        </div></header>
        <main className="mx-auto max-w-md px-5 py-10">
            <Link to="/parent/login" className="portal-back-button"><ArrowLeft className="h-4 w-4" aria-hidden="true" />Back to Parent Login</Link>
            <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                <LockKeyhole className="mb-4 h-7 w-7 text-blue-600" aria-hidden="true" />
                <h1 className="text-2xl font-bold text-slate-950">Forgot password</h1>
                <p className="mt-2 text-sm text-slate-600">{step === "request" ? "Enter the email registered to your parent account." : step === "verify"
                    ? "Enter the six-digit code sent to your registered email. It expires in five minutes; you have five attempts."
                    : step === "reset" ? "Choose your new password before the verification code expires." : "You can now sign in with your new password."}</p>
                {error && <p role="alert" className="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}
                {message && <p role="status" className="mt-4 rounded-xl bg-emerald-50 p-3 text-sm text-emerald-800">{message}</p>}
                {step === "request" && <form onSubmit={requestCode} className="mt-5 space-y-4">
                    <label className="block text-sm font-semibold">Registered email<span className="required-marker" aria-hidden="true"> *</span>
                        <input name="email" type="email" required maxLength={254} autoComplete="email" disabled={busy} value={form.email} onChange={change} className={inputClass} />
                    </label>
                    <button disabled={busy || cooldown > 0} className={`${buttonClass} w-full`}>{busy ? "Sending…" : cooldown ? `Wait ${cooldown}s` : "Send verification code"}</button>
                    <p className="text-sm text-slate-500">If you no longer have access to this email, contact the school to update your parent account.</p>
                </form>}
                {step === "verify" && <form onSubmit={verify} className="mt-5 space-y-4">
                    <p className="break-words text-sm text-slate-600">Email: {form.email}</p>
                    <label className="block text-sm font-semibold">Verification code<span className="required-marker" aria-hidden="true"> *</span>
                        <input name="code" required inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} disabled={busy} value={form.code} onChange={change} className={`${inputClass} tracking-widest`} />
                    </label>
                    <button disabled={busy} className={`${buttonClass} w-full`}>{busy ? "Verifying…" : "Verify code"}</button>
                    <div className="flex flex-wrap justify-between gap-3"><button type="button" disabled={busy || cooldown > 0} onClick={requestCode} className="min-h-11 text-sm font-semibold text-blue-700 disabled:text-slate-400">{cooldown ? `Resend in ${cooldown}s` : "Resend code"}</button>
                        <button type="button" disabled={busy} onClick={startOver} className="min-h-11 text-sm font-semibold text-slate-600">Change email</button></div>
                </form>}
                {step === "reset" && <form onSubmit={reset} className="mt-5 space-y-4">
                    <p className="text-sm text-slate-500">Use at least 8 characters, including uppercase and lowercase letters, a number and a symbol.</p>
                    {[["newPassword", "New password"], ["confirmPassword", "Confirm new password"]].map(([name, label]) => <label key={name} className="block text-sm font-semibold">{label}<span className="required-marker" aria-hidden="true"> *</span>
                        <input name={name} type="password" autoComplete="new-password" required minLength={8} maxLength={128} disabled={busy} value={form[name]} onChange={change} className={inputClass} />
                    </label>)}
                    <button disabled={busy} className={`${buttonClass} w-full`}>{busy ? "Resetting…" : "Reset password"}</button>
                    <button type="button" disabled={busy} onClick={startOver} className="min-h-11 text-sm font-semibold text-blue-700">Start again / request a new code</button>
                </form>}
                {step === "done" && <Link to="/parent/login" className={`${buttonClass} mt-5 inline-flex w-full items-center justify-center`}>Back to Parent Login</Link>}
            </section>
        </main>
    </div>;
}
