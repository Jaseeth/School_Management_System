import {
    Eye,
    EyeOff,
    GraduationCap,
    LockKeyhole,
    LogIn,
    UserRound,
} from "lucide-react";
import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { studentAuthApi } from "../../api/studentAuthApi";

export default function StudentLoginPage() {
    const navigate = useNavigate();
    const [form, setForm] = useState({
        indexNumber: "",
        password: "",
    });
    const [showPassword, setShowPassword] = useState(false);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const handleChange = (event) => {
        const { name, value } = event.target;
        setForm((current) => ({ ...current, [name]: value }));
        setError("");
    };

    const handleSubmit = async (event) => {
        event.preventDefault();
        setError("");

        if (!form.indexNumber.trim()) {
            setError("Index number is required.");
            return;
        }

        if (!form.password) {
            setError("Password is required.");
            return;
        }

        setLoading(true);

        try {
            const { data } = await studentAuthApi.login({
                indexNumber: form.indexNumber.trim(),
                password: form.password,
            });

            if (
                !Array.isArray(data?.roles) ||
                !data.roles.includes("Student") ||
                !data.token
            ) {
                setError("This account is not a student account.");
                return;
            }

            localStorage.setItem("accessToken", data.token);
            localStorage.setItem(
                "studentUser",
                JSON.stringify({
                    fullName: data.fullName,
                    indexNumber: data.indexNumber,
                    email: data.email,
                    studentId: data.studentId,
                    roles: data.roles,
                    expiresAt: data.expiresAt,
                    mustChangePassword: data.mustChangePassword === true,
                })
            );

            navigate(
                data.mustChangePassword === true
                    ? "/student/change-password"
                    : "/student/dashboard",
                { replace: true }
            );
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to sign in. Please try again."
            );
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex min-h-20 max-w-7xl items-center px-5 sm:px-7 lg:px-8">
                    <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-600 text-white">
                            <GraduationCap className="h-5 w-5" />
                        </div>
                        <div>
                            <p className="font-bold text-slate-950">
                                School Management
                            </p>
                            <p className="text-xs text-slate-500">
                                Student Portal
                            </p>
                        </div>
                    </div>
                </div>
            </header>

            <main className="flex min-h-[calc(100vh-5rem)] items-center justify-center px-5 py-10 sm:px-7 lg:px-8">
                <div className="w-full max-w-md">
                    <div className="text-center">
                        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-blue-600 text-white shadow-lg shadow-blue-600/20">
                            <GraduationCap className="h-8 w-8" />
                        </div>
                        <h1 className="mt-6 text-3xl font-bold tracking-tight text-slate-950">
                            Student Login
                        </h1>
                        <p className="mt-2 text-sm leading-6 text-slate-500">
                            Sign in using your student index number and password.
                        </p>
                    </div>

                    <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-xl shadow-slate-200/50 sm:p-8">
                        {error && (
                            <div
                                role="alert"
                                className="mb-6 rounded-xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-medium text-red-700"
                            >
                                {error}
                            </div>
                        )}

                        <form onSubmit={handleSubmit}>
                            <label
                                htmlFor="student-index"
                                className="mb-2 block text-sm font-semibold text-slate-700"
                            >
                                Index Number
                            </label>
                            <div className="relative">
                                <UserRound className="absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                                <input
                                    id="student-index"
                                    type="text"
                                    name="indexNumber"
                                    value={form.indexNumber}
                                    onChange={handleChange}
                                    disabled={loading}
                                    required
                                    autoComplete="username"
                                    placeholder="Enter your index number"
                                    className="h-12 w-full rounded-xl border border-slate-200 bg-white pl-11 pr-4 text-sm text-slate-950 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                />
                            </div>

                            <label
                                htmlFor="student-password"
                                className="mb-2 mt-5 block text-sm font-semibold text-slate-700"
                            >
                                Password
                            </label>
                            <div className="relative">
                                <LockKeyhole className="absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                                <input
                                    id="student-password"
                                    type={showPassword ? "text" : "password"}
                                    name="password"
                                    value={form.password}
                                    onChange={handleChange}
                                    disabled={loading}
                                    required
                                    autoComplete="current-password"
                                    placeholder="Enter your password"
                                    className="h-12 w-full rounded-xl border border-slate-200 bg-white pl-11 pr-12 text-sm text-slate-950 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                />
                                <button
                                    type="button"
                                    onClick={() =>
                                        setShowPassword((current) => !current)
                                    }
                                    className="absolute right-3 top-1/2 flex h-8 w-8 -translate-y-1/2 cursor-pointer items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                                    aria-label={
                                        showPassword
                                            ? "Hide password"
                                            : "Show password"
                                    }
                                >
                                    {showPassword ? (
                                        <EyeOff className="h-4 w-4" />
                                    ) : (
                                        <Eye className="h-4 w-4" />
                                    )}
                                </button>
                            </div>

                            <div className="mt-3 text-right">
                                <Link
                                    to="/student/forgot-password"
                                    className="cursor-pointer text-sm font-semibold text-blue-600 hover:text-blue-700"
                                >
                                    Forgot password?
                                </Link>
                            </div>

                            <button
                                type="submit"
                                disabled={loading}
                                className="mt-7 inline-flex h-12 w-full cursor-pointer items-center justify-center gap-2 rounded-xl bg-blue-600 px-6 text-sm font-semibold text-white shadow-sm hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                {loading ? (
                                    "Signing in..."
                                ) : (
                                    <>
                                        <LogIn className="h-4 w-4" />
                                        Sign In
                                    </>
                                )}
                            </button>
                        </form>

                        <div className="mt-6 border-t border-slate-100 pt-6 text-center">
                            <p className="text-sm text-slate-500">
                                Haven&apos;t activated your account yet?
                            </p>
                            <Link
                                to="/student/register"
                                className="mt-2 inline-block cursor-pointer text-sm font-semibold text-blue-600 hover:text-blue-700"
                            >
                                Register Student Account
                            </Link>
                        </div>
                    </div>

                    <p className="mt-6 text-center text-xs text-slate-400">
                        Student access is available only for registered student accounts.
                    </p>
                </div>
            </main>
        </div>
    );
}