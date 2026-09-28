import { ArrowLeft, CalendarDays, GraduationCap } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

export default function ParentAttendancePage() {
    const { studentId } = useParams();
    const [profile, setProfile] = useState(null);
    const [yearId, setYearId] = useState("");
    const [attendance, setAttendance] = useState(null);
    const [loadingProfile, setLoadingProfile] = useState(true);
    const [loadingAttendance, setLoadingAttendance] = useState(false);
    const [error, setError] = useState("");

    useEffect(() => {
        let active = true;

        setLoadingProfile(true);
        setProfile(null);
        setYearId("");
        setAttendance(null);
        setError("");

        parentPortalApi.getChildAcademicProfile(studentId)
            .then(({ data }) => {
                if (!active) return;

                setProfile(data);
                const years = data.enrollmentHistory ?? [];
                setYearId(String(
                    data.currentEnrollment?.academicYearId ??
                    years[0]?.academicYearId ??
                    ""
                ));
            })
            .catch((err) => {
                if (!active) return;

                setError(
                    err?.response?.status === 403
                        ? "This student is not linked to your parent account."
                        : err?.response?.data?.message ||
                        "Unable to load this student's academic years."
                );
            })
            .finally(() => {
                if (active) setLoadingProfile(false);
            });

        return () => {
            active = false;
        };
    }, [studentId]);

    useEffect(() => {
        if (!yearId || !profile) return;

        let active = true;
        setLoadingAttendance(true);
        setAttendance(null);
        setError("");

        parentPortalApi.getChildAttendance(studentId, Number(yearId))
            .then(({ data }) => {
                if (active) setAttendance(data);
            })
            .catch((err) => {
                if (!active) return;

                setError(
                    err?.response?.status === 403
                        ? "This student is not linked to your parent account."
                        : err?.response?.data?.message ||
                        "Unable to load attendance."
                );
            })
            .finally(() => {
                if (active) setLoadingAttendance(false);
            });

        return () => {
            active = false;
        };
    }, [studentId, yearId, profile]);

    const years = Array.from(
        new Map(
            (profile?.enrollmentHistory ?? []).map((item) => [
                item.academicYearId,
                {
                    id: item.academicYearId,
                    name: item.academicYearName,
                },
            ])
        ).values()
    );

    if (
        profile?.currentEnrollment &&
        !years.some(
            (year) => year.id === profile.currentEnrollment.academicYearId
        )
    ) {
        years.unshift({
            id: profile.currentEnrollment.academicYearId,
            name: profile.currentEnrollment.academicYearName,
        });
    }

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
                    <CalendarDays className="h-6 w-6" />
                    <span className="text-sm font-semibold">
                        Child Attendance
                    </span>
                </div>

                <h1 className="mt-2 text-3xl font-bold text-slate-950">
                    {profile?.student?.fullName || "Attendance"}
                </h1>

                {profile?.student && (
                    <p className="mt-2 text-sm text-slate-600">
                        Index Number: {profile.student.indexNumber}
                    </p>
                )}

                {loadingProfile && (
                    <p role="status" className="mt-7 text-slate-600">
                        Loading academic years...
                    </p>
                )}

                {!loadingProfile && years.length > 0 && (
                    <label className="mt-7 block max-w-xs text-sm font-semibold text-slate-700">
                        Academic Year
                        <select
                            value={yearId}
                            onChange={(event) => setYearId(event.target.value)}
                            className="mt-2 w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-3 font-normal text-slate-900 focus:border-blue-500 focus:outline-none"
                        >
                            {years.map((year) => (
                                <option key={year.id} value={year.id}>
                                    {year.name}
                                </option>
                            ))}
                        </select>
                    </label>
                )}

                {!loadingProfile && !error && years.length === 0 && (
                    <p className="mt-7 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                        No academic enrollment found for this student.
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

                {loadingAttendance && (
                    <p role="status" className="mt-7 text-slate-600">
                        Loading attendance...
                    </p>
                )}

                {!loadingAttendance && !error && attendance && (
                    <>
                        <div className="mt-7 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                            {[
                                ["Total Days", attendance.totalDays],
                                ["Present", attendance.presentDays],
                                ["Absent", attendance.absentDays],
                                ["Attendance", `${attendance.attendancePercentage}%`],
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
                        </div>

                        <h2 className="mt-9 text-xl font-bold text-slate-950">
                            Attendance Records · {attendance.academicYearName}
                        </h2>

                        {attendance.attendance?.length ? (
                            <div className="mt-4 overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
                                <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                                    <thead className="bg-slate-50 text-slate-600">
                                        <tr>
                                            <th className="px-5 py-3 font-semibold">
                                                Date
                                            </th>
                                            <th className="px-5 py-3 font-semibold">
                                                Status
                                            </th>
                                            <th className="px-5 py-3 font-semibold">
                                                Remarks
                                            </th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-slate-100">
                                        {attendance.attendance.map((record) => (
                                            <tr key={record.attendanceId}>
                                                <td className="px-5 py-4 text-slate-700">
                                                    {record.attendanceDate?.slice(0, 10)}
                                                </td>
                                                <td className="px-5 py-4 font-semibold text-slate-800">
                                                    {record.status}
                                                </td>
                                                <td className="px-5 py-4 text-slate-600">
                                                    {record.remarks || "—"}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        ) : (
                            <p className="mt-4 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                                No attendance records for this academic year.
                            </p>
                        )}
                    </>
                )}
            </main>
        </div>
    );
}