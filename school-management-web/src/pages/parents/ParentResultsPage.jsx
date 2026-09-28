import { ArrowLeft, Award, GraduationCap } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

export default function ParentResultsPage() {
    const { studentId } = useParams();

    const [profile, setProfile] = useState(null);
    const [yearId, setYearId] = useState("");
    const [termId, setTermId] = useState("");
    const [results, setResults] = useState(null);
    const [loadingProfile, setLoadingProfile] = useState(true);
    const [loadingResults, setLoadingResults] = useState(false);
    const [error, setError] = useState("");

    useEffect(() => {
        let active = true;

        setLoadingProfile(true);
        setProfile(null);
        setYearId("");
        setTermId("");
        setResults(null);
        setError("");

        parentPortalApi.getChildAcademicProfile(studentId)
            .then(({ data }) => {
                if (!active) return;

                setProfile(data);

                const published = data.publishedResults ?? [];
                const preferredYear =
                    data.currentEnrollment?.academicYearId;

                const first =
                    published.find(
                        (item) =>
                            item.academicYearId === preferredYear
                    ) ?? published[0];

                if (first) {
                    setYearId(String(first.academicYearId));
                    setTermId(String(first.academicTermId));
                }
            })
            .catch((err) => {
                if (!active) return;

                setError(
                    err?.response?.status === 403
                        ? "This student is not linked to your parent account."
                        : err?.response?.data?.message ||
                        "Unable to load published results."
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
        if (!profile || !yearId || !termId) return;

        let active = true;

        setResults(null);
        setLoadingResults(true);
        setError("");

        parentPortalApi
            .getChildResults(
                studentId,
                Number(yearId),
                Number(termId)
            )
            .then(({ data }) => {
                if (active) setResults(data);
            })
            .catch((err) => {
                if (!active) return;

                setError(
                    err?.response?.status === 403
                        ? "This student is not linked to your parent account."
                        : err?.response?.data?.message ||
                        "Unable to load results for this term."
                );
            })
            .finally(() => {
                if (active) setLoadingResults(false);
            });

        return () => {
            active = false;
        };
    }, [profile, studentId, yearId, termId]);

    const published = profile?.publishedResults ?? [];

    const years = Array.from(
        new Map(
            published.map((item) => [
                item.academicYearId,
                {
                    id: item.academicYearId,
                    name: item.academicYearName,
                },
            ])
        ).values()
    );

    const terms = Array.from(
        new Map(
            published
                .filter(
                    (item) =>
                        String(item.academicYearId) === yearId
                )
                .map((item) => [
                    item.academicTermId,
                    {
                        id: item.academicTermId,
                        name: item.academicTermName,
                    },
                ])
        ).values()
    );

    function changeYear(event) {
        const nextYearId = event.target.value;

        const firstTerm = published.find(
            (item) =>
                String(item.academicYearId) === nextYearId
        );

        setYearId(nextYearId);
        setTermId(
            firstTerm
                ? String(firstTerm.academicTermId)
                : ""
        );
        setResults(null);
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
                    <Award className="h-6 w-6" />
                    <span className="text-sm font-semibold">
                        Published Results
                    </span>
                </div>

                <h1 className="mt-2 text-3xl font-bold text-slate-950">
                    {profile?.student?.fullName || "Results"}
                </h1>

                {profile?.student && (
                    <p className="mt-2 text-sm text-slate-600">
                        Index Number: {profile.student.indexNumber}
                    </p>
                )}

                {loadingProfile && (
                    <p role="status" className="mt-7 text-slate-600">
                        Loading published results...
                    </p>
                )}

                {!loadingProfile && years.length > 0 && (
                    <div className="mt-7 flex flex-wrap gap-4">
                        <label className="block min-w-52 text-sm font-semibold text-slate-700">
                            Academic Year
                            <select
                                value={yearId}
                                onChange={changeYear}
                                className="mt-2 block w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-3 font-normal text-slate-900 focus:border-blue-500 focus:outline-none"
                            >
                                {years.map((year) => (
                                    <option
                                        key={year.id}
                                        value={year.id}
                                    >
                                        {year.name}
                                    </option>
                                ))}
                            </select>
                        </label>

                        <label className="block min-w-52 text-sm font-semibold text-slate-700">
                            Term
                            <select
                                value={termId}
                                onChange={(event) =>
                                    setTermId(event.target.value)
                                }
                                className="mt-2 block w-full cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-3 font-normal text-slate-900 focus:border-blue-500 focus:outline-none"
                            >
                                {terms.map((term) => (
                                    <option
                                        key={term.id}
                                        value={term.id}
                                    >
                                        {term.name}
                                    </option>
                                ))}
                            </select>
                        </label>
                    </div>
                )}

                {!loadingProfile &&
                    !error &&
                    years.length === 0 && (
                        <p className="mt-7 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                            No published results are available
                            for this student yet.
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

                {loadingResults && (
                    <p role="status" className="mt-7 text-slate-600">
                        Loading term results...
                    </p>
                )}

                {!loadingResults && !error && results && (
                    <section className="mt-8">
                        <h2 className="text-xl font-bold text-slate-950">
                            {results.academicYearName}
                            {" · "}
                            {results.academicTermName}
                        </h2>

                        <p className="mt-1 text-sm text-slate-500">
                            Published marks: {results.totalResults}
                        </p>

                        {results.results?.length ? (
                            <div className="mt-4 overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
                                <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                                    <thead className="bg-slate-50 text-slate-600">
                                        <tr>
                                            <th className="px-5 py-3 font-semibold">
                                                Exam
                                            </th>
                                            <th className="px-5 py-3 font-semibold">
                                                Subject
                                            </th>
                                            <th className="px-5 py-3 font-semibold">
                                                Marks
                                            </th>
                                            <th className="px-5 py-3 font-semibold">
                                                Percentage
                                            </th>
                                        </tr>
                                    </thead>

                                    <tbody className="divide-y divide-slate-100">
                                        {results.results.map((item) => (
                                            <tr
                                                key={`${item.examId}-${item.subjectId}`}
                                            >
                                                <td className="px-5 py-4 font-semibold text-slate-800">
                                                    {item.examName}
                                                </td>
                                                <td className="px-5 py-4 text-slate-700">
                                                    {item.subjectName}
                                                </td>
                                                <td className="px-5 py-4 text-slate-700">
                                                    {item.marksObtained}
                                                    {" / "}
                                                    {item.maximumMarks}
                                                </td>
                                                <td className="px-5 py-4 text-slate-700">
                                                    {item.percentage}%
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        ) : (
                            <p className="mt-4 rounded-2xl border border-slate-200 bg-white p-6 text-slate-600">
                                No published results for this term.
                            </p>
                        )}
                    </section>
                )}
            </main>
        </div>
    );
}