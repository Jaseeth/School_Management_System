import {
    ArrowLeft,
    ArrowRight,
    CalendarDays,
    Check,
    CheckCircle2,
    GraduationCap,
    Mail,
    Phone,
    Save,
    UserRound,
    UsersRound,
    BookOpen,
    ClipboardCheck,
} from "lucide-react";

import {
    useEffect,
    useMemo,
    useState,
} from "react";

import {
    useNavigate,
} from "react-router-dom";

import {
    academicApi,
} from "../../api/academicApi";

import {
    studentsApi,
} from "../../api/studentsApi";

import {
    parentGuardiansApi,
} from "../../api/parentGuardiansApi";

import {
    studentEnrollmentsApi,
} from "../../api/studentEnrollmentsApi";


const steps = [
    {
        id: 1,
        title: "Student Information",
        description: "Basic student details",
        icon: UserRound,
    },
    {
        id: 2,
        title: "Academic Placement",
        description: "Year, section, grade and class",
        icon: GraduationCap,
    },
    {
        id: 3,
        title: "Subjects",
        description: "Assign student subjects",
        icon: BookOpen,
    },
    {
        id: 4,
        title: "Parent / Guardian",
        description: "Parent or guardian information",
        icon: UsersRound,
    },
    {
        id: 5,
        title: "Review & Create",
        description: "Review all details",
        icon: ClipboardCheck,
    },
];


export default function AddStudentPage() {

    const navigate =
        useNavigate();

    // ============================================================
    // CURRENT STEP
    // ============================================================

    const [
        currentStep,
        setCurrentStep,
    ] = useState(1);


    // ============================================================
    // STUDENT FORM
    // ============================================================

    const [
        studentForm,
        setStudentForm,
    ] = useState({
        indexNumber: "",
        fullName: "",
        dateOfBirth: "",
        email: "",
        mobile: "",
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

    const [
        subjects,
        setSubjects,
    ] = useState([]);


    // ============================================================
    // ACADEMIC SELECTIONS
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
        selectedSubjectIds,
        setSelectedSubjectIds,
    ] = useState([]);


    // ============================================================
    // PARENT FORM
    // ============================================================

    const [
        parentForm,
        setParentForm,
    ] = useState({
        parentNumber: "",
        fullName: "",
        email: "",
        phoneNumber: "",
        relationship: "",
        isPrimaryGuardian: true,
        isEmergencyContact: true,
    });


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
        loadingSubjects,
        setLoadingSubjects,
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
    // ============================================================

    useEffect(() => {

        loadInitialData();

    }, []);


    const loadInitialData =
        async () => {

            try {

                setLoadingInitialData(true);
                setLoadingSubjects(true);

                setError("");

                const [
                    academicYearsResponse,
                    sectionsResponse,
                    subjectsResponse,
                ] = await Promise.all([
                    academicApi
                        .getAcademicYears(),

                    academicApi
                        .getSections(),

                    academicApi
                        .getSubjects(),
                ]);

                setAcademicYears(
                    academicYearsResponse
                        .data ?? []
                );

                setSections(
                    sectionsResponse
                        .data ?? []
                );

                setSubjects(
                    subjectsResponse
                        .data ?? []
                );

            }
            catch (err) {

                console.error(
                    "Failed to load initial data:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load student registration information."
                );

            }
            finally {

                setLoadingInitialData(false);
                setLoadingSubjects(false);

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

                setLoadingGrades(true);

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

                setLoadingGrades(false);

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

                setLoadingClasses(true);

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

                setLoadingClasses(false);

            }

        };


    // ============================================================
    // STUDENT FORM CHANGE
    // ============================================================

    const handleStudentChange =
        (event) => {

            const {
                name,
                value,
            } =
                event.target;

            setStudentForm(
                (current) => ({
                    ...current,

                    [name]:
                        value,
                })
            );

            setError("");

        };


    // ============================================================
    // PARENT FORM CHANGE
    // ============================================================

    const handleParentChange =
        (event) => {

            const {
                name,
                value,
                type,
                checked,
            } =
                event.target;

            setParentForm(
                (current) => ({
                    ...current,

                    [name]:
                        type === "checkbox"
                            ? checked
                            : value,
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

            setGradeId("");

            setGrades([]);
            setClasses([]);

            setStudentForm(
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

            setClasses([]);

            setStudentForm(
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
    // SUBJECT TOGGLE
    // ============================================================

    const toggleSubject =
        (subjectId) => {

            setSelectedSubjectIds(
                (current) => {

                    if (
                        current.includes(
                            subjectId
                        )
                    ) {

                        return current.filter(
                            (id) =>
                                id !== subjectId
                        );

                    }

                    return [
                        ...current,
                        subjectId,
                    ];

                }
            );

            setError("");

        };


    // ============================================================
    // SELECT ALL SUBJECTS
    // ============================================================

    const handleSelectAllSubjects =
        () => {

            if (
                selectedSubjectIds.length ===
                subjects.length
            ) {

                setSelectedSubjectIds([]);

                return;

            }

            setSelectedSubjectIds(
                subjects.map(
                    (subject) =>
                        subject.id
                )
            );

        };


    // ============================================================
    // SELECTED DISPLAY VALUES
    // ============================================================

    const selectedAcademicYear =
        useMemo(
            () =>
                academicYears.find(
                    (item) =>
                        String(item.id) ===
                        String(academicYearId)
                ),
            [
                academicYears,
                academicYearId,
            ]
        );

    const selectedSection =
        useMemo(
            () =>
                sections.find(
                    (item) =>
                        String(item.id) ===
                        String(sectionId)
                ),
            [
                sections,
                sectionId,
            ]
        );

    const selectedGrade =
        useMemo(
            () =>
                grades.find(
                    (item) =>
                        String(item.id) ===
                        String(gradeId)
                ),
            [
                grades,
                gradeId,
            ]
        );

    const selectedClass =
        useMemo(
            () =>
                classes.find(
                    (item) =>
                        String(item.id) ===
                        String(
                            studentForm
                                .schoolClassId
                        )
                ),
            [
                classes,
                studentForm
                    .schoolClassId,
            ]
        );

    const selectedSubjects =
        useMemo(
            () =>
                subjects.filter(
                    (subject) =>
                        selectedSubjectIds
                            .includes(
                                subject.id
                            )
                ),
            [
                subjects,
                selectedSubjectIds,
            ]
        );


    // ============================================================
    // VALIDATE CURRENT STEP
    // ============================================================

    const validateStep =
        () => {

            // STEP 1
            if (
                currentStep === 1
            ) {

                if (
                    !studentForm
                        .indexNumber
                        .trim()
                ) {
                    return "Index number is required.";
                }

                if (
                    !studentForm
                        .fullName
                        .trim()
                ) {
                    return "Full name is required.";
                }

                if (
                    !studentForm
                        .email
                        .trim()
                ) {
                    return "Student email is required.";
                }

                const emailPattern =
                    /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

                if (
                    !emailPattern.test(
                        studentForm
                            .email
                            .trim()
                    )
                ) {
                    return "Enter a valid student email address.";
                }

                return "";

            }


            // STEP 2
            if (
                currentStep === 2
            ) {

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
                    !studentForm
                        .schoolClassId
                ) {
                    return "Class is required.";
                }

                return "";

            }


            // STEP 3
            if (
                currentStep === 3
            ) {

                if (
                    selectedSubjectIds
                        .length === 0
                ) {
                    return "Select at least one subject.";
                }

                return "";

            }


            // STEP 4
            if (
                currentStep === 4
            ) {

                if (
                    !parentForm
                        .parentNumber
                        .trim()
                ) {
                    return "Parent number is required.";
                }

                if (
                    !parentForm
                        .fullName
                        .trim()
                ) {
                    return "Parent or guardian full name is required.";
                }

                if (
                    !parentForm
                        .relationship
                        .trim()
                ) {
                    return "Relationship is required.";
                }

                if (
                    parentForm.email.trim()
                ) {

                    const emailPattern =
                        /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

                    if (
                        !emailPattern.test(
                            parentForm
                                .email
                                .trim()
                        )
                    ) {
                        return "Enter a valid parent email address.";
                    }

                }

                return "";

            }

            return "";

        };


    // ============================================================
    // NEXT STEP
    // ============================================================

    const handleNext =
        () => {

            setError("");
            setSuccess("");

            const validationMessage =
                validateStep();

            if (
                validationMessage
            ) {

                setError(
                    validationMessage
                );

                return;

            }

            setCurrentStep(
                (current) =>
                    Math.min(
                        current + 1,
                        5
                    )
            );

            window.scrollTo({
                top: 0,
                behavior: "smooth",
            });

        };


    // ============================================================
    // PREVIOUS STEP
    // ============================================================

    const handlePrevious =
        () => {

            setError("");
            setSuccess("");

            setCurrentStep(
                (current) =>
                    Math.max(
                        current - 1,
                        1
                    )
            );

            window.scrollTo({
                top: 0,
                behavior: "smooth",
            });

        };


    // ============================================================
    // FINAL CREATE
    // ============================================================

    const handleCreateStudent =
        async () => {

            setError("");
            setSuccess("");

            try {

                setSubmitting(true);

                // ------------------------------------------------
                // 1. CREATE STUDENT
                // ------------------------------------------------

                const studentPayload = {

                    indexNumber:
                        studentForm
                            .indexNumber
                            .trim(),

                    fullName:
                        studentForm
                            .fullName
                            .trim(),

                    dateOfBirth:
                        studentForm
                            .dateOfBirth
                            ? studentForm
                                .dateOfBirth
                            : null,

                    email:
                        studentForm
                            .email
                            .trim()
                            .toLowerCase(),

                    mobile:
                        studentForm
                            .mobile
                            .trim() ||
                        null,

                    schoolClassId:
                        Number(
                            studentForm
                                .schoolClassId
                        ),

                    academicYearId:
                        Number(
                            academicYearId
                        ),
                };

                const studentResponse =
                    await studentsApi
                        .createStudent(
                            studentPayload
                        );

                const newStudentId =
                    studentResponse
                        .data
                        ?.id;

                if (
                    !newStudentId
                ) {

                    throw new Error(
                        "Student was created but the new student ID was not returned."
                    );

                }


                // ------------------------------------------------
                // 2. ASSIGN SUBJECTS
                // ------------------------------------------------

                for (
                    const subjectId
                    of selectedSubjectIds
                ) {

                    await studentEnrollmentsApi
                        .assignSubject({
                            studentId:
                                Number(
                                    newStudentId
                                ),

                            academicYearId:
                                Number(
                                    academicYearId
                                ),

                            subjectId:
                                Number(
                                    subjectId
                                ),
                        });

                }


                // ------------------------------------------------
                // 3. CREATE PARENT
                // ------------------------------------------------

                const parentResponse =
                    await parentGuardiansApi
                        .createParent({
                            parentNumber:
                                parentForm
                                    .parentNumber
                                    .trim(),

                            fullName:
                                parentForm
                                    .fullName
                                    .trim(),

                            email:
                                parentForm
                                    .email
                                    .trim()
                                    ? parentForm
                                        .email
                                        .trim()
                                        .toLowerCase()
                                    : null,

                            phoneNumber:
                                parentForm
                                    .phoneNumber
                                    .trim() ||
                                null,
                        });

                const parentId =
                    parentResponse
                        .data
                        ?.parent
                        ?.id;

                if (
                    !parentId
                ) {

                    throw new Error(
                        "Parent was created but the new parent ID was not returned."
                    );

                }


                // ------------------------------------------------
                // 4. LINK PARENT TO STUDENT
                // ------------------------------------------------

                await parentGuardiansApi
                    .linkStudent({
                        studentId:
                            Number(
                                newStudentId
                            ),

                        parentGuardianId:
                            Number(
                                parentId
                            ),

                        relationship:
                            parentForm
                                .relationship
                                .trim(),

                        isPrimaryGuardian:
                            parentForm
                                .isPrimaryGuardian,

                        isEmergencyContact:
                            parentForm
                                .isEmergencyContact,
                    });


                // ------------------------------------------------
                // COMPLETE
                // ------------------------------------------------

                setSuccess(
                    "Student registration completed successfully."
                );

                setTimeout(
                    () => {
                        navigate(
                            `/students/${newStudentId}`
                        );
                    },
                    900
                );

            }
            catch (err) {

                console.error(
                    "Failed to complete student registration:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    err?.message ||
                    "Unable to complete student registration."
                );

            }
            finally {

                setSubmitting(false);

            }

        };


    // ============================================================
    // RENDER
    // ============================================================

    return (

        <div className="mx-auto w-full max-w-[1600px] pb-6">

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
                    Complete the registration steps to create the student, assign academic placement and subjects, and link a parent or guardian.
                </p>

            </div>


            {/* ====================================================
                STEP NAVIGATION
            ==================================================== */}

            <div className="mt-8 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">

                <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-5">

                    {steps.map(
                        (
                            step,
                            index
                        ) => {

                            const Icon =
                                step.icon;

                            const completed =
                                step.id <
                                currentStep;

                            const active =
                                step.id ===
                                currentStep;

                            return (

                                <div
                                    key={
                                        step.id
                                    }
                                    className={`relative flex items-center gap-3 px-5 py-4 ${index !==
                                            steps.length - 1
                                            ? "xl:border-r xl:border-slate-100"
                                            : ""
                                        }`}
                                >

                                    <div
                                        className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${completed
                                                ? "bg-emerald-50 text-emerald-600"
                                                : active
                                                    ? "bg-blue-600 text-white"
                                                    : "bg-slate-100 text-slate-400"
                                            }`}
                                    >

                                        {completed ? (
                                            <Check className="h-5 w-5" />
                                        ) : (
                                            <Icon className="h-5 w-5" />
                                        )}

                                    </div>

                                    <div className="min-w-0">

                                        <p
                                            className={`text-xs font-semibold ${active
                                                    ? "text-blue-600"
                                                    : completed
                                                        ? "text-emerald-600"
                                                        : "text-slate-400"
                                                }`}
                                        >
                                            Step {step.id}
                                        </p>

                                        <p
                                            className={`truncate text-sm font-semibold ${active ||
                                                    completed
                                                    ? "text-slate-950"
                                                    : "text-slate-500"
                                                }`}
                                        >
                                            {step.title}
                                        </p>

                                    </div>

                                </div>

                            );

                        }
                    )}

                </div>

            </div>


            {/* ====================================================
                ERROR
            ==================================================== */}

            {error && (

                <div className="mt-6 rounded-xl border border-red-100 bg-red-50 px-5 py-4 text-sm font-medium text-red-700">
                    {error}
                </div>

            )}


            {/* ====================================================
                SUCCESS
            ==================================================== */}

            {success && (

                <div className="mt-6 flex items-center gap-3 rounded-xl border border-emerald-100 bg-emerald-50 px-5 py-4 text-sm font-medium text-emerald-700">

                    <CheckCircle2 className="h-5 w-5 shrink-0" />

                    {success}

                </div>

            )}


            {/* ====================================================
                STEP CONTENT
            ==================================================== */}

            <div className="mt-6">


                {/* =================================================
                    STEP 1
                ================================================= */}

                {currentStep === 1 && (

                    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                        <SectionHeader
                            icon={UserRound}
                            title="Student Information"
                            description="Enter the student's personal and contact information."
                        />

                        <div className="mt-7 grid gap-5 md:grid-cols-2">

                            <FormInput
                                label="Index Number"
                                required
                                name="indexNumber"
                                value={
                                    studentForm
                                        .indexNumber
                                }
                                onChange={
                                    handleStudentChange
                                }
                                placeholder="Example: ST2030009"
                            />

                            <FormInput
                                label="Full Name"
                                required
                                name="fullName"
                                value={
                                    studentForm
                                        .fullName
                                }
                                onChange={
                                    handleStudentChange
                                }
                                placeholder="Enter student's full name"
                            />

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
                                            studentForm
                                                .dateOfBirth
                                        }
                                        onChange={
                                            handleStudentChange
                                        }
                                        className="h-11 w-full rounded-xl border border-slate-200 bg-white pl-12 pr-4 text-sm text-slate-900 outline-none transition focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                    />

                                </div>

                                <p className="mt-2 text-xs text-slate-400">
                                    Optional
                                </p>

                            </div>

                            <FormInput
                                label="Email"
                                required
                                name="email"
                                type="email"
                                value={
                                    studentForm
                                        .email
                                }
                                onChange={
                                    handleStudentChange
                                }
                                placeholder="student@example.com"
                                icon={Mail}
                            />

                            <FormInput
                                label="Mobile"
                                name="mobile"
                                value={
                                    studentForm
                                        .mobile
                                }
                                onChange={
                                    handleStudentChange
                                }
                                placeholder="Enter mobile number"
                                icon={Phone}
                                helper="Optional"
                            />

                        </div>

                    </div>

                )}


                {/* =================================================
                    STEP 2
                ================================================= */}

                {currentStep === 2 && (

                    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                        <SectionHeader
                            icon={GraduationCap}
                            title="Academic Placement"
                            description="Assign the student to an academic year, section, grade and class."
                        />

                        <div className="mt-7 grid gap-5 md:grid-cols-2">

                            <SelectField
                                label="Academic Year"
                                required
                                value={
                                    academicYearId
                                }
                                onChange={
                                    handleAcademicYearChange
                                }
                                disabled={
                                    loadingInitialData
                                }
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
                                            {year.name}
                                        </option>
                                    )
                                )}

                            </SelectField>


                            <SelectField
                                label="Section"
                                required
                                value={
                                    sectionId
                                }
                                onChange={
                                    handleSectionChange
                                }
                                disabled={
                                    loadingInitialData
                                }
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
                                            {section.name}
                                        </option>
                                    )
                                )}

                            </SelectField>


                            <SelectField
                                label="Grade"
                                required
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
                                            {grade.name}
                                        </option>
                                    )
                                )}

                            </SelectField>


                            <SelectField
                                label="Class"
                                required
                                name="schoolClassId"
                                value={
                                    studentForm
                                        .schoolClassId
                                }
                                onChange={
                                    handleStudentChange
                                }
                                disabled={
                                    !gradeId ||
                                    loadingClasses
                                }
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
                                            {schoolClass.name}
                                        </option>
                                    )
                                )}

                            </SelectField>

                        </div>

                    </div>

                )}


                {/* =================================================
                    STEP 3
                ================================================= */}

                {currentStep === 3 && (

                    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">

                            <SectionHeader
                                icon={BookOpen}
                                title="Subjects"
                                description="Select the subjects the student will study."
                            />

                            <button
                                type="button"
                                onClick={
                                    handleSelectAllSubjects
                                }
                                disabled={
                                    loadingSubjects ||
                                    subjects.length === 0
                                }
                                className="inline-flex h-10 items-center justify-center rounded-xl border border-slate-200 bg-white px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                            >
                                {selectedSubjectIds.length ===
                                    subjects.length &&
                                    subjects.length > 0
                                    ? "Clear All"
                                    : "Select All"}
                            </button>

                        </div>

                        {loadingSubjects ? (

                            <div className="mt-8 text-sm text-slate-500">
                                Loading subjects...
                            </div>

                        ) : subjects.length === 0 ? (

                            <div className="mt-8 rounded-xl border border-slate-200 bg-slate-50 px-5 py-8 text-center text-sm text-slate-500">
                                No active subjects are available.
                            </div>

                        ) : (

                            <div className="mt-7 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">

                                {subjects.map(
                                    (subject) => {

                                        const selected =
                                            selectedSubjectIds
                                                .includes(
                                                    subject.id
                                                );

                                        return (

                                            <button
                                                key={
                                                    subject.id
                                                }
                                                type="button"
                                                onClick={() =>
                                                    toggleSubject(
                                                        subject.id
                                                    )
                                                }
                                                className={`flex min-h-[76px] items-center gap-4 rounded-xl border px-4 py-4 text-left transition ${selected
                                                        ? "border-blue-300 bg-blue-50 ring-2 ring-blue-500/10"
                                                        : "border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50"
                                                    }`}
                                            >

                                                <div
                                                    className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${selected
                                                            ? "bg-blue-600 text-white"
                                                            : "bg-slate-100 text-slate-500"
                                                        }`}
                                                >

                                                    {selected ? (
                                                        <Check className="h-4 w-4" />
                                                    ) : (
                                                        <BookOpen className="h-4 w-4" />
                                                    )}

                                                </div>

                                                <div className="min-w-0">

                                                    <p className="font-semibold text-slate-900">
                                                        {subject.name}
                                                    </p>

                                                    {subject.code && (

                                                        <p className="mt-1 text-xs text-slate-500">
                                                            {subject.code}
                                                        </p>

                                                    )}

                                                </div>

                                            </button>

                                        );

                                    }
                                )}

                            </div>

                        )}

                    </div>

                )}


                {/* =================================================
                    STEP 4
                ================================================= */}

                {currentStep === 4 && (

                    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                        <SectionHeader
                            icon={UsersRound}
                            title="Parent / Guardian"
                            description="Enter the parent or guardian who should be linked to this student."
                        />

                        <div className="mt-7 grid gap-5 md:grid-cols-2">

                            <FormInput
                                label="Parent Number"
                                required
                                name="parentNumber"
                                value={
                                    parentForm
                                        .parentNumber
                                }
                                onChange={
                                    handleParentChange
                                }
                                placeholder="Example: P2030001"
                            />

                            <FormInput
                                label="Full Name"
                                required
                                name="fullName"
                                value={
                                    parentForm
                                        .fullName
                                }
                                onChange={
                                    handleParentChange
                                }
                                placeholder="Enter parent or guardian name"
                            />

                            <FormInput
                                label="Email"
                                name="email"
                                type="email"
                                value={
                                    parentForm
                                        .email
                                }
                                onChange={
                                    handleParentChange
                                }
                                placeholder="parent@example.com"
                                icon={Mail}
                                helper="Optional"
                            />

                            <FormInput
                                label="Phone Number"
                                name="phoneNumber"
                                value={
                                    parentForm
                                        .phoneNumber
                                }
                                onChange={
                                    handleParentChange
                                }
                                placeholder="Enter phone number"
                                icon={Phone}
                                helper="Optional"
                            />

                            <SelectField
                                label="Relationship"
                                required
                                name="relationship"
                                value={
                                    parentForm
                                        .relationship
                                }
                                onChange={
                                    handleParentChange
                                }
                            >
                                <option value="">
                                    Select Relationship
                                </option>

                                <option value="Father">
                                    Father
                                </option>

                                <option value="Mother">
                                    Mother
                                </option>

                                <option value="Guardian">
                                    Guardian
                                </option>

                                <option value="Brother">
                                    Brother
                                </option>

                                <option value="Sister">
                                    Sister
                                </option>

                                <option value="Other">
                                    Other
                                </option>

                            </SelectField>

                        </div>


                        <div className="mt-6 grid gap-4 sm:grid-cols-2">

                            <CheckboxCard
                                name="isPrimaryGuardian"
                                checked={
                                    parentForm
                                        .isPrimaryGuardian
                                }
                                onChange={
                                    handleParentChange
                                }
                                title="Primary Guardian"
                                description="This person will be the student's main guardian."
                            />

                            <CheckboxCard
                                name="isEmergencyContact"
                                checked={
                                    parentForm
                                        .isEmergencyContact
                                }
                                onChange={
                                    handleParentChange
                                }
                                title="Emergency Contact"
                                description="This person may be contacted in an emergency."
                            />

                        </div>

                    </div>

                )}


                {/* =================================================
                    STEP 5
                ================================================= */}

                {currentStep === 5 && (

                    <div className="space-y-6">

                        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                            <SectionHeader
                                icon={ClipboardCheck}
                                title="Review & Create"
                                description="Review the information before creating the student record."
                            />

                        </div>


                        <div className="grid gap-6 xl:grid-cols-2">

                            <ReviewCard
                                title="Student Information"
                                onEdit={() =>
                                    setCurrentStep(
                                        1
                                    )
                                }
                                rows={[
                                    [
                                        "Index Number",
                                        studentForm
                                            .indexNumber,
                                    ],
                                    [
                                        "Full Name",
                                        studentForm
                                            .fullName,
                                    ],
                                    [
                                        "Date of Birth",
                                        studentForm
                                            .dateOfBirth ||
                                        "Not provided",
                                    ],
                                    [
                                        "Email",
                                        studentForm
                                            .email,
                                    ],
                                    [
                                        "Mobile",
                                        studentForm
                                            .mobile ||
                                        "Not provided",
                                    ],
                                ]}
                            />


                            <ReviewCard
                                title="Academic Placement"
                                onEdit={() =>
                                    setCurrentStep(
                                        2
                                    )
                                }
                                rows={[
                                    [
                                        "Academic Year",
                                        selectedAcademicYear
                                            ?.name ||
                                        "-",
                                    ],
                                    [
                                        "Section",
                                        selectedSection
                                            ?.name ||
                                        "-",
                                    ],
                                    [
                                        "Grade",
                                        selectedGrade
                                            ?.name ||
                                        "-",
                                    ],
                                    [
                                        "Class",
                                        selectedClass
                                            ?.name ||
                                        "-",
                                    ],
                                ]}
                            />


                            <ReviewCard
                                title="Parent / Guardian"
                                onEdit={() =>
                                    setCurrentStep(
                                        4
                                    )
                                }
                                rows={[
                                    [
                                        "Parent Number",
                                        parentForm
                                            .parentNumber,
                                    ],
                                    [
                                        "Full Name",
                                        parentForm
                                            .fullName,
                                    ],
                                    [
                                        "Relationship",
                                        parentForm
                                            .relationship,
                                    ],
                                    [
                                        "Email",
                                        parentForm
                                            .email ||
                                        "Not provided",
                                    ],
                                    [
                                        "Phone",
                                        parentForm
                                            .phoneNumber ||
                                        "Not provided",
                                    ],
                                    [
                                        "Primary Guardian",
                                        parentForm
                                            .isPrimaryGuardian
                                            ? "Yes"
                                            : "No",
                                    ],
                                    [
                                        "Emergency Contact",
                                        parentForm
                                            .isEmergencyContact
                                            ? "Yes"
                                            : "No",
                                    ],
                                ]}
                            />


                            <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                                <div className="flex items-center justify-between gap-4">

                                    <h3 className="font-semibold text-slate-950">
                                        Subjects
                                    </h3>

                                    <button
                                        type="button"
                                        onClick={() =>
                                            setCurrentStep(
                                                3
                                            )
                                        }
                                        className="text-sm font-semibold text-blue-600 transition hover:text-blue-700"
                                    >
                                        Edit
                                    </button>

                                </div>

                                <div className="mt-5 flex flex-wrap gap-2">

                                    {selectedSubjects.map(
                                        (
                                            subject
                                        ) => (

                                            <span
                                                key={
                                                    subject.id
                                                }
                                                className="rounded-lg bg-blue-50 px-3 py-2 text-sm font-medium text-blue-700"
                                            >
                                                {subject.name}
                                            </span>

                                        )
                                    )}

                                </div>

                            </div>

                        </div>

                    </div>

                )}

            </div>


            {/* ====================================================
                ACTIONS
            ==================================================== */}

            <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:items-center sm:justify-between">

                <div>

                    {currentStep > 1 && (

                        <button
                            type="button"
                            onClick={
                                handlePrevious
                            }
                            disabled={
                                submitting
                            }
                            className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            <ArrowLeft className="h-4 w-4" />

                            Previous
                        </button>

                    )}

                </div>


                <div className="flex flex-col-reverse gap-3 sm:flex-row">

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


                    {currentStep < 5 ? (

                        <button
                            type="button"
                            onClick={
                                handleNext
                            }
                            disabled={
                                submitting ||
                                loadingInitialData
                            }
                            className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                            Next

                            <ArrowRight className="h-4 w-4" />
                        </button>

                    ) : (

                        <button
                            type="button"
                            onClick={
                                handleCreateStudent
                            }
                            disabled={
                                submitting
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

                    )}

                </div>

            </div>

        </div>

    );

}


// ============================================================
// SECTION HEADER
// ============================================================

function SectionHeader({
    icon: Icon,
    title,
    description,
}) {

    return (

        <div className="flex items-center gap-3">

            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                <Icon className="h-5 w-5" />
            </div>

            <div>

                <h2 className="font-semibold text-slate-950">
                    {title}
                </h2>

                <p className="mt-0.5 text-xs text-slate-500">
                    {description}
                </p>

            </div>

        </div>

    );

}


// ============================================================
// FORM INPUT
// ============================================================

function FormInput({
    label,
    required = false,
    helper = "",
    icon: Icon,
    ...props
}) {

    return (

        <div>

            <label className="mb-2 block text-sm font-semibold text-slate-700">

                {label}

                {required && (
                    <span className="ml-1 text-red-500">
                        *
                    </span>
                )}

            </label>


            <div className="relative">

                {Icon && (

                    <Icon className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />

                )}

                <input
                    {...props}
                    className={`h-11 w-full rounded-xl border border-slate-200 bg-white pr-4 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10 ${Icon
                            ? "pl-11"
                            : "pl-4"
                        }`}
                />

            </div>


            {helper && (

                <p className="mt-2 text-xs text-slate-400">
                    {helper}
                </p>

            )}

        </div>

    );

}


// ============================================================
// SELECT FIELD
// ============================================================

function SelectField({
    label,
    required = false,
    children,
    ...props
}) {

    return (

        <div>

            <label className="mb-2 block text-sm font-semibold text-slate-700">

                {label}

                {required && (
                    <span className="ml-1 text-red-500">
                        *
                    </span>
                )}

            </label>

            <select
                {...props}
                className="h-11 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-700 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
            >
                {children}
            </select>

        </div>

    );

}


// ============================================================
// CHECKBOX CARD
// ============================================================

function CheckboxCard({
    title,
    description,
    ...props
}) {

    return (

        <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-slate-200 bg-slate-50 p-4 transition hover:bg-slate-100">

            <input
                type="checkbox"
                {...props}
                className="mt-1 h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
            />

            <div>

                <p className="text-sm font-semibold text-slate-900">
                    {title}
                </p>

                <p className="mt-1 text-xs leading-5 text-slate-500">
                    {description}
                </p>

            </div>

        </label>

    );

}


// ============================================================
// REVIEW CARD
// ============================================================

function ReviewCard({
    title,
    rows,
    onEdit,
}) {

    return (

        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

            <div className="flex items-center justify-between gap-4">

                <h3 className="font-semibold text-slate-950">
                    {title}
                </h3>

                <button
                    type="button"
                    onClick={
                        onEdit
                    }
                    className="text-sm font-semibold text-blue-600 transition hover:text-blue-700"
                >
                    Edit
                </button>

            </div>


            <div className="mt-5 divide-y divide-slate-100">

                {rows.map(
                    (
                        [
                            label,
                            value,
                        ]
                    ) => (

                        <div
                            key={
                                label
                            }
                            className="flex flex-col gap-1 py-3 first:pt-0 sm:flex-row sm:items-start sm:justify-between sm:gap-6"
                        >

                            <span className="text-sm text-slate-500">
                                {label}
                            </span>

                            <span className="text-sm font-semibold text-slate-900 sm:text-right">
                                {value}
                            </span>

                        </div>

                    )
                )}

            </div>

        </div>

    );

}