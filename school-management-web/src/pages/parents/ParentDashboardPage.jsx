import {
    Award,
    Bell,
    BookOpen,
    CalendarDays,
    GraduationCap,
    LockKeyhole,
    LogOut,
    UsersRound,
    UserRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

export default function ParentDashboardPage() {
    const navigate = useNavigate();
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        let active = true;

        parentPortalApi.getChildren()
            .then(({ data: result }) => {
                if (active) setData(result);
            })
            .catch((err) => {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load your children."
                    );
                }
            })
            .finally(() => {
                if (active) setLoading(false);
            });

        return () => {
            active = false;
        };
    }, []);

    function logout() {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("parentUser");
        navigate("/parent/login", { replace: true });
    }

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-5 py-4">
                    <div className="flex items-center gap-3">
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

                    <button
                        type="button"
                        onClick={logout}
                        className="flex cursor-pointer items-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                    >
                        <LogOut className="h-4 w-4" />
                        Log Out
                    </button>
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-5 py-9">
                <p className="text-sm font-semibold text-blue-600">
                    Parent Dashboard
                </p>

                <h1 className="mt-2 break-words text-2xl font-bold text-slate-950 sm:text-3xl">
                    Welcome, {data?.parent?.fullName || "Parent"}
                </h1>

                <p className="mt-2 text-sm text-slate-500">
                    Children linked to your parent account.
                </p>

                <nav
                    aria-label="Parent account"
                    className="mt-5 flex flex-wrap gap-3"
                >
                    <Link
                        to="/parent/notifications"
                        className="inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-xl border border-blue-200 bg-white px-4 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                    >
                        <Bell className="h-4 w-4" />
                        Notifications
                    </Link>

                    <Link
                        to="/parent/change-password"
                        className="inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                    >
                        <LockKeyhole className="h-4 w-4" />
                        Change Password
                    </Link>

                    <Link
                        to="/parent/profile"
                        className="inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-xl border border-blue-200 bg-white px-4 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                    >
                        <UserRound className="h-4 w-4" />
                        My Profile
                    </Link>
                </nav>

                {loading && (
                    <p role="status" className="mt-8 text-slate-600">
                        Loading children...
                    </p>
                )}

                {error && (
                    <p
                        role="alert"
                        className="mt-8 rounded-xl bg-red-50 p-4 text-red-700"
                    >
                        {error}
                    </p>
                )}

                {!loading && !error && (
                    <section className="mt-8">
                        <div className="flex items-center gap-2">
                            <UsersRound className="h-5 w-5 text-blue-600" />
                            <h2 className="text-xl font-bold text-slate-950">
                                My Children ({data?.totalChildren ?? 0})
                            </h2>
                        </div>

                        {data?.children?.length ? (
                            <div className="mt-5 grid gap-4 lg:grid-cols-2">
                                {data.children.map((entry) => (
                                    <article
                                        key={entry.relationshipId}
                                        className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"
                                    >
                                        <h3 className="break-words text-lg font-bold text-slate-950">
                                            {entry.student?.fullName}
                                        </h3>

                                        <p className="mt-2 break-words text-sm text-slate-600">
                                            Index Number:{" "}
                                            {entry.student?.indexNumber}
                                        </p>

                                        <p className="mt-1 text-sm text-slate-600">
                                            {
                                                entry.student?.schoolClass
                                                    ?.sectionName
                                            }
                                            {" · Grade "}
                                            {
                                                entry.student?.schoolClass
                                                    ?.gradeName
                                            }
                                            {" · Class "}
                                            {
                                                entry.student?.schoolClass
                                                    ?.className
                                            }
                                        </p>

                                        <p className="mt-1 text-sm text-slate-600">
                                            Relationship:{" "}
                                            {entry.relationship}
                                        </p>

                                        <div className="mt-5 grid grid-cols-1 gap-2 sm:grid-cols-2">
                                            <Link
                                                to={`/parent/children/${entry.student.id}/academic-profile`}
                                                className="inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-xl bg-blue-600 px-3 py-2 text-sm font-semibold text-white hover:bg-blue-700 sm:col-span-2"
                                            >
                                                <BookOpen className="h-4 w-4 shrink-0" />
                                                Academic Profile
                                            </Link>

                                            <Link
                                                to={`/parent/children/${entry.student.id}/attendance`}
                                                className="inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-xl border border-blue-200 bg-white px-3 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                                            >
                                                <CalendarDays className="h-4 w-4 shrink-0" />
                                                Attendance
                                            </Link>

                                            <Link
                                                to={`/parent/children/${entry.student.id}/results`}
                                                className="inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-xl border border-blue-200 bg-white px-3 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                                            >
                                                <Award className="h-4 w-4 shrink-0" />
                                                Results
                                            </Link>
                                        </div>
                                    </article>
                                ))}
                            </div>
                        ) : (
                            <p className="mt-5 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                                No children are linked to this account yet.
                            </p>
                        )}
                    </section>
                )}
            </main>
        </div>
    );
}