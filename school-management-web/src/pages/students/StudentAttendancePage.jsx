import {
    ArrowLeft,
    CalendarCheck2,
    CalendarDays,
    CheckCircle2,
    GraduationCap,
    XCircle,
} from "lucide-react";

import {
    useEffect,
    useState,
} from "react";

import {
    useNavigate,
} from "react-router-dom";

import {
    studentPortalApi,
} from "../../api/studentPortalApi";


export default function StudentAttendancePage() {

    const navigate =
        useNavigate();


    const [
        data,
        setData,
    ] = useState(null);


    const [
        loading,
        setLoading,
    ] = useState(true);


    const [
        error,
        setError,
    ] = useState("");


    useEffect(() => {

        loadAttendance();

    }, []);


    const loadAttendance =
        async () => {

            try {

                setLoading(true);
                setError("");

                const response =
                    await studentPortalApi
                        .getAttendance();

                setData(
                    response.data
                );

            }
            catch (err) {

                console.error(
                    "Unable to load attendance:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load attendance."
                );

            }
            finally {

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


            <main className="mx-auto w-full max-w-7xl px-5 py-8 sm:px-7 lg:px-8 lg:py-10">

                <button
                    type="button"
                    onClick={() =>
                        navigate(
                            "/student/dashboard"
                        )
                    }
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>


                <div className="mt-6">

                    <p className="text-sm font-semibold text-blue-600">
                        Academic
                    </p>

                    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                        My Attendance
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        View your attendance summary and daily attendance records.
                    </p>

                </div>


                {loading && (

                    <div className="mt-10 flex justify-center">

                        <div className="h-8 w-8 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />

                    </div>

                )}


                {error && (

                    <div className="mt-8 rounded-xl border border-red-100 bg-red-50 p-4 text-sm text-red-700">
                        {error}
                    </div>

                )}


                {!loading &&
                    !error &&
                    data && (

                        <>

                            <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">

                                <SummaryCard
                                    icon={
                                        <CalendarDays className="h-5 w-5" />
                                    }
                                    label="Total Days"
                                    value={
                                        data.totalDays ?? 0
                                    }
                                />

                                <SummaryCard
                                    icon={
                                        <CheckCircle2 className="h-5 w-5" />
                                    }
                                    label="Present"
                                    value={
                                        data.presentDays ?? 0
                                    }
                                />

                                <SummaryCard
                                    icon={
                                        <XCircle className="h-5 w-5" />
                                    }
                                    label="Absent"
                                    value={
                                        data.absentDays ?? 0
                                    }
                                />

                                <SummaryCard
                                    icon={
                                        <CalendarCheck2 className="h-5 w-5" />
                                    }
                                    label="Attendance"
                                    value={`${data.attendancePercentage ?? 0}%`}
                                />

                            </div>


                            <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">

                                <div className="border-b border-slate-100 px-6 py-5">

                                    <h2 className="font-semibold text-slate-950">
                                        Attendance Records
                                    </h2>

                                    <p className="mt-1 text-xs text-slate-500">
                                        {
                                            data
                                                ?.currentEnrollment
                                                ?.academicYear
                                                ? `Academic Year ${data.currentEnrollment.academicYear}`
                                                : "Current academic year"
                                        }
                                    </p>

                                </div>


                                {data.attendance?.length > 0 ? (

                                    <div className="overflow-x-auto">

                                        <table className="w-full">

                                            <thead className="bg-slate-50">

                                                <tr>

                                                    <TableHead>
                                                        Date
                                                    </TableHead>

                                                    <TableHead>
                                                        Status
                                                    </TableHead>

                                                    <TableHead>
                                                        Remarks
                                                    </TableHead>

                                                </tr>

                                            </thead>


                                            <tbody className="divide-y divide-slate-100">

                                                {data.attendance.map(
                                                    (
                                                        record
                                                    ) => (

                                                        <tr
                                                            key={
                                                                record
                                                                    .attendanceId
                                                            }
                                                            className="transition hover:bg-slate-50"
                                                        >

                                                            <TableCell>
                                                                {
                                                                    formatDate(
                                                                        record
                                                                            .attendanceDate
                                                                    )
                                                                }
                                                            </TableCell>


                                                            <TableCell>

                                                                <AttendanceStatus
                                                                    status={
                                                                        record
                                                                            .status
                                                                    }
                                                                />

                                                            </TableCell>


                                                            <TableCell>
                                                                {
                                                                    record
                                                                        .remarks ||
                                                                    "—"
                                                                }
                                                            </TableCell>

                                                        </tr>

                                                    )
                                                )}

                                            </tbody>

                                        </table>

                                    </div>

                                ) : (

                                    <div className="p-10 text-center">

                                        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-slate-100 text-slate-400">
                                            <CalendarCheck2 className="h-6 w-6" />
                                        </div>

                                        <p className="mt-4 font-semibold text-slate-900">
                                            No attendance records
                                        </p>

                                        <p className="mt-1 text-sm text-slate-500">
                                            No attendance has been recorded for the current academic year.
                                        </p>

                                    </div>

                                )}

                            </section>

                        </>

                    )}

            </main>

        </div>

    );

}


function SummaryCard({
    icon,
    label,
    value,
}) {

    return (

        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">

            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                {icon}
            </div>

            <p className="mt-4 text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>

            <p className="mt-1 text-xl font-bold text-slate-950">
                {value}
            </p>

        </div>

    );

}


function AttendanceStatus({
    status,
}) {

    const normalized =
        status?.toLowerCase();


    if (
        normalized ===
        "present"
    ) {

        return (

            <span className="inline-flex rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                Present
            </span>

        );

    }


    if (
        normalized ===
        "absent"
    ) {

        return (

            <span className="inline-flex rounded-full bg-red-50 px-3 py-1 text-xs font-semibold text-red-700">
                Absent
            </span>

        );

    }


    return (

        <span className="inline-flex rounded-full bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700">
            {status || "Unknown"}
        </span>

    );

}


function TableHead({
    children,
}) {

    return (

        <th className="px-6 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            {children}
        </th>

    );

}


function TableCell({
    children,
}) {

    return (

        <td className="px-6 py-4 text-sm text-slate-600">
            {children}
        </td>

    );

}


function formatDate(
    value
) {

    if (!value) {
        return "—";
    }

    const date =
        new Date(value);

    if (
        Number.isNaN(
            date.getTime()
        )
    ) {
        return value;
    }

    return date.toLocaleDateString(
        undefined,
        {
            year: "numeric",
            month: "short",
            day: "numeric",
        }
    );

}