import {
    ArrowLeft,
    CalendarDays,
    GraduationCap,
    Save,
    UserRound,
} from "lucide-react";

import {
    useEffect,
    useState,
} from "react";

import {
    useNavigate,
} from "react-router-dom";

import { academicApi } from "../../api/academicApi";
import { studentsApi } from "../../api/studentsApi";

export default function AddStudentPage() {
    const navigate =
        useNavigate();

    // ============================================================
    // FORM
    // ============================================================

    const [form, setForm] =
        useState({
            indexNumber: "",
            fullName: "",
            dateOfBirth: "",
            schoolClassId: "",
        });

    // ============================================================
    // ACADEMIC DATA
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
    // SELECTED ACADEMIC VALUES
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

    // ============================================================
    // UI STATE
    // ============================================================

    const [
        loadingInitialData,
        setLoadingInitialData,
    ] = useState(true);

    const [
        loadingGrades,
        setLoadingGrades,
    ] = useState(false);

    const [
        loadingClasses,
        setLoadingClasses,
    ] = useState(false);

    const [
        submitting,
        setSubmitting,
    ] = useState(false);

    const [
        error,
        setError,
    ] = useState("");

    const [
        success,
        setSuccess,
    ] = useState("");

    // ============================================================
    // INITIAL LOAD
    // Academic Years + Sections
    // ============================================================

    useEffect(() => {
        loadInitialAcademicData();
    }, []);

    const loadInitialAcademicData =
        async () => {
            try {
                setLoadingInitialData(
                    true
                );

                setError("");

                const [
                    academicYearsResponse,
                    sectionsResponse,
                ] = await Promise.all([
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
                    "Failed to load academic data:",
                    err
                );

                setError(
                    "Unable to load academic information."
                );
            }
            finally {
                setLoadingInitialData(
                    false
                );
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
                setLoadingGrades(
                    true
                );

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

                setError(
                    "Unable to load grades."
                );
            }
            finally {
                setLoadingGrades(
                    false
                );
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
                setLoadingClasses(
                    true
                );

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

                setError(
                    "Unable to load classes."
                );
            }
            finally {
                setLoadingClasses(
                    false
                );
            }
        };

    // ============================================================
    // NORMAL FORM CHANGE
    // ============================================================

    const handleChange =
        (event) => {
            const {
                name,
                value,
            } = event.target;

            setForm(
                (current) => ({
                    ...current,

                    [name]:
                        value,
                })
            );

            setError("");
        };

    // ============================================================
    // ACADEMIC YEAR CHANGE
    // ============================================================

    const handleAcademicYearChange =
        (event) => {
            setAcademicYearId(
                event.target.value
            );

            setError("");
        };

    // ============================================================
    // SECTION CHANGE
    // ============================================================

    const handleSectionChange =
        (event) => {
            const value =
                event.target.value;

            setSectionId(
                value
            );

            // Reset dependent values
            setGradeId("");

            setGrades([]);
            setClasses([]);

            setForm(
                (current) => ({
                    ...current,

                    schoolClassId:
                        "",
                })
            );

            setError("");

            if (value) {
                loadGrades(
                    value
                );
            }
        };

    // ============================================================
    // GRADE CHANGE
    // ============================================================

    const handleGradeChange =
        (event) => {
            const value =
                event.target.value;

            setGradeId(
                value
            );

            // Reset class
            setClasses([]);

            setForm(
                (current) => ({
                    ...current,

                    schoolClassId:
                        "",
                })
            );

            setError("");

            if (value) {
                loadClasses(
                    value
                );
            }
        };

    // ============================================================
    // VALIDATION
    // ============================================================

    const validateForm =
        () => {
            if (
                !form.indexNumber.trim()
            ) {
                return "Index number is required.";
            }

            if (
                !form.fullName.trim()
            ) {
                return "Full name is required.";
            }

            if (
                !academicYearId
            ) {
                return "Academic year is required.";
            }

            if (
                !sectionId
            ) {
                return "Section is required.";
            }

            if (
                !gradeId
            ) {
                return "Grade is required.";
            }

            if (
                !form.schoolClassId
            ) {
                return "Class is required.";
            }

            return "";
        };

    // ============================================================
    // SUBMIT
    // ============================================================

    const handleSubmit =
        async (event) => {
            event.preventDefault();

            setError("");
            setSuccess("");

            const validationMessage =
                validateForm();

            if (
                validationMessage
            ) {
                setError(
                    validationMessage
                );

                return;
            }

            try {
                setSubmitting(
                    true
                );

                const payload = {
                    indexNumber:
                        form.indexNumber
                            .trim(),

                    fullName:
                        form.fullName
                            .trim(),

                    dateOfBirth:
                        form.dateOfBirth
                            ? form.dateOfBirth
                            : null,

                    schoolClassId:
                        Number(
                            form.schoolClassId
                        ),

                    academicYearId:
                        Number(
                            academicYearId
                        ),
                };

                const response =
                    await studentsApi
                        .createStudent(
                            payload
                        );

                setSuccess(
                    response.data
                        ?.message ||
                    "Student created successfully."
                );

                setForm({
                    indexNumber: "",
                    fullName: "",
                    dateOfBirth: "",
                    schoolClassId: "",
                });

                setAcademicYearId("");
                setSectionId("");
                setGradeId("");

                setGrades([]);
                setClasses([]);
            }
            catch (err) {
                console.error(
                    "Failed to create student:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to create student."
                );
            }
            finally {
                setSubmitting(
                    false
                );
            }
        };

    // ============================================================
    // RENDER
    // ============================================================

    return (
        <div className="mx-auto w-full max-w-[1600px] pb-4">

            {/* ====================================================
                BACK
            ==================================================== */}

            <button
                type="button"
                onClick={() =>
                    navigate(
                        "/students"
                    )
                }
                className="inline-flex items-center gap-2 text-sm font-semibold text-slate-600 transition hover:text-blue-600"
            >
                <ArrowLeft className="h-4 w-4" />

                Back to Students
            </button>

            {/* ====================================================
                PAGE HEADER
            ==================================================== */}

            <div className="mt-6">

                <p className="text-sm font-semibold text-blue-600">
                    Student Management
                </p>

                <h1 className="mt-1 text-3xl font-bold tracking-tight text-slate-950">
                    Add Student
                </h1>

                <p className="mt-2 text-sm leading-6 text-slate-500">
                    Create a new student record and assign the student to an academic year and class.
                </p>

            </div>

            {/* ====================================================
                FORM
            ==================================================== */}

            <form
                onSubmit={
                    handleSubmit
                }
                className="mt-8"
            >

                <div className="grid gap-6 xl:grid-cols-2">

                    {/* =================================================
                        STUDENT INFORMATION
                    ================================================= */}

                    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                        <div className="flex items-center gap-3">

                            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                                <UserRound className="h-5 w-5" />
                            </div>

                            <div>

                                <h2 className="font-semibold text-slate-950">
                                    Student Information
                                </h2>

                                <p className="text-xs text-slate-500">
                                    Enter the student's basic information
                                </p>

                            </div>

                        </div>

                        <div className="mt-6 space-y-5">

                            {/* INDEX NUMBER */}

                            <div>

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Index Number

                                    <span className="ml-1 text-red-500">
                                        *
                                    </span>
                                </label>

                                <input
                                    type="text"
                                    name="indexNumber"
                                    value={
                                        form.indexNumber
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    placeholder="Example: ST2030009"
                                    autoComplete="off"
                                    className="h-11 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                />

                            </div>

                            {/* FULL NAME */}

                            <div>

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Full Name

                                    <span className="ml-1 text-red-500">
                                        *
                                    </span>
                                </label>

                                <input
                                    type="text"
                                    name="fullName"
                                    value={
                                        form.fullName
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    placeholder="Enter student's full name"
                                    autoComplete="off"
                                    className="h-11 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                />

                            </div>

                            {/* DATE OF BIRTH */}

                            <div>

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Date of Birth
                                </label>

                                <div className="relative">

                                    <CalendarDays className="pointer-events-none absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-400" />

                                    <input
                                        type="date"
                                        name="dateOfBirth"
                                        value={
                                            form.dateOfBirth
                                        }
                                        onChange={
                                            handleChange
                                        }
                                        className="h-11 w-full rounded-xl border border-slate-200 bg-white pl-12 pr-4 text-sm text-slate-900 outline-none transition focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                    />

                                </div>

                                <p className="mt-2 text-xs text-slate-400">
                                    Optional
                                </p>

                            </div>

                        </div>

                    </div>

                    {/* =================================================
                        ACADEMIC PLACEMENT
                    ================================================= */}

                    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                        <div className="flex items-center gap-3">

                            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                                <GraduationCap className="h-5 w-5" />
                            </div>

                            <div>

                                <h2 className="font-semibold text-slate-950">
                                    Academic Placement
                                </h2>

                                <p className="text-xs text-slate-500">
                                    Select academic year, section, grade and class
                                </p>

                            </div>

                        </div>

                        <div className="mt-6 space-y-5">

                            {/* ACADEMIC YEAR */}

                            <div>

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Academic Year

                                    <span className="ml-1 text-red-500">
                                        *
                                    </span>
                                </label>

                                <select
                                    value={
                                        academicYearId
                                    }
                                    onChange={
                                        handleAcademicYearChange
                                    }
                                    disabled={
                                        loadingInitialData
                                    }
                                    className="h-11 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                >
                                    <option value="">
                                        {loadingInitialData
                                            ? "Loading academic years..."
                                            : "Select Academic Year"}
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

                            </div>

                            {/* SECTION */}

                            <div>

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Section

                                    <span className="ml-1 text-red-500">
                                        *
                                    </span>
                                </label>

                                <select
                                    value={
                                        sectionId
                                    }
                                    onChange={
                                        handleSectionChange
                                    }
                                    disabled={
                                        loadingInitialData
                                    }
                                    className="h-11 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                >
                                    <option value="">
                                        {loadingInitialData
                                            ? "Loading sections..."
                                            : "Select Section"}
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

                            </div>

                            {/* GRADE */}

                            <div>

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Grade

                                    <span className="ml-1 text-red-500">
                                        *
                                    </span>
                                </label>

                                <select
                                    value={
                                        gradeId
                                    }
                                    onChange={
                                        handleGradeChange
                                    }
                                    disabled={
                                        !sectionId ||
                                        loadingGrades
                                    }
                                    className="h-11 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                >
                                    <option value="">
                                        {loadingGrades
                                            ? "Loading grades..."
                                            : !sectionId
                                                ? "Select Section First"
                                                : "Select Grade"}
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

                            </div>

                            {/* CLASS */}

                            <div>

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Class

                                    <span className="ml-1 text-red-500">
                                        *
                                    </span>
                                </label>

                                <select
                                    name="schoolClassId"
                                    value={
                                        form.schoolClassId
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    disabled={
                                        !gradeId ||
                                        loadingClasses
                                    }
                                    className="h-11 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                >
                                    <option value="">
                                        {loadingClasses
                                            ? "Loading classes..."
                                            : !gradeId
                                                ? "Select Grade First"
                                                : "Select Class"}
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

                            </div>

                        </div>

                    </div>

                </div>

                {/* =================================================
                    ERROR
                ================================================= */}

                {error && (
                    <div className="mt-6 rounded-xl border border-red-100 bg-red-50 px-5 py-4 text-sm font-medium text-red-700">
                        {error}
                    </div>
                )}

                {/* =================================================
                    SUCCESS
                ================================================= */}

                {success && (
                    <div className="mt-6 rounded-xl border border-emerald-100 bg-emerald-50 px-5 py-4 text-sm font-medium text-emerald-700">
                        {success}
                    </div>
                )}

                {/* =================================================
                    ACTIONS
                ================================================= */}

                <div className="mt-6 flex flex-col-reverse gap-3 pb-2 sm:flex-row sm:justify-end">

                    <button
                        type="button"
                        onClick={() =>
                            navigate(
                                "/students"
                            )
                        }
                        disabled={
                            submitting
                        }
                        className="inline-flex h-11 items-center justify-center rounded-xl border border-slate-200 bg-white px-5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                        Cancel
                    </button>

                    <button
                        type="submit"
                        disabled={
                            submitting ||
                            loadingInitialData
                        }
                        className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                        {submitting ? (
                            <>
                                <div className="h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />

                                Creating...
                            </>
                        ) : (
                            <>
                                <Save className="h-4 w-4" />

                                Create Student
                            </>
                        )}
                    </button>

                </div>

            </form>

        </div>
    );
}