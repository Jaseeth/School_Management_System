import {
    CalendarDays,
    Download,
    FileSpreadsheet,
    GraduationCap,
    Plus,
    Search,
    Upload,
    UserCheck,
    UserRoundCheck,
} from "lucide-react";

import {
    useEffect,
    useState,
} from "react";

import { studentsApi } from "../../api/studentsApi";
import { academicApi } from "../../api/academicApi";
import { useNavigate } from "react-router-dom";

export default function StudentsPage() {

    // ============================================================
    // STUDENT DATA
    // ============================================================

    const [students, setStudents] =
        useState([]);

    const [summary, setSummary] =
        useState({
            totalStudents: 0,
            activeStudents: 0,
            graduatedStudents: 0,
            activeAcademicYearId: null,
            activeAcademicYearName: "",
        });

    // ============================================================
    // SEARCH
    // ============================================================

    const [search, setSearch] =
        useState("");

    const [
        debouncedSearch,
        setDebouncedSearch,
    ] = useState("");

    // ============================================================
    // FILTER DATA
    // ============================================================

    const [
        academicYears,
        setAcademicYears,
    ] = useState([]);

    const [
        sections,
        setSections,
    ] = useState([]);

    const [
        grades,
        setGrades,
    ] = useState([]);

    const [
        classes,
        setClasses,
    ] = useState([]);

    // ============================================================
    // SELECTED FILTERS
    // ============================================================

    const [
        academicYearId,
        setAcademicYearId,
    ] = useState("");

    const [
        sectionId,
        setSectionId,
    ] = useState("");

    const [
        gradeId,
        setGradeId,
    ] = useState("");

    const [
        schoolClassId,
        setSchoolClassId,
    ] = useState("");

    const [
        status,
        setStatus,
    ] = useState("");

    // ============================================================
    // PAGINATION
    // ============================================================

    const [page, setPage] =
        useState(1);

    const pageSize = 20;

    const [
        totalCount,
        setTotalCount,
    ] = useState(0);

    const [
        totalPages,
        setTotalPages,
    ] = useState(0);

    // ============================================================
    // UI STATE
    // ============================================================

    const [
        loading,
        setLoading,
    ] = useState(true);

    const [
        summaryLoading,
        setSummaryLoading,
    ] = useState(true);

    const [
        filtersLoading,
        setFiltersLoading,
    ] = useState(true);

    const [
        gradesLoading,
        setGradesLoading,
    ] = useState(false);

    const [
        classesLoading,
        setClassesLoading,
    ] = useState(false);

    const [
        error,
        setError,
    ] = useState("");

    const navigate =
        useNavigate();

    // ============================================================
    // LOAD SUMMARY
    // ============================================================

    const loadSummary =
        async () => {
            try {
                setSummaryLoading(true);

                const response =
                    await studentsApi
                        .getManagementSummary();

                setSummary({
                    totalStudents:
                        response.data
                            ?.totalStudents ??
                        0,

                    activeStudents:
                        response.data
                            ?.activeStudents ??
                        0,

                    graduatedStudents:
                        response.data
                            ?.graduatedStudents ??
                        0,

                    activeAcademicYearId:
                        response.data
                            ?.activeAcademicYearId ??
                        null,

                    activeAcademicYearName:
                        response.data
                            ?.activeAcademicYearName ??
                        "",
                });
            }
            catch (err) {
                console.error(
                    "Failed to load student summary:",
                    err
                );
            }
            finally {
                setSummaryLoading(false);
            }
        };

    // ============================================================
    // LOAD ACADEMIC YEAR + SECTION FILTERS
    // ============================================================

    const loadAcademicFilters =
        async () => {
            try {
                setFiltersLoading(true);

                const [
                    academicYearsResponse,
                    sectionsResponse,
                ] =
                    await Promise.all([
                        academicApi
                            .getAcademicYears(),

                        academicApi
                            .getSections(),
                    ]);

                setAcademicYears(
                    academicYearsResponse
                        .data ?? []
                );

                setSections(
                    sectionsResponse
                        .data ?? []
                );
            }
            catch (err) {
                console.error(
                    "Failed to load academic filters:",
                    err
                );
            }
            finally {
                setFiltersLoading(false);
            }
        };

    // ============================================================
    // LOAD GRADES
    // ============================================================

    const loadGrades =
        async (
            selectedSectionId
        ) => {
            try {
                setGradesLoading(true);

                const response =
                    await academicApi
                        .getGrades(
                            selectedSectionId
                        );

                setGrades(
                    response.data ?? []
                );
            }
            catch (err) {
                console.error(
                    "Failed to load grades:",
                    err
                );

                setGrades([]);
            }
            finally {
                setGradesLoading(false);
            }
        };

    // ============================================================
    // LOAD CLASSES
    // ============================================================

    const loadClasses =
        async (
            selectedGradeId
        ) => {
            try {
                setClassesLoading(true);

                const response =
                    await academicApi
                        .getClasses(
                            selectedGradeId
                        );

                setClasses(
                    response.data ?? []
                );
            }
            catch (err) {
                console.error(
                    "Failed to load classes:",
                    err
                );

                setClasses([]);
            }
            finally {
                setClassesLoading(false);
            }
        };

    // ============================================================
    // LOAD STUDENTS
    // ============================================================

    const loadStudents =
        async () => {
            try {
                setLoading(true);
                setError("");

                const params = {
                    page,
                    pageSize,
                };

                if (
                    debouncedSearch
                ) {
                    params.search =
                        debouncedSearch;
                }

                if (
                    academicYearId
                ) {
                    params.academicYearId =
                        academicYearId;
                }

                if (sectionId) {
                    params.sectionId =
                        sectionId;
                }

                if (gradeId) {
                    params.gradeId =
                        gradeId;
                }

                if (
                    schoolClassId
                ) {
                    params.schoolClassId =
                        schoolClassId;
                }

                if (
                    status ===
                    "active"
                ) {
                    params.isActive =
                        true;
                }

                if (
                    status ===
                    "inactive"
                ) {
                    params.isActive =
                        false;
                }

                const response =
                    await studentsApi
                        .getManagementStudents(
                            params
                        );

                const data =
                    response.data;

                setStudents(
                    data?.students ?? []
                );

                setTotalCount(
                    data?.totalCount ?? 0
                );

                setTotalPages(
                    data?.totalPages ?? 0
                );
            }
            catch (err) {
                console.error(
                    "Failed to load students:",
                    err
                );

                setStudents([]);
                setTotalCount(0);
                setTotalPages(0);

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load student records."
                );
            }
            finally {
                setLoading(false);
            }
        };

    // ============================================================
    // INITIAL LOAD
    // ============================================================

    useEffect(() => {
        loadSummary();
        loadAcademicFilters();
    }, []);

    // ============================================================
    // SEARCH DEBOUNCE
    // ============================================================

    useEffect(() => {
        const timer =
            setTimeout(() => {
                setDebouncedSearch(
                    search.trim()
                );

                setPage(1);
            }, 400);

        return () =>
            clearTimeout(timer);

    }, [search]);

    // ============================================================
    // SECTION -> GRADES
    // ============================================================

    useEffect(() => {
        if (!sectionId) {
            return;
        }

        loadGrades(
            sectionId
        );

    }, [sectionId]);

    // ============================================================
    // GRADE -> CLASSES
    // ============================================================

    useEffect(() => {
        if (!gradeId) {
            return;
        }

        loadClasses(
            gradeId
        );

    }, [gradeId]);

    // ============================================================
    // RELOAD STUDENTS WHEN FILTER CHANGES
    // ============================================================

    useEffect(() => {
        loadStudents();

    }, [
        debouncedSearch,
        academicYearId,
        sectionId,
        gradeId,
        schoolClassId,
        status,
        page,
    ]);

    // ============================================================
    // FILTER HANDLERS
    // ============================================================

    const handleAcademicYearChange =
        (event) => {
            setAcademicYearId(
                event.target.value
            );

            setPage(1);
        };

    const handleSectionChange =
        (event) => {
            const value =
                event.target.value;

            setSectionId(value);

            // Reset dependent filters immediately
            setGradeId("");
            setSchoolClassId("");

            setGrades([]);
            setClasses([]);

            setPage(1);
        };

    const handleGradeChange =
        (event) => {
            const value =
                event.target.value;

            setGradeId(value);

            // Reset dependent class immediately
            setSchoolClassId("");
            setClasses([]);

            setPage(1);
        };

    const handleClassChange =
        (event) => {
            setSchoolClassId(
                event.target.value
            );

            setPage(1);
        };

    const handleStatusChange =
        (event) => {
            setStatus(
                event.target.value
            );

            setPage(1);
        };

    // ============================================================
    // PAGINATION
    // ============================================================

    const goPrevious = () => {
        if (page > 1) {
            setPage(
                (current) =>
                    current - 1
            );
        }
    };

    const goNext = () => {
        if (
            totalPages > 0 &&
            page < totalPages
        ) {
            setPage(
                (current) =>
                    current + 1
            );
        }
    };

    // ============================================================
    // PAGINATION RANGE
    // ============================================================

    const startRecord =
        totalCount === 0
            ? 0
            : (page - 1) *
            pageSize +
            1;

    const endRecord =
        Math.min(
            page * pageSize,
            totalCount
        );

    // ============================================================
    // RENDER
    // ============================================================

    return (
        <div className="mx-auto w-full max-w-[1600px]">

            {/* ====================================================
                HEADER
            ==================================================== */}

            <div className="flex flex-col gap-6 xl:flex-row xl:items-start xl:justify-between">

                <div className="max-w-2xl">

                    <p className="text-sm font-semibold text-blue-600">
                        Student Management
                    </p>

                    <h2 className="mt-1 text-3xl font-bold tracking-tight text-slate-950">
                        Students
                    </h2>

                    <p className="mt-2 text-sm leading-6 text-slate-500">
                        Manage student records,
                        academic information,
                        imports and exports.
                    </p>

                </div>

                <div className="flex flex-wrap gap-3">

                    <button
                        type="button"
                        className="inline-flex h-11 items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 text-sm font-semibold text-slate-700 shadow-sm transition hover:border-slate-300 hover:bg-slate-50"
                    >
                        <Upload className="h-4 w-4" />
                        Import Students
                    </button>

                    <button
                        type="button"
                        className="inline-flex h-11 items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 text-sm font-semibold text-slate-700 shadow-sm transition hover:border-slate-300 hover:bg-slate-50"
                    >
                        <Download className="h-4 w-4" />
                        Export Students
                    </button>

                    <button
                        type="button"
                        onClick={() =>
                            navigate(
                                "/students/add"
                            )
                        }
                        className="inline-flex h-11 items-center gap-2 rounded-xl bg-blue-600 px-4 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700"
                    >
                        <Plus className="h-4 w-4" />
                        Add Student
                    </button>

                </div>

            </div>

            {/* ====================================================
                SUMMARY CARDS
            ==================================================== */}

            <div className="mt-8 grid gap-5 sm:grid-cols-2 xl:grid-cols-4">

                {/* TOTAL */}

                <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">

                    <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                        <GraduationCap className="h-5 w-5" />
                    </div>

                    <p className="mt-5 text-sm font-medium text-slate-500">
                        Total Students
                    </p>

                    <p className="mt-1 text-3xl font-bold text-slate-950">
                        {summaryLoading
                            ? "..."
                            : summary.totalStudents}
                    </p>

                </div>

                {/* ACTIVE */}

                <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">

                    <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-emerald-50 text-emerald-700">
                        <UserCheck className="h-5 w-5" />
                    </div>

                    <p className="mt-5 text-sm font-medium text-slate-500">
                        Active Students
                    </p>

                    <p className="mt-1 text-3xl font-bold text-slate-950">
                        {summaryLoading
                            ? "..."
                            : summary.activeStudents}
                    </p>

                    <p className="mt-2 text-xs font-medium text-emerald-600">
                        Currently active
                    </p>

                </div>

                {/* COMPLETED */}

                <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">

                    <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-slate-100 text-slate-600">
                        <UserRoundCheck className="h-5 w-5" />
                    </div>

                    <p className="mt-5 text-sm font-medium text-slate-500">
                        Completed
                    </p>

                    <p className="mt-1 text-3xl font-bold text-slate-950">
                        {summaryLoading
                            ? "..."
                            : summary.graduatedStudents}
                    </p>

                    <p className="mt-2 text-xs text-slate-400">
                        Completed students
                    </p>

                </div>

                {/* ACADEMIC YEAR */}

                <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">

                    <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                        <CalendarDays className="h-5 w-5" />
                    </div>

                    <p className="mt-5 text-sm font-medium text-slate-500">
                        Current Academic Year
                    </p>

                    <p className="mt-1 text-xl font-bold text-slate-950">
                        {summaryLoading
                            ? "..."
                            : summary.activeAcademicYearName ||
                            "Not set"}
                    </p>

                    <p className="mt-2 text-xs text-slate-400">
                        Active academic year
                    </p>

                </div>

            </div>

            {/* ====================================================
                STUDENT LIST
            ==================================================== */}

            <div className="mt-8 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">

                {/* =================================================
                    SEARCH + FILTERS
                ================================================= */}

                <div className="border-b border-slate-200 p-5 sm:p-6">

                    <div className="flex flex-col gap-4">

                        {/* SEARCH */}

                        <div className="relative w-full lg:max-w-xl">

                            <Search className="absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400" />

                            <input
                                type="text"
                                value={search}
                                onChange={(event) =>
                                    setSearch(
                                        event.target.value
                                    )
                                }
                                placeholder="Search by student name or index number..."
                                className="h-11 w-full rounded-xl border border-slate-200 bg-slate-50 pl-12 pr-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:bg-white focus:ring-4 focus:ring-blue-500/10"
                            />

                        </div>

                        {/* FILTERS */}

                        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">

                            {/* ACADEMIC YEAR */}

                            <select
                                value={academicYearId}
                                onChange={
                                    handleAcademicYearChange
                                }
                                disabled={
                                    filtersLoading
                                }
                                className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                            >
                                <option value="">
                                    {filtersLoading
                                        ? "Loading Academic Years..."
                                        : "All Academic Years"}
                                </option>

                                {academicYears.map(
                                    (year) => (
                                        <option
                                            key={
                                                year.id
                                            }
                                            value={
                                                year.id
                                            }
                                        >
                                            {
                                                year.name
                                            }
                                        </option>
                                    )
                                )}

                            </select>

                            {/* SECTION */}

                            <select
                                value={sectionId}
                                onChange={
                                    handleSectionChange
                                }
                                disabled={
                                    filtersLoading
                                }
                                className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                            >
                                <option value="">
                                    {filtersLoading
                                        ? "Loading Sections..."
                                        : "All Sections"}
                                </option>

                                {sections.map(
                                    (section) => (
                                        <option
                                            key={
                                                section.id
                                            }
                                            value={
                                                section.id
                                            }
                                        >
                                            {
                                                section.name
                                            }
                                        </option>
                                    )
                                )}

                            </select>

                            {/* GRADE */}

                            <select
                                value={gradeId}
                                onChange={
                                    handleGradeChange
                                }
                                disabled={
                                    !sectionId ||
                                    gradesLoading
                                }
                                className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                            >
                                <option value="">
                                    {gradesLoading
                                        ? "Loading Grades..."
                                        : !sectionId
                                            ? "Select Section First"
                                            : "All Grades"}
                                </option>

                                {grades.map(
                                    (grade) => (
                                        <option
                                            key={
                                                grade.id
                                            }
                                            value={
                                                grade.id
                                            }
                                        >
                                            {
                                                grade.name
                                            }
                                        </option>
                                    )
                                )}

                            </select>

                            {/* CLASS */}

                            <select
                                value={
                                    schoolClassId
                                }
                                onChange={
                                    handleClassChange
                                }
                                disabled={
                                    !gradeId ||
                                    classesLoading
                                }
                                className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                            >
                                <option value="">
                                    {classesLoading
                                        ? "Loading Classes..."
                                        : !gradeId
                                            ? "Select Grade First"
                                            : "All Classes"}
                                </option>

                                {classes.map(
                                    (
                                        schoolClass
                                    ) => (
                                        <option
                                            key={
                                                schoolClass.id
                                            }
                                            value={
                                                schoolClass.id
                                            }
                                        >
                                            {
                                                schoolClass.name
                                            }
                                        </option>
                                    )
                                )}

                            </select>

                            {/* STATUS */}

                            <select
                                value={status}
                                onChange={
                                    handleStatusChange
                                }
                                className="h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700 outline-none transition focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                            >
                                <option value="">
                                    All Status
                                </option>

                                <option value="active">
                                    Active
                                </option>

                                <option value="inactive">
                                    Inactive
                                </option>

                            </select>

                        </div>

                    </div>

                </div>

                {/* =================================================
                    ERROR
                ================================================= */}

                {error && (
                    <div className="border-b border-red-100 bg-red-50 px-6 py-4 text-sm text-red-700">
                        {error}
                    </div>
                )}

                {/* =================================================
                    TABLE
                ================================================= */}

                <div className="overflow-x-auto">

                    <table className="w-full min-w-[1000px]">

                        <thead className="bg-slate-50/80">

                            <tr className="border-b border-slate-200">

                                <th className="px-6 py-4 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Student
                                </th>

                                <th className="px-6 py-4 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Index Number
                                </th>

                                <th className="px-6 py-4 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Class
                                </th>

                                <th className="px-6 py-4 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Academic Year
                                </th>

                                <th className="px-6 py-4 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Status
                                </th>

                                <th className="px-6 py-4 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Action
                                </th>

                            </tr>

                        </thead>

                        <tbody>

                            {/* LOADING */}

                            {loading && (
                                <tr>
                                    <td
                                        colSpan="6"
                                        className="px-6 py-16 text-center"
                                    >

                                        <div className="mx-auto h-8 w-8 animate-spin rounded-full border-2 border-slate-200 border-t-blue-600" />

                                        <p className="mt-4 text-sm text-slate-500">
                                            Loading students...
                                        </p>

                                    </td>
                                </tr>
                            )}

                            {/* EMPTY */}

                            {!loading &&
                                students.length ===
                                0 && (
                                    <tr>
                                        <td
                                            colSpan="6"
                                            className="px-6 py-20 text-center"
                                        >

                                            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-slate-100 text-slate-400">
                                                <FileSpreadsheet className="h-6 w-6" />
                                            </div>

                                            <p className="mt-4 text-sm font-semibold text-slate-800">
                                                No students found
                                            </p>

                                            <p className="mt-1 text-sm text-slate-400">
                                                Try changing your search or filters.
                                            </p>

                                        </td>
                                    </tr>
                                )}

                            {/* STUDENTS */}

                            {!loading &&
                                students.map(
                                    (
                                        student
                                    ) => (
                                        <tr
                                            key={
                                                student.id
                                            }
                                            className="border-b border-slate-100 transition last:border-b-0 hover:bg-slate-50/70"
                                        >

                                            {/* STUDENT */}

                                            <td className="px-6 py-4">

                                                <div className="flex items-center gap-3">

                                                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-sm font-bold text-blue-700">
                                                        {student
                                                            .fullName
                                                            ?.charAt(
                                                                0
                                                            )
                                                            ?.toUpperCase() ||
                                                            "S"}
                                                    </div>

                                                    <div className="min-w-0">

                                                        <p className="truncate text-sm font-semibold text-slate-900">
                                                            {
                                                                student.fullName
                                                            }
                                                        </p>

                                                        <p className="mt-0.5 text-xs text-slate-400">

                                                            {student.section ||
                                                                "—"}

                                                            {student.grade
                                                                ? ` / Grade ${student.grade}`
                                                                : ""}

                                                        </p>

                                                    </div>

                                                </div>

                                            </td>

                                            {/* INDEX */}

                                            <td className="px-6 py-4 text-sm font-medium text-slate-700">
                                                {
                                                    student.indexNumber
                                                }
                                            </td>

                                            {/* CLASS */}

                                            <td className="px-6 py-4 text-sm text-slate-600">
                                                {student.class ||
                                                    "—"}
                                            </td>

                                            {/* YEAR */}

                                            <td className="px-6 py-4 text-sm text-slate-600">
                                                {student.academicYear ||
                                                    "—"}
                                            </td>

                                            {/* STATUS */}

                                            <td className="px-6 py-4">

                                                <span
                                                    className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${student.isActive
                                                            ? "bg-emerald-50 text-emerald-700"
                                                            : "bg-slate-100 text-slate-600"
                                                        }`}
                                                >
                                                    {student.isActive
                                                        ? "Active"
                                                        : "Inactive"}
                                                </span>

                                            </td>

                                            {/* ACTION */}

                                            <td className="px-6 py-4 text-right">

                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        navigate(
                                                            `/students/${student.id}`
                                                        )
                                                    }
                                                    className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-semibold text-slate-700 transition hover:border-blue-200 hover:bg-blue-50 hover:text-blue-700"
                                                >
                                                    View
                                                </button>

                                            </td>

                                        </tr>
                                    )
                                )}

                        </tbody>

                    </table>

                </div>

                {/* =================================================
                    PAGINATION
                ================================================= */}

                <div className="flex flex-col gap-3 border-t border-slate-200 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6">

                    <p className="text-sm text-slate-500">

                        {totalCount === 0
                            ? "Showing 0 students"
                            : `Showing ${startRecord}–${endRecord} of ${totalCount} students`}

                    </p>

                    <div className="flex items-center gap-2">

                        <button
                            type="button"
                            onClick={
                                goPrevious
                            }
                            disabled={
                                loading ||
                                page <= 1
                            }
                            className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            Previous
                        </button>

                        <div className="min-w-[90px] text-center text-sm text-slate-500">

                            {totalPages > 0
                                ? `${page} / ${totalPages}`
                                : "0 / 0"}

                        </div>

                        <button
                            type="button"
                            onClick={
                                goNext
                            }
                            disabled={
                                loading ||
                                totalPages ===
                                0 ||
                                page >=
                                totalPages
                            }
                            className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            Next
                        </button>

                    </div>

                </div>

            </div>

        </div>
    );
}