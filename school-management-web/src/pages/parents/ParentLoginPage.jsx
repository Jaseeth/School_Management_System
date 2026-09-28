import { GraduationCap, LogIn } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

export default function ParentLoginPage() {
    const navigate = useNavigate();
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");

    async function handleSubmit(event) {
        event.preventDefault();
        setError("");
        setBusy(true);

        try {
            const { data } = await parentPortalApi.login(
                email.trim(),
                password
            );

            if (!data?.token || !data?.roles?.includes("Parent")) {
                setError("This account does not have parent access.");
                return;
            }

            localStorage.removeItem("authUser");
            localStorage.removeItem("studentUser");
            localStorage.setItem("accessToken", data.token);
            localStorage.setItem(
                "parentUser",
                JSON.stringify({
                    fullName: data.fullName,
                    email: data.email,
                    roles: data.roles,
                    expiresAt: data.expiresAt,
                })
            );

            navigate("/parent/dashboard", { replace: true });
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to sign in. Please try again."
            );
        } finally {
            setBusy(false);
        }
    }

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white px-5 py-4">
                <div className="mx-auto flex max-w-6xl items-center gap-3">
                    <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-white">
                        <GraduationCap className="h-6 w-6" />
                    </div>
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

            <main className="mx-auto flex min-h-[calc(100vh-5rem)] max-w-md items-center px-5 py-10">
                <form
                    onSubmit={handleSubmit}
                    className="w-full rounded-2xl border border-slate-200 bg-white p-7 shadow-sm"
                >
                    <h1 className="text-2xl font-bold text-slate-950">
                        Parent Login
                    </h1>
                    <p className="mt-2 text-sm text-slate-500">
                        Sign in using the email and password for your parent account.
                    </p>

                    <label className="mt-7 block text-sm font-semibold text-slate-700">
                        Email
                        <input
                            type="email"
                            required
                            placeholder="example@gmail.com"
                            autoComplete="username"
                            value={email}
                            onChange={(event) => setEmail(event.target.value)}
                            disabled={busy}
                            className="mt-2 w-full rounded-xl border border-slate-200 px-4 py-3 font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                        />
                    </label>

                    <label className="mt-5 block text-sm font-semibold text-slate-700">
                        Password
                        <input
                            type="password"
                            required
                            placeholder="Enter your password"
                            autoComplete="current-password"
                            value={password}
                            onChange={(event) => setPassword(event.target.value)}
                            disabled={busy}
                            className="mt-2 w-full rounded-xl border border-slate-200 px-4 py-3 font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                        />
                    </label>

                    {error && (
                        <p
                            role="alert"
                            className="mt-5 rounded-xl bg-red-50 p-3 text-sm text-red-700"
                        >
                            {error}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={busy}
                        className="mt-6 flex w-full cursor-pointer items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-3 font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                        <LogIn className="h-5 w-5" />
                        {busy ? "Signing in..." : "Sign In"}
                    </button>
                </form>
            </main>
        </div>
    );
}