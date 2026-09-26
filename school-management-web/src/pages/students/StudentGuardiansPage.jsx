import {
    ArrowLeft,
    GraduationCap,
    Mail,
    Phone,
    UsersRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { studentPortalApi } from "../../api/studentPortalApi";

export default function StudentGuardiansPage() {
    const navigate = useNavigate();

    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [retry, setRetry] = useState(0);

    useEffect(() => {
        let active = true;

        studentPortalApi
            .getGuardians()
            .then((response) => {
                if (active) {
                    setData(response.data);
                    setError("");
                }
            })
            .catch((err) => {
                if (active) {
                    setData(null);
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load your guardians."
                    );
                }
            })
            .finally(() => {
                if (active) {
                    setLoading(false);
                }
            });

        return () => {
            active = false;
        };
    }, [retry]);

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex min-h-20 max-w-7xl items-center gap-3 px-5 sm:px-7 lg:px-8">
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
            </header>

            <main className="mx-auto w-full max-w-7xl px-5 py-8 sm:px-7 lg:px-8 lg:py-10">
                <button
                    type="button"
                    onClick={() =>
                        navigate("/student/dashboard")
                    }
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>

                <div className="mt-6">
                    <p className="text-sm font-semibold text-blue-600">
                        Student Information
                    </p>

                    <h1 className="mt-2 text-3xl font-bold text-slate-950">
                        My Guardians
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        View parents and guardians linked to
                        your student record.
                    </p>
                </div>

                {loading && (
                    <div
                        className="mt-10 flex justify-center"
                        role="status"
                        aria-label="Loading guardians"
                    >
                        <div className="h-8 w-8 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />
                    </div>
                )}

                {!loading && error && (
                    <div
                        className="mt-8 rounded-xl border border-red-100 bg-red-50 p-5 text-sm text-red-700"
                        role="alert"
                    >
                        <p>{error}</p>

                        <button
                            type="button"
                            onClick={() => {
                                setLoading(true);
                                setRetry((value) => value + 1);
                            }}
                            className="mt-3 cursor-pointer font-semibold underline"
                        >
                            Try Again
                        </button>
                    </div>
                )}

                {!loading && !error && data && (
                    <>
                        <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                            <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                                Student
                            </p>

                            <p className="mt-2 font-semibold text-slate-950">
                                {data.student?.fullName}
                                {" · "}
                                {data.student?.indexNumber}
                            </p>

                            <p className="mt-1 text-sm text-slate-500">
                                {data.count ?? 0} linked guardian
                                {data.count === 1 ? "" : "s"}
                            </p>
                        </div>

                        {data.guardians?.length ? (
                            <div className="mt-6 grid gap-4 md:grid-cols-2">
                                {data.guardians.map(
                                    (guardian) => (
                                        <section
                                            key={guardian.id}
                                            className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
                                        >
                                            <div className="flex flex-wrap items-start justify-between gap-3">
                                                <div>
                                                    <h2 className="text-lg font-semibold text-slate-950">
                                                        {guardian.fullName}
                                                    </h2>

                                                    <p className="mt-1 text-sm text-slate-500">
                                                        {guardian.relationship ||
                                                            "Guardian"}
                                                    </p>
                                                </div>

                                                <div className="flex flex-wrap gap-2">
                                                    {guardian.isPrimaryGuardian && (
                                                        <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
                                                            Primary
                                                        </span>
                                                    )}

                                                    {guardian.isEmergencyContact && (
                                                        <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                                                            Emergency Contact
                                                        </span>
                                                    )}
                                                </div>
                                            </div>

                                            <div className="mt-5 space-y-3 text-sm text-slate-700">
                                                <div className="flex items-center gap-2">
                                                    <Phone className="h-4 w-4 shrink-0 text-blue-600" />
                                                    <span>
                                                        {guardian.phoneNumber ||
                                                            "No phone number available"}
                                                    </span>
                                                </div>

                                                <div className="flex items-center gap-2">
                                                    <Mail className="h-4 w-4 shrink-0 text-blue-600" />
                                                    <span className="break-all">
                                                        {guardian.email ||
                                                            "No email available"}
                                                    </span>
                                                </div>
                                            </div>
                                        </section>
                                    )
                                )}
                            </div>
                        ) : (
                            <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">
                                <UsersRound className="mx-auto h-8 w-8 text-slate-400" />

                                <h2 className="mt-4 font-semibold text-slate-950">
                                    No guardians linked
                                </h2>

                                <p className="mt-2 text-sm text-slate-500">
                                    No active parent or guardian is
                                    linked to your student record.
                                </p>
                            </div>
                        )}
                    </>
                )}
            </main>
        </div>
    );
}