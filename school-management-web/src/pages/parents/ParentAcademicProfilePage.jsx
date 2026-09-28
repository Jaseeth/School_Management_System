import {
    ArrowLeft,
    Award,
    BookOpen,
    CalendarDays,
    GraduationCap,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

const displayDate = (value) =>
    value ? String(value).slice(0, 10) : "—";

export default function ParentAcademicProfilePage() {
    const { studentId } = useParams();
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        let active = true;

        setLoading(true);
        setData(null);
        setError("");

        parentPortalApi.getChildAcademicProfile(studentId)
            .then(({ data: response }) => {
                if (active) setData(response);
            })
            .catch((err) => {
                if (!active) return;

                setError(
                    err?.response?.status === 403
                        ? "This student is not linked to your parent account."
                        : err?.response?.data?.message ||
                        "Unable to load academic profile."
                );
            })
            .finally(() => {
                if (active) setLoading(false);
            });

        return () => {
            active = false;
        };
    }, [studentId]);

    const enrollment = data?.currentEnrollment;
    const currentSubjects = (data?.subjects ?? []).filter(
        (item) =>
            item.academicYearId === enrollment?.academicYearId
    );

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex max-w-6xl items-center gap-3 px-5 py-4">
                    <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-white">
                        <GraduationCap className="h-6 w-6" />
                    </span>
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

            <main className="mx-auto max-w-6xl px-5 py-9">
                <Link
                    to="/parent/dashboard"
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600 hover:text-blue-700"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to My Children
                </Link>

                <div className="mt-7 flex items-center gap-3 text-blue-600">
                    <BookOpen className="h-6 w-6" />
                    <span className="text-sm font-semibold">
                        Academic Profile
                    </span>
                </div>

                <h1 className="mt-2 text-3xl font-bold text-slate-950">
                    {data?.student?.fullName || "Academic Profile"}
                </h1>

                {data?.student && (
                    <p className="mt-2 text-sm text-slate-600">
                        Index Number: {data.student.indexNumber}
                    </p>
                )}

                {loading && (
                    <p role="status" className="mt-7 text-slate-600">
                        Loading academic profile...
                    </p>
                )}

                {error && (
                    <p
                        role="alert"
                        className="mt-7 rounded-xl border border-red-100 bg-red-50 p-4 text-red-700"
                    >
                        {error}
                    </p>
                )}

                {!loading && !error && data && (
                    <>
                        <section className="mt-7 grid gap-4 md:grid-cols-2">
                            <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                                <h2 className="text-lg font-bold text-slate-950">
                                    Student Information
                                </h2>

                                <dl className="mt-4 space-y-3 text-sm">
                                    <Info
                                        label="Date of Birth"
                                        value={displayDate(
                                            data.student?.dateOfBirth
                                        )}
                                    />
                                    <Info
                                        label="Status"
                                        value={
                                            data.student?.isCompleted
                                                ? "Graduated"
                                                : data.student?.isActive
                                                    ? "Active"
                                                    : "Inactive"
                                        }
                                    />
                                    <Info
                                        label="Your Relationship"
                                        value={
                                            data.relationship?.relationship
                                        }
                                    />
                                    {data.student?.isCompleted && (
                                        <Info
                                            label="Completion Date"
                                            value={displayDate(
                                                data.student.completionDate
                                            )}
                                        />
                                    )}
                                </dl>
                            </div>

                            <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                                <h2 className="text-lg font-bold text-slate-950">
                                    Current Enrollment
                                </h2>

                                {enrollment ? (
                                    <dl className="mt-4 space-y-3 text-sm">
                                        <Info
                                            label="Academic Year"
                                            value={
                                                enrollment.academicYearName
                                            }
                                        />
                                        <Info
                                            label="Section"
                                            value={
                                                enrollment.sectionName
                                            }
                                        />
                                        <Info
                                            label="Grade"
                                            value={
                                                enrollment.gradeName
                                            }
                                        />
                                        <Info
                                            label="Class"
                                            value={
                                                enrollment.className
                                            }
                                        />
                                        <Info
                                            label="Enrollment Date"
                                            value={displayDate(
                                                enrollment.enrollmentDate
                                            )}
                                        />
                                    </dl>
                                ) : (
                                    <p className="mt-4 text-sm text-slate-600">
                                        No current enrollment available.
                                    </p>
                                )}
                            </div>
                        </section>

                        <section className="mt-5 grid gap-4 sm:grid-cols-3">
                            {[
                                [
                                    "Attendance Days",
                                    data.attendanceSummary?.totalDays ?? 0,
                                ],
                                [
                                    "Attendance Rate",
                                    `${data.attendanceSummary
                                        ?.attendancePercentage ?? 0
                                    }%`,
                                ],
                                [
                                    "Published Marks",
                                    data.totalPublishedResults ?? 0,
                                ],
                            ].map(([label, value]) => (
                                <div
                                    key={label}
                                    className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
                                >
                                    <p className="text-sm text-slate-500">
                                        {label}
                                    </p>
                                    <p className="mt-2 text-2xl font-bold text-slate-950">
                                        {value}
                                    </p>
                                </div>
                            ))}
                        </section>

                        <div className="mt-5 flex flex-wrap gap-3">
                            <Link
                                to={`/parent/children/${studentId}/attendance`}
                                className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-blue-200 bg-white px-4 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                            >
                                <CalendarDays className="h-4 w-4" />
                                View Attendance
                            </Link>

                            <Link
                                to={`/parent/children/${studentId}/results`}
                                className="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-blue-200 bg-white px-4 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                            >
                                <Award className="h-4 w-4" />
                                View Results
                            </Link>
                        </div>

                        <section className="mt-9">
                            <h2 className="text-xl font-bold text-slate-950">
                                Current Subjects ({currentSubjects.length})
                            </h2>

                            {currentSubjects.length ? (
                                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                                    {currentSubjects.map((subject) => (
                                        <div
                                            key={subject.id}
                                            className="rounded-xl border border-slate-200 bg-white p-4 text-sm font-semibold text-slate-800"
                                        >
                                            {subject.subjectName}
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <p className="mt-4 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                                    No subjects enrolled for the current
                                    academic year.
                                </p>
                            )}
                        </section>

                        <section className="mt-9">
                            <h2 className="text-xl font-bold text-slate-950">
                                Enrollment History
                            </h2>

                            {data.enrollmentHistory?.length ? (
                                <div className="mt-4 grid gap-3">
                                    {data.enrollmentHistory.map((item) => (
                                        <div
                                            key={item.id}
                                            className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
                                        >
                                            <div className="flex flex-wrap items-center justify-between gap-2">
                                                <p className="font-semibold text-slate-900">
                                                    {item.academicYearName}
                                                </p>

                                                {item.isCurrent && (
                                                    <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
                                                        Current
                                                    </span>
                                                )}
                                            </div>

                                            <p className="mt-2 text-sm text-slate-600">
                                                {item.sectionName}
                                                {" · Grade "}
                                                {item.gradeName}
                                                {" · Class "}
                                                {item.className}
                                            </p>
                                            <p className="mt-1 text-sm text-slate-500">
                                                Enrolled:{" "}
                                                {displayDate(
                                                    item.enrollmentDate
                                                )}
                                            </p>
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <p className="mt-4 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                                    No enrollment history available.
                                </p>
                            )}
                        </section>

                        <section className="mt-9">
                            <h2 className="text-xl font-bold text-slate-950">
                                Promotion History
                            </h2>

                            {data.promotionHistory?.length ? (
                                <div className="mt-4 grid gap-3">
                                    {data.promotionHistory.map((item) => (
                                        <div
                                            key={item.id}
                                            className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
                                        >
                                            <p className="font-semibold text-slate-900">
                                                {item.action}
                                                {" · "}
                                                {item.fromAcademicYear}
                                                {" → "}
                                                {item.toAcademicYear}
                                            </p>

                                            <p className="mt-2 text-sm text-slate-600">
                                                Grade {item.fromGrade}
                                                {" · Class "}
                                                {item.fromClass}
                                                {" → "}
                                                {item.toGrade
                                                    ? `Grade ${item.toGrade} · Class ${item.toClass}`
                                                    : "No next class"}
                                            </p>

                                            {item.reason && (
                                                <p className="mt-2 text-sm text-slate-600">
                                                    Reason: {item.reason}
                                                </p>
                                            )}

                                            <p className="mt-1 text-sm text-slate-500">
                                                Processed:{" "}
                                                {displayDate(
                                                    item.processedAt
                                                )}
                                            </p>
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <p className="mt-4 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                                    No promotion history available.
                                </p>
                            )}
                        </section>
                    </>
                )}
            </main>
        </div>
    );
}

function Info({ label, value }) {
    return (
        <div className="flex justify-between gap-4">
            <dt className="text-slate-500">{label}</dt>
            <dd className="text-right font-semibold text-slate-800">
                {value ?? "—"}
            </dd>
        </div>
    );
}