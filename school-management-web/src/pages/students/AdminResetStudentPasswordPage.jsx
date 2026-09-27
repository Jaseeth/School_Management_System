import { ArrowLeft, Check, Copy, KeyRound } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { studentsApi } from "../../api/studentsApi";
import { useAuth } from "../../context/AuthContext";

export default function AdminResetStudentPasswordPage() {
    const { id } = useParams();
    const { hasRole } = useAuth();

    const [student, setStudent] = useState(null);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState(false);
    const [verified, setVerified] = useState(false);
    const [result, setResult] = useState(null);
    const [copied, setCopied] = useState(false);
    const [error, setError] = useState("");

    useEffect(() => {
        if (!hasRole("Admin")) return;

        let active = true;

        studentsApi.getStudentDetails(id)
            .then(({ data }) => {
                if (active) setStudent(data.student);
            })
            .catch((err) => {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load student."
                    );
                }
            })
            .finally(() => {
                if (active) setLoading(false);
            });

        return () => {
            active = false;
        };
    }, [id, hasRole]);

    if (!hasRole("Admin")) {
        return (
            <p className="p-6 text-red-700">
                Only an administrator can reset a student password.
            </p>
        );
    }

    async function resetPassword() {
        if (!verified || !student?.indexNumber || busy) return;

        setError("");
        setBusy(true);

        try {
            const response = await studentsApi.resetStudentPassword(
                student.indexNumber
            );
            setResult(response.data);
        } catch (err) {
            setError(
                err?.response?.data?.errors?.join(" ") ||
                err?.response?.data?.message ||
                "Unable to reset student password."
            );
        } finally {
            setBusy(false);
        }
    }

    async function copyPassword() {
        try {
            await navigator.clipboard.writeText(result.temporaryPassword);
            setCopied(true);
        } catch {
            setError(
                "Unable to copy. Select the password and copy it manually."
            );
        }
    }

    return (
        <div className="mx-auto w-full max-w-2xl">
            <Link
                to={`/students/${id}`}
                className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600 hover:text-blue-600"
            >
                <ArrowLeft className="h-4 w-4" />
                Back to Student
            </Link>

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
                <div className="mb-5 flex h-12 w-12 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                    <KeyRound className="h-6 w-6" />
                </div>

                <h1 className="text-2xl font-bold text-slate-950">
                    Reset Student Password
                </h1>

                {loading && (
                    <p className="mt-5 text-sm text-slate-600">
                        Loading student...
                    </p>
                )}

                {error && (
                    <p
                        role="alert"
                        className="mt-5 rounded-xl bg-red-50 p-4 text-sm text-red-700"
                    >
                        {error}
                    </p>
                )}

                {student && !result && (
                    <>
                        <p className="mt-5 text-sm text-slate-700">
                            Student: <strong>{student.fullName}</strong>
                        </p>
                        <p className="mt-2 text-sm text-slate-700">
                            Index Number: <strong>{student.indexNumber}</strong>
                        </p>

                        <p className="mt-5 rounded-xl bg-amber-50 p-4 text-sm text-amber-800">
                            Verify the student’s identity before resetting the
                            password. The current password will stop working
                            immediately.
                        </p>

                        <label className="mt-5 flex cursor-pointer items-start gap-3 text-sm text-slate-700">
                            <input
                                type="checkbox"
                                checked={verified}
                                onChange={(event) =>
                                    setVerified(event.target.checked)
                                }
                                className="mt-1 cursor-pointer"
                            />
                            I verified this student’s identity and will give
                            the temporary password securely to the student.
                        </label>

                        <button
                            type="button"
                            onClick={resetPassword}
                            disabled={!verified || busy}
                            className="mt-6 w-full cursor-pointer rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {busy ? "Resetting..." : "Reset Password"}
                        </button>
                    </>
                )}

                {result && (
                    <div className="mt-6">
                        <p
                            role="status"
                            className="rounded-xl bg-emerald-50 p-4 text-sm text-emerald-700"
                        >
                            {result.message}
                        </p>

                        <p className="mt-5 text-sm font-semibold text-slate-700">
                            Temporary password for {result.student?.fullName}
                        </p>

                        <div className="mt-2 flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 p-4">
                            <code className="select-all break-all text-lg font-bold text-slate-950">
                                {result.temporaryPassword}
                            </code>
                            <button
                                type="button"
                                onClick={copyPassword}
                                className="cursor-pointer rounded-lg p-2 text-blue-600 hover:bg-blue-50"
                                aria-label="Copy temporary password"
                            >
                                {copied
                                    ? <Check className="h-5 w-5" />
                                    : <Copy className="h-5 w-5" />}
                            </button>
                        </div>

                        <p className="mt-4 text-sm text-slate-600">
                            This password is shown once. The student must
                            change it after signing in.
                        </p>

                        <Link
                            to={`/students/${id}`}
                            className="mt-6 block cursor-pointer rounded-xl bg-slate-900 px-5 py-3 text-center text-sm font-semibold text-white hover:bg-slate-800"
                        >
                            Done
                        </Link>
                    </div>
                )}
            </div>
        </div>
    );
}