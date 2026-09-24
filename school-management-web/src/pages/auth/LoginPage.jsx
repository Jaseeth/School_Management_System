import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
    Eye,
    EyeOff,
    LockKeyhole,
    Mail,
    School,
} from "lucide-react";
import { useAuth } from "../../context/AuthContext";

export default function LoginPage() {
    const navigate = useNavigate();
    const { login } = useAuth();

    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [showPassword, setShowPassword] =
        useState(false);

    const [loading, setLoading] =
        useState(false);

    const [error, setError] =
        useState("");

    const handleSubmit = async (e) => {
        e.preventDefault();

        setError("");

        if (!email.trim() || !password.trim()) {
            setError(
                "Please enter your email and password."
            );

            return;
        }

        try {
            setLoading(true);

            const result = await login(
                email.trim(),
                password
            );

            if (result.mustChangePassword) {
                navigate(
                    "/change-password"
                );

                return;
            }

            navigate("/dashboard");
        } catch (err) {
            const message =
                err?.response?.data?.message ||
                "Unable to login. Please try again.";

            setError(message);
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen bg-slate-950">
            <div className="grid min-h-screen lg:grid-cols-2">
                {/* LEFT SIDE */}
                <div className="relative hidden overflow-hidden lg:flex">
                    <div className="absolute inset-0 bg-gradient-to-br from-slate-950 via-blue-950 to-indigo-950" />

                    <div className="absolute -left-24 top-20 h-72 w-72 rounded-full bg-blue-500/20 blur-3xl" />

                    <div className="absolute bottom-0 right-0 h-96 w-96 rounded-full bg-indigo-500/20 blur-3xl" />

                    <div className="relative z-10 flex w-full flex-col justify-between p-12 xl:p-16">
                        <div className="flex items-center gap-3">
                            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-white/10 backdrop-blur">
                                <School className="h-6 w-6 text-white" />
                            </div>

                            <div>
                                <p className="text-lg font-semibold text-white">
                                    School Management
                                </p>

                                <p className="text-sm text-slate-300">
                                    Smart Education System
                                </p>
                            </div>
                        </div>

                        <div className="max-w-xl">
                            <div className="mb-6 inline-flex rounded-full border border-white/10 bg-white/5 px-4 py-2 text-sm text-blue-200 backdrop-blur">
                                Secure • Simple • Connected
                            </div>

                            <h1 className="text-4xl font-semibold leading-tight text-white xl:text-5xl">
                                One intelligent system for
                                your entire school.
                            </h1>

                            <p className="mt-6 max-w-lg text-lg leading-8 text-slate-300">
                                Manage students, staff,
                                attendance, academics,
                                communication, parents and
                                reporting from one secure
                                workspace.
                            </p>
                        </div>

                        <p className="text-sm text-slate-400">
                            School Management System
                        </p>
                    </div>
                </div>

                {/* RIGHT SIDE */}
                <div className="flex items-center justify-center bg-slate-50 px-6 py-12 sm:px-10">
                    <div className="w-full max-w-md">
                        {/* MOBILE BRAND */}
                        <div className="mb-10 flex items-center gap-3 lg:hidden">
                            <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-slate-900">
                                <School className="h-5 w-5 text-white" />
                            </div>

                            <div>
                                <p className="font-semibold text-slate-900">
                                    School Management
                                </p>

                                <p className="text-xs text-slate-500">
                                    Smart Education System
                                </p>
                            </div>
                        </div>

                        <div>
                            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-blue-600">
                                Welcome back
                            </p>

                            <h2 className="mt-3 text-3xl font-bold tracking-tight text-slate-950">
                                Sign in to your account
                            </h2>

                            <p className="mt-3 text-sm leading-6 text-slate-500">
                                Enter your registered
                                credentials to continue.
                            </p>
                        </div>

                        <form
                            onSubmit={handleSubmit}
                            className="mt-8 space-y-5"
                        >
                            {/* EMAIL */}
                            <div>
                                <label className="mb-2 block text-sm font-medium text-slate-700">
                                    Email address
                                </label>

                                <div className="relative">
                                    <Mail className="absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400" />

                                    <input
                                        type="email"
                                        value={email}
                                        onChange={(e) =>
                                            setEmail(
                                                e.target.value
                                            )
                                        }
                                        placeholder="name@school.com"
                                        autoComplete="email"
                                        className="h-12 w-full rounded-xl border border-slate-200 bg-white pl-12 pr-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                    />
                                </div>
                            </div>

                            {/* PASSWORD */}
                            <div>
                                <div className="mb-2 flex items-center justify-between">
                                    <label className="text-sm font-medium text-slate-700">
                                        Password
                                    </label>

                                    <button
                                        type="button"
                                        className="text-sm font-medium text-blue-600 transition hover:text-blue-700"
                                    >
                                        Forgot password?
                                    </button>
                                </div>

                                <div className="relative">
                                    <LockKeyhole className="absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400" />

                                    <input
                                        type={
                                            showPassword
                                                ? "text"
                                                : "password"
                                        }
                                        value={password}
                                        onChange={(e) =>
                                            setPassword(
                                                e.target.value
                                            )
                                        }
                                        placeholder="Enter your password"
                                        autoComplete="current-password"
                                        className="h-12 w-full rounded-xl border border-slate-200 bg-white pl-12 pr-12 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                    />

                                    <button
                                        type="button"
                                        onClick={() =>
                                            setShowPassword(
                                                (current) =>
                                                    !current
                                            )
                                        }
                                        className="absolute right-4 top-1/2 -translate-y-1/2 text-slate-400 transition hover:text-slate-600"
                                    >
                                        {showPassword ? (
                                            <EyeOff className="h-5 w-5" />
                                        ) : (
                                            <Eye className="h-5 w-5" />
                                        )}
                                    </button>
                                </div>
                            </div>

                            {/* ERROR */}
                            {error && (
                                <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                                    {error}
                                </div>
                            )}

                            {/* LOGIN BUTTON */}
                            <button
                                type="submit"
                                disabled={loading}
                                className="flex h-12 w-full items-center justify-center rounded-xl bg-slate-950 px-5 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 focus:outline-none focus:ring-4 focus:ring-blue-500/20 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                {loading
                                    ? "Signing in..."
                                    : "Sign in"}
                            </button>
                        </form>

                        <div className="mt-8 border-t border-slate-200 pt-6">
                            <p className="text-center text-xs leading-5 text-slate-400">
                                Secure access for authorized
                                school staff, students and
                                parents.
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}