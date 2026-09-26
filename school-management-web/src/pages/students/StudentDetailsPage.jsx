import {
    ArrowLeft,
    BookOpen,
    CalendarDays,
    CheckCircle2,
    Copy,
    GraduationCap,
    KeyRound,
    Mail,
    Phone,
    ShieldCheck,
    UserRound,
    Users,
    X,
} from "lucide-react";

import {
    useEffect,
    useState,
} from "react";

import {
    useNavigate,
    useParams,
} from "react-router-dom";

import { studentsApi } from "../../api/studentsApi";
import {
    studentRegistrationApi,
} from "../../api/studentRegistrationApi";

export default function StudentDetailsPage() {
    const {
        id,
    } = useParams();

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

    const [
        generatingCode,
        setGeneratingCode,
    ] = useState(false);

    const [
        registrationData,
        setRegistrationData,
    ] = useState(null);

    const [
        registrationError,
        setRegistrationError,
    ] = useState("");

    const [
        copied,
        setCopied,
    ] = useState(false);

    useEffect(() => {
        loadStudentDetails();
    }, [id]);

    const loadStudentDetails =
        async () => {
            try {
                setLoading(true);
                setError("");

                const response =
                    await studentsApi
                        .getStudentDetails(
                            id
                        );

                setData(
                    response.data
                );
            }
            catch (err) {
                console.error(
                    "Failed to load student details:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load student details."
                );
            }
            finally {
                setLoading(false);
            }
        };

    const formatDate =
        (value) => {
            if (!value) {
                return "—";
            }

            return new Date(
                value
            ).toLocaleDateString();
        };

    if (loading) {
        return (
            <div className="mx-auto w-full max-w-[1500px]">

                <div className="flex min-h-[420px] items-center justify-center">

                    <div className="text-center">

                        <div className="mx-auto h-10 w-10 animate-spin rounded-full border-2 border-slate-200 border-t-blue-600" />

                        <p className="mt-4 text-sm text-slate-500">
                            Loading student details...
                        </p>

                    </div>

                </div>

            </div>
        );
    }

    if (error) {
        return (
            <div className="mx-auto w-full max-w-[1500px]">

                <button
                    type="button"
                    onClick={() =>
                        navigate(
                            "/students"
                        )
                    }
                    className="mb-6 inline-flex items-center gap-2 text-sm font-semibold text-slate-600 transition hover:text-blue-600"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Students
                </button>

                <div className="rounded-2xl border border-red-100 bg-red-50 p-6 text-sm text-red-700">
                    {error}
                </div>

            </div>
        );
    }

    const student =
        data?.student;

    const currentEnrollment =
        data?.currentEnrollment;

    const enrollmentHistory =
        data?.enrollmentHistory ??
        [];

    const subjects =
        data?.subjects ??
        [];

    const guardians =
        data?.guardians ??
        [];

    const handleGenerateRegistrationCode =
        async () => {

            try {

                setGeneratingCode(true);
                setRegistrationError("");
                setRegistrationData(null);

                const response =
                    await studentRegistrationApi
                        .generateCode({
                            indexNumber:
                                student.indexNumber,
                        });

                setRegistrationData(
                    response.data
                );

            }
            catch (err) {

                console.error(
                    "Failed to generate registration code:",
                    err
                );

                setRegistrationError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to generate registration code."
                );

            }
            finally {

                setGeneratingCode(false);

            }

        };


    const handleCopyCode =
        async () => {

            const code =
                registrationData
                    ?.registrationCode;

            if (!code) {
                return;
            }

            await navigator.clipboard
                .writeText(code);

            setCopied(true);

            setTimeout(
                () => {
                    setCopied(false);
                },
                1500
            );

        };

    return (
        <div className="mx-auto w-full max-w-[1500px]">

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
                PROFILE HEADER
            ==================================================== */}

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">

                    <div className="flex items-start gap-4">

                        <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-blue-50 text-xl font-bold text-blue-700">

                            {student?.fullName
                                ?.charAt(0)
                                ?.toUpperCase() ||
                                "S"}

                        </div>

                        <div>

                            <div className="flex flex-wrap items-center gap-3">

                                <h1 className="text-2xl font-bold text-slate-950">
                                    {
                                        student?.fullName
                                    }
                                </h1>

                                <span
                                    className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${student?.isActive
                                        ? "bg-emerald-50 text-emerald-700"
                                        : "bg-slate-100 text-slate-600"
                                        }`}
                                >
                                    {student?.isActive
                                        ? "Active"
                                        : "Inactive"}
                                </span>

                                {student?.isGraduated && (
                                    <span className="inline-flex rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
                                        Graduated
                                    </span>
                                )}

                            </div>

                            <p className="mt-2 text-sm text-slate-500">
                                Index Number:
                                <span className="ml-2 font-semibold text-slate-700">
                                    {
                                        student?.indexNumber
                                    }
                                </span>
                            </p>

                        </div>

                    </div>

                    <div className="flex shrink-0">

                        <button
                            type="button"
                            onClick={
                                handleGenerateRegistrationCode
                            }
                            disabled={
                                generatingCode
                            }
                            className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                        >

                            {generatingCode ? (
                                <>
                                    <div className="h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />
                                    Generating...
                                </>
                            ) : (
                                <>
                                    <KeyRound className="h-4 w-4" />
                                    Generate Registration Code
                                </>
                            )}

                        </button>

                    </div>

                </div>

            </div>

            {registrationError && (

                <div className="mt-6 rounded-xl border border-red-100 bg-red-50 px-5 py-4 text-sm font-medium text-red-700">
                    {registrationError}
                </div>

            )}

            {/* ====================================================
                BASIC + CURRENT ACADEMIC
            ==================================================== */}

            <div className="mt-6 grid gap-6 xl:grid-cols-2">

                {/* BASIC INFORMATION */}

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
                                Basic student details
                            </p>
                        </div>

                    </div>

                    <div className="mt-6 grid gap-5 sm:grid-cols-2">

                        <InfoItem
                            label="Full Name"
                            value={
                                student?.fullName
                            }
                        />

                        <InfoItem
                            label="Index Number"
                            value={
                                student?.indexNumber
                            }
                        />

                        <InfoItem
                            label="Date of Birth"
                            value={formatDate(
                                student?.dateOfBirth
                            )}
                        />

                        <InfoItem
                            label="Student Status"
                            value={
                                student?.isActive
                                    ? "Active"
                                    : "Inactive"
                            }
                        />

                        <InfoItem
                            label="Graduation Status"
                            value={
                                student?.isGraduated
                                    ? "Graduated"
                                    : "Not Graduated"
                            }
                        />

                        <InfoItem
                            label="Graduation Date"
                            value={formatDate(
                                student?.graduationDate
                            )}
                        />

                        {student?.isGraduated && (
                            <InfoItem
                                label="Graduation Academic Year"
                                value={
                                    student
                                        ?.graduationAcademicYear ||
                                    "—"
                                }
                            />
                        )}

                    </div>

                </div>

                {/* CURRENT ACADEMIC */}

                <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">

                    <div className="flex items-center gap-3">

                        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                            <GraduationCap className="h-5 w-5" />
                        </div>

                        <div>
                            <h2 className="font-semibold text-slate-950">
                                Current Academic Enrollment
                            </h2>

                            <p className="text-xs text-slate-500">
                                Current academic placement
                            </p>
                        </div>

                    </div>

                    {currentEnrollment ? (
                        <div className="mt-6 grid gap-5 sm:grid-cols-2">

                            <InfoItem
                                label="Academic Year"
                                value={
                                    currentEnrollment
                                        .academicYear
                                }
                            />

                            <InfoItem
                                label="Section"
                                value={
                                    currentEnrollment
                                        .section
                                }
                            />

                            <InfoItem
                                label="Grade"
                                value={
                                    currentEnrollment
                                        .grade
                                }
                            />

                            <InfoItem
                                label="Class"
                                value={
                                    currentEnrollment
                                        .class
                                }
                            />

                            <InfoItem
                                label="Enrollment Date"
                                value={formatDate(
                                    currentEnrollment
                                        .enrollmentDate
                                )}
                            />

                            <InfoItem
                                label="Created By"
                                value={
                                    currentEnrollment
                                        .createdByStaff ||
                                    "—"
                                }
                            />

                        </div>
                    ) : (
                        <EmptyState
                            text="No current academic enrollment found."
                        />
                    )}

                </div>

            </div>

            {/* ====================================================
                SUBJECTS
            ==================================================== */}

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white shadow-sm">

                <SectionHeader
                    icon={BookOpen}
                    title="Subjects"
                    subtitle="Current subject enrollments"
                />

                {subjects.length > 0 ? (
                    <div className="overflow-x-auto">

                        <table className="w-full min-w-[700px]">

                            <thead className="bg-slate-50">

                                <tr className="border-y border-slate-200">

                                    <TableHead>
                                        Subject
                                    </TableHead>

                                    <TableHead>
                                        Code
                                    </TableHead>

                                    <TableHead>
                                        Academic Year
                                    </TableHead>

                                    <TableHead>
                                        Enrolled At
                                    </TableHead>

                                    <TableHead>
                                        Enrolled By
                                    </TableHead>

                                </tr>

                            </thead>

                            <tbody>

                                {subjects.map(
                                    (subject) => (

                                        <tr
                                            key={
                                                subject.id
                                            }
                                            className="border-b border-slate-100 last:border-b-0"
                                        >

                                            <TableCell strong>
                                                {
                                                    subject.subject
                                                }
                                            </TableCell>

                                            <TableCell>
                                                {
                                                    subject.subjectCode ||
                                                    "—"
                                                }
                                            </TableCell>

                                            <TableCell>
                                                {
                                                    subject.academicYear
                                                }
                                            </TableCell>

                                            <TableCell>
                                                {formatDate(
                                                    subject.enrolledAt
                                                )}
                                            </TableCell>

                                            <TableCell>
                                                {
                                                    subject.enrolledByStaff ||
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
                    <EmptyState
                        text="No active subjects found."
                    />
                )}

            </div>

            {/* ====================================================
                GUARDIANS
            ==================================================== */}

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white shadow-sm">

                <SectionHeader
                    icon={Users}
                    title="Parents & Guardians"
                    subtitle="Linked parent and guardian information"
                />

                {guardians.length > 0 ? (
                    <div className="grid gap-4 p-6 md:grid-cols-2">

                        {guardians.map(
                            (guardian) => (

                                <div
                                    key={
                                        guardian.relationshipId
                                    }
                                    className="rounded-xl border border-slate-200 p-5"
                                >

                                    <div className="flex items-start justify-between gap-4">

                                        <div>

                                            <p className="font-semibold text-slate-900">
                                                {
                                                    guardian.fullName
                                                }
                                            </p>

                                            <p className="mt-1 text-sm text-slate-500">
                                                {
                                                    guardian.relationship
                                                }
                                            </p>

                                        </div>

                                        <div className="flex flex-wrap justify-end gap-2">

                                            {guardian.isPrimaryGuardian && (
                                                <span className="rounded-full bg-blue-50 px-2.5 py-1 text-[11px] font-semibold text-blue-700">
                                                    Primary
                                                </span>
                                            )}

                                            {guardian.isEmergencyContact && (
                                                <span className="rounded-full bg-amber-50 px-2.5 py-1 text-[11px] font-semibold text-amber-700">
                                                    Emergency
                                                </span>
                                            )}

                                        </div>

                                    </div>

                                    <div className="mt-5 space-y-3">

                                        <div className="flex items-center gap-3 text-sm text-slate-600">

                                            <ShieldCheck className="h-4 w-4 text-slate-400" />

                                            <span>
                                                {
                                                    guardian.parentNumber
                                                }
                                            </span>

                                        </div>

                                        <div className="flex items-center gap-3 text-sm text-slate-600">

                                            <Mail className="h-4 w-4 text-slate-400" />

                                            <span>
                                                {
                                                    guardian.email ||
                                                    "—"
                                                }
                                            </span>

                                        </div>

                                        <div className="flex items-center gap-3 text-sm text-slate-600">

                                            <Phone className="h-4 w-4 text-slate-400" />

                                            <span>
                                                {
                                                    guardian.phoneNumber ||
                                                    "—"
                                                }
                                            </span>

                                        </div>

                                    </div>

                                </div>

                            )
                        )}

                    </div>
                ) : (
                    <EmptyState
                        text="No parent or guardian linked."
                    />
                )}

            </div>

            {/* ====================================================
                ENROLLMENT HISTORY
            ==================================================== */}

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white shadow-sm">

                <SectionHeader
                    icon={CalendarDays}
                    title="Academic History"
                    subtitle="Student enrollment history"
                />

                {enrollmentHistory.length > 0 ? (
                    <div className="overflow-x-auto">

                        <table className="w-full min-w-[800px]">

                            <thead className="bg-slate-50">

                                <tr className="border-y border-slate-200">

                                    <TableHead>
                                        Academic Year
                                    </TableHead>

                                    <TableHead>
                                        Section
                                    </TableHead>

                                    <TableHead>
                                        Grade
                                    </TableHead>

                                    <TableHead>
                                        Class
                                    </TableHead>

                                    <TableHead>
                                        Enrollment Date
                                    </TableHead>

                                    <TableHead>
                                        Status
                                    </TableHead>

                                </tr>

                            </thead>

                            <tbody>

                                {enrollmentHistory.map(
                                    (item) => (

                                        <tr
                                            key={
                                                item.id
                                            }
                                            className="border-b border-slate-100 last:border-b-0"
                                        >

                                            <TableCell strong>
                                                {
                                                    item.academicYear
                                                }
                                            </TableCell>

                                            <TableCell>
                                                {
                                                    item.section
                                                }
                                            </TableCell>

                                            <TableCell>
                                                {
                                                    item.grade
                                                }
                                            </TableCell>

                                            <TableCell>
                                                {
                                                    item.class
                                                }
                                            </TableCell>

                                            <TableCell>
                                                {formatDate(
                                                    item.enrollmentDate
                                                )}
                                            </TableCell>

                                            <TableCell>

                                                <span
                                                    className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${item.isCurrent
                                                        ? "bg-blue-50 text-blue-700"
                                                        : "bg-slate-100 text-slate-600"
                                                        }`}
                                                >
                                                    {item.isCurrent
                                                        ? "Current"
                                                        : "Previous"}
                                                </span>

                                            </TableCell>

                                        </tr>

                                    )
                                )}

                            </tbody>

                        </table>

                    </div>
                ) : (
                    <EmptyState
                        text="No academic history found."
                    />
                )}

            </div>

            {registrationData && (

                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4">

                    <div className="w-full max-w-md rounded-2xl bg-white shadow-2xl">

                        <div className="flex items-center justify-between border-b border-slate-100 px-6 py-5">

                            <div>

                                <h2 className="text-lg font-bold text-slate-950">
                                    Registration Code
                                </h2>

                                <p className="mt-1 text-xs text-slate-500">
                                    Give this code securely to the student.
                                </p>

                            </div>

                            <button
                                type="button"
                                onClick={() =>
                                    setRegistrationData(
                                        null
                                    )
                                }
                                className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
                            >
                                <X className="h-5 w-5" />
                            </button>

                        </div>

                        <div className="p-6">

                            <div className="rounded-xl border border-blue-100 bg-blue-50 p-5 text-center">

                                <p className="text-xs font-semibold uppercase tracking-wider text-blue-600">
                                    Registration Code
                                </p>

                                <div className="mt-3 flex items-center justify-center gap-3">

                                    <span className="text-3xl font-bold tracking-[0.2em] text-slate-950">
                                        {
                                            registrationData
                                                .registrationCode
                                        }
                                    </span>

                                    <button
                                        type="button"
                                        onClick={
                                            handleCopyCode
                                        }
                                        className="flex h-9 w-9 items-center justify-center rounded-lg bg-white text-blue-600 shadow-sm transition hover:bg-blue-100"
                                        title="Copy code"
                                    >
                                        {copied ? (
                                            <CheckCircle2 className="h-5 w-5" />
                                        ) : (
                                            <Copy className="h-5 w-5" />
                                        )}
                                    </button>

                                </div>

                            </div>

                            <div className="mt-6 space-y-4">

                                <ModalInfoRow
                                    label="Student"
                                    value={
                                        registrationData
                                            ?.student
                                            ?.fullName
                                    }
                                />

                                <ModalInfoRow
                                    label="Index Number"
                                    value={
                                        registrationData
                                            ?.student
                                            ?.indexNumber
                                    }
                                />

                                <ModalInfoRow
                                    label="Registered Email"
                                    value={
                                        registrationData
                                            ?.student
                                            ?.maskedEmail
                                    }
                                />

                                <ModalInfoRow
                                    label="Expires In"
                                    value={`${registrationData?.expiresInMinutes ?? 30} minutes`}
                                />

                                <ModalInfoRow
                                    label="Maximum Attempts"
                                    value={
                                        registrationData
                                            ?.maxAttempts
                                    }
                                />

                            </div>

                            <div className="mt-6 rounded-xl border border-amber-100 bg-amber-50 px-4 py-3 text-xs leading-5 text-amber-700">
                                This code is temporary. If a new code is generated, the previous unused code becomes invalid.
                            </div>

                            <button
                                type="button"
                                onClick={() =>
                                    setRegistrationData(
                                        null
                                    )
                                }
                                className="mt-6 inline-flex h-11 w-full items-center justify-center rounded-xl bg-slate-900 px-5 text-sm font-semibold text-white transition hover:bg-slate-800"
                            >
                                Done
                            </button>

                        </div>

                    </div>

                </div>

            )}

        </div>
    );
}

// ============================================================
// INFO ITEM
// ============================================================

function InfoItem({
    label,
    value,
}) {
    return (
        <div>

            <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
                {label}
            </p>

            <p className="mt-1.5 text-sm font-semibold text-slate-800">
                {value || "—"}
            </p>

        </div>
    );
}

// ============================================================
// SECTION HEADER
// ============================================================

function SectionHeader({
    icon: Icon,
    title,
    subtitle,
}) {
    return (
        <div className="flex items-center gap-3 p-6">

            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                <Icon className="h-5 w-5" />
            </div>

            <div>

                <h2 className="font-semibold text-slate-950">
                    {title}
                </h2>

                <p className="text-xs text-slate-500">
                    {subtitle}
                </p>

            </div>

        </div>
    );
}

// ============================================================
// EMPTY STATE
// ============================================================

function EmptyState({
    text,
}) {
    return (
        <div className="px-6 py-12 text-center text-sm text-slate-400">
            {text}
        </div>
    );
}

// ============================================================
// MODAL INFO ROW
// ============================================================

function ModalInfoRow({
    label,
    value,
}) {
    return (
        <div className="flex items-center justify-between gap-6 border-b border-slate-100 pb-3 last:border-b-0">

            <span className="text-sm text-slate-500">
                {label}
            </span>

            <span className="text-right text-sm font-semibold text-slate-900">
                {value || "—"}
            </span>

        </div>
    );
}

// ============================================================
// TABLE HELPERS
// ============================================================

function TableHead({
    children,
}) {
    return (
        <th className="px-6 py-4 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            {children}
        </th>
    );
}

function TableCell({
    children,
    strong = false,
}) {
    return (
        <td
            className={`px-6 py-4 text-sm ${strong
                ? "font-semibold text-slate-800"
                : "text-slate-600"
                }`}
        >
            {children}
        </td>
    );
}