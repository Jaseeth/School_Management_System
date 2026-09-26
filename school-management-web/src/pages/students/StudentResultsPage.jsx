import {
    ArrowLeft,
    Award,
    BookOpen,
    CalendarDays,
    FileText,
    GraduationCap,
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


export default function StudentResultsPage() {

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

        loadResults();

    }, []);


    const loadResults =
        async () => {

            try {

                setLoading(true);
                setError("");

                const response =
                    await studentPortalApi
                        .getResults();

                setData(
                    response.data
                );

            }
            catch (err) {

                console.error(
                    "Unable to load results:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load your results."
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
                        My Results
                    </h1>

                    <p className="mt-2 text-sm text-slate-500">
                        View your published examination results and performance.
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

                            <div className="mt-8 grid gap-4 sm:grid-cols-3">

                                <InfoCard
                                    icon={
                                        <GraduationCap className="h-5 w-5" />
                                    }
                                    label="Student"
                                    value={
                                        data
                                            ?.student
                                            ?.fullName ||
                                        "—"
                                    }
                                />

                                <InfoCard
                                    icon={
                                        <BookOpen className="h-5 w-5" />
                                    }
                                    label="Index Number"
                                    value={
                                        data
                                            ?.student
                                            ?.indexNumber ||
                                        "—"
                                    }
                                />

                                <InfoCard
                                    icon={
                                        <FileText className="h-5 w-5" />
                                    }
                                    label="Published Exams"
                                    value={
                                        data
                                            ?.examCount ??
                                        data
                                            ?.results
                                            ?.length ??
                                        0
                                    }
                                />

                            </div>


                            {data.results?.length > 0 ? (

                                <div className="mt-6 space-y-6">

                                    {data.results.map(
                                        (
                                            result
                                        ) => (

                                            <ExamCard
                                                key={
                                                    result
                                                        .exam
                                                        .id
                                                }
                                                result={
                                                    result
                                                }
                                            />

                                        )
                                    )}

                                </div>

                            ) : (

                                <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">

                                    <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-xl bg-slate-100 text-slate-400">
                                        <Award className="h-7 w-7" />
                                    </div>

                                    <h2 className="mt-4 font-semibold text-slate-950">
                                        No published results
                                    </h2>

                                    <p className="mt-2 text-sm text-slate-500">
                                        No examination results have been published for your account yet.
                                    </p>

                                </div>

                            )}

                        </>

                    )}

            </main>

        </div>

    );

}


function ExamCard({
    result,
}) {

    return (

        <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">

            <div className="border-b border-slate-100 p-6">

                <div className="flex flex-col justify-between gap-5 md:flex-row md:items-center">

                    <div>

                        <div className="flex flex-wrap items-center gap-2">

                            <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
                                {
                                    result
                                        ?.academicYear
                                        ?.name
                                }
                            </span>

                            <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-600">
                                {
                                    result
                                        ?.term
                                        ?.name
                                }
                            </span>

                        </div>


                        <h2 className="mt-3 text-xl font-bold text-slate-950">
                            {
                                result
                                    ?.exam
                                    ?.name
                            }
                        </h2>


                        <div className="mt-2 flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-slate-500">

                            <span className="inline-flex items-center gap-2">
                                <CalendarDays className="h-4 w-4" />

                                {
                                    formatDate(
                                        result
                                            ?.exam
                                            ?.examDate
                                    )
                                }
                            </span>

                            <span>
                                Grade{" "}
                                {
                                    result
                                        ?.schoolClass
                                        ?.grade
                                }
                                {" • "}
                                Class{" "}
                                {
                                    result
                                        ?.schoolClass
                                        ?.name
                                }
                            </span>

                        </div>

                    </div>


                    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 md:min-w-[480px]">

                        <MiniStat
                            label="Subjects"
                            value={
                                result
                                    ?.summary
                                    ?.subjectCount ??
                                0
                            }
                        />

                        <MiniStat
                            label="Marks"
                            value={`${result?.summary?.totalMarks ?? 0}/${result?.summary?.maximumTotal ?? 0}`}
                        />

                        <MiniStat
                            label="Average"
                            value={
                                result
                                    ?.summary
                                    ?.average ??
                                0
                            }
                        />

                        <MiniStat
                            label="Percentage"
                            value={`${result?.summary?.percentage ?? 0}%`}
                        />

                    </div>

                </div>

            </div>


            <div className="overflow-x-auto">

                <table className="w-full">

                    <thead className="bg-slate-50">

                        <tr>

                            <TableHead>
                                Subject
                            </TableHead>

                            <TableHead>
                                Marks Obtained
                            </TableHead>

                            <TableHead>
                                Maximum Marks
                            </TableHead>

                            <TableHead>
                                Percentage
                            </TableHead>

                        </tr>

                    </thead>


                    <tbody className="divide-y divide-slate-100">

                        {result.subjects?.map(
                            (
                                subject
                            ) => (

                                <tr
                                    key={
                                        subject
                                            .markId
                                    }
                                    className="transition hover:bg-slate-50"
                                >

                                    <TableCell>

                                        <div className="flex items-center gap-3">

                                            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-50 text-blue-600">
                                                <BookOpen className="h-4 w-4" />
                                            </div>

                                            <span className="font-semibold text-slate-900">
                                                {
                                                    subject
                                                        ?.subject
                                                        ?.name
                                                }
                                            </span>

                                        </div>

                                    </TableCell>


                                    <TableCell>
                                        {
                                            subject
                                                ?.marksObtained
                                        }
                                    </TableCell>


                                    <TableCell>
                                        {
                                            subject
                                                ?.maximumMarks
                                        }
                                    </TableCell>


                                    <TableCell>

                                        <span className="font-semibold text-slate-900">
                                            {
                                                subject
                                                    ?.percentage
                                            }
                                            %
                                        </span>

                                    </TableCell>

                                </tr>

                            )
                        )}

                    </tbody>

                </table>

            </div>

        </section>

    );

}


function InfoCard({
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

            <p className="mt-1 font-semibold text-slate-950">
                {value}
            </p>

        </div>

    );

}


function MiniStat({
    label,
    value,
}) {

    return (

        <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">

            <p className="text-[11px] font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>

            <p className="mt-1 font-bold text-slate-950">
                {value}
            </p>

        </div>

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

        <td className="whitespace-nowrap px-6 py-4 text-sm text-slate-600">
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