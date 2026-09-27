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
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { studentsApi } from "../../api/studentsApi";
import { studentRegistrationApi } from "../../api/studentRegistrationApi";
import { useAuth } from "../../context/AuthContext";

export default function StudentDetailsPage() {
    const { id } = useParams();
    const navigate = useNavigate();
    const { hasRole } = useAuth();

    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [generatingCode, setGeneratingCode] = useState(false);
    const [registrationData, setRegistrationData] = useState(null);
    const [registrationError, setRegistrationError] = useState("");
    const [copied, setCopied] = useState(false);

    useEffect(() => {
        let active = true;

        studentsApi
            .getStudentDetails(id)
            .then((response) => {
                if (active) setData(response.data);
            })
            .catch((err) => {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load student details."
                    );
                }
            })
            .finally(() => {
                if (active) setLoading(false);
            });

        return () => {
            active = false;
        };
    }, [id]);

    const student = data?.student;
    const currentEnrollment = data?.currentEnrollment;
    const enrollmentHistory = data?.enrollmentHistory ?? [];
    const subjects = data?.subjects ?? [];
    const guardians = data?.guardians ?? [];

    const formatDate = (value) =>
        value ? new Date(value).toLocaleDateString() : "—";

    async function handleGenerateRegistrationCode() {
        if (!student) return;

        setGeneratingCode(true);
        setRegistrationError("");
        setRegistrationData(null);

        try {
            const response = await studentRegistrationApi.generateCode({
                indexNumber: student.indexNumber,
            });
            setRegistrationData(response.data);
        } catch (err) {
            setRegistrationError(
                err?.response?.data?.message ||
                "Unable to generate registration code."
            );
        } finally {
            setGeneratingCode(false);
        }
    }

    async function handleCopyCode() {
        if (!registrationData?.registrationCode) return;

        try {
            await navigator.clipboard.writeText(
                registrationData.registrationCode
            );
            setCopied(true);
            setTimeout(() => setCopied(false), 1500);
        } catch {
            setRegistrationError(
                "Unable to copy the code. Select it and copy manually."
            );
        }
    }

    if (loading) {
        return (
            <div className="py-20 text-center text-sm text-slate-500">
                Loading student details...
            </div>
        );
    }

    if (error || !student) {
        return (
            <div className="mx-auto w-full max-w-[1500px]">
                <button
                    type="button"
                    onClick={() => navigate("/students")}
                    className="mb-6 inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Students
                </button>
                <div className="rounded-2xl border border-red-100 bg-red-50 p-6 text-sm text-red-700">
                    {error || "Student not found."}
                </div>
            </div>
        );
    }

    return (
        <div className="mx-auto w-full max-w-[1500px]">
            <button
                type="button"
                onClick={() => navigate("/students")}
                className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600 hover:text-blue-600"
            >
                <ArrowLeft className="h-4 w-4" />
                Back to Students
            </button>

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
                    <div className="flex items-start gap-4">
                        <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-blue-50 text-xl font-bold text-blue-700">
                            {student.fullName?.charAt(0)?.toUpperCase() || "S"}
                        </div>
                        <div>
                            <div className="flex flex-wrap items-center gap-3">
                                <h1 className="text-2xl font-bold text-slate-950">
                                    {student.fullName}
                                </h1>
                                <span
                                    className={
                                        student.isActive
                                            ? "rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700"
                                            : "rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-600"
                                    }
                                >
                                    {student.isActive ? "Active" : "Inactive"}
                                </span>
                                {student.isGraduated && (
                                    <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
                                        Graduated
                                    </span>
                                )}
                            </div>
                            <p className="mt-2 text-sm text-slate-500">
                                Index Number:{" "}
                                <span className="font-semibold text-slate-700">
                                    {student.indexNumber}
                                </span>
                            </p>
                        </div>
                    </div>

                    <div className="flex shrink-0 flex-wrap gap-3">
                        {hasRole("Admin") && (
                            <button
                                type="button"
                                onClick={() =>
                                    navigate(
                                        "/students/" + id + "/reset-password"
                                    )
                                }
                                className="inline-flex h-11 cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                            >
                                <KeyRound className="h-4 w-4" />
                                Reset Password
                            </button>
                        )}

                        <button
                            type="button"
                            onClick={handleGenerateRegistrationCode}
                            disabled={generatingCode}
                            className="inline-flex h-11 cursor-pointer items-center gap-2 rounded-xl bg-blue-600 px-5 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                            <KeyRound className="h-4 w-4" />
                            {generatingCode
                                ? "Generating..."
                                : "Generate Registration Code"}
                        </button>
                    </div>
                </div>
            </div>

            {registrationError && (
                <p
                    role="alert"
                    className="mt-6 rounded-xl border border-red-100 bg-red-50 px-5 py-4 text-sm text-red-700"
                >
                    {registrationError}
                </p>
            )}

            <div className="mt-6 grid gap-6 xl:grid-cols-2">
                <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                    <SectionHeader
                        icon={UserRound}
                        title="Student Information"
                        subtitle="Basic student details"
                    />
                    <div className="mt-6 grid gap-5 sm:grid-cols-2">
                        <InfoItem label="Full Name" value={student.fullName} />
                        <InfoItem
                            label="Index Number"
                            value={student.indexNumber}
                        />
                        <InfoItem
                            label="Date of Birth"
                            value={formatDate(student.dateOfBirth)}
                        />
                        <InfoItem
                            label="Student Status"
                            value={student.isActive ? "Active" : "Inactive"}
                        />
                        <InfoItem
                            label="Graduation Status"
                            value={
                                student.isGraduated
                                    ? "Graduated"
                                    : "Not Graduated"
                            }
                        />
                        <InfoItem
                            label="Graduation Date"
                            value={formatDate(student.graduationDate)}
                        />
                        {student.isGraduated && (
                            <InfoItem
                                label="Graduation Academic Year"
                                value={student.graduationAcademicYear}
                            />
                        )}
                    </div>
                </div>

                <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                    <SectionHeader
                        icon={GraduationCap}
                        title="Current Academic Enrollment"
                        subtitle="Current academic placement"
                    />
                    {currentEnrollment ? (
                        <div className="mt-6 grid gap-5 sm:grid-cols-2">
                            <InfoItem
                                label="Academic Year"
                                value={currentEnrollment.academicYear}
                            />
                            <InfoItem
                                label="Section"
                                value={currentEnrollment.section}
                            />
                            <InfoItem
                                label="Grade"
                                value={currentEnrollment.grade}
                            />
                            <InfoItem
                                label="Class"
                                value={currentEnrollment.class}
                            />
                            <InfoItem
                                label="Enrollment Date"
                                value={formatDate(
                                    currentEnrollment.enrollmentDate
                                )}
                            />
                            <InfoItem
                                label="Created By"
                                value={currentEnrollment.createdByStaff}
                            />
                        </div>
                    ) : (
                        <EmptyState text="No current academic enrollment found." />
                    )}
                </div>
            </div>

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white shadow-sm">
                <SectionHeader
                    icon={BookOpen}
                    title="Subjects"
                    subtitle="Current subject enrollments"
                />
                {subjects.length ? (
                    <div className="overflow-x-auto">
                        <table className="w-full min-w-[700px]">
                            <thead className="bg-slate-50">
                                <tr className="border-y border-slate-200">
                                    {[
                                        "Subject",
                                        "Code",
                                        "Academic Year",
                                        "Enrolled At",
                                        "Enrolled By",
                                    ].map((label) => (
                                        <TableHead key={label}>
                                            {label}
                                        </TableHead>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {subjects.map((subject) => (
                                    <tr
                                        key={subject.id}
                                        className="border-b border-slate-100 last:border-b-0"
                                    >
                                        <TableCell strong>
                                            {subject.subject}
                                        </TableCell>
                                        <TableCell>
                                            {subject.subjectCode || "—"}
                                        </TableCell>
                                        <TableCell>
                                            {subject.academicYear}
                                        </TableCell>
                                        <TableCell>
                                            {formatDate(subject.enrolledAt)}
                                        </TableCell>
                                        <TableCell>
                                            {subject.enrolledByStaff || "—"}
                                        </TableCell>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState text="No active subjects found." />
                )}
            </div>

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white shadow-sm">
                <SectionHeader
                    icon={Users}
                    title="Parents & Guardians"
                    subtitle="Linked parent and guardian information"
                />
                {guardians.length ? (
                    <div className="grid gap-4 p-6 md:grid-cols-2">
                        {guardians.map((guardian) => (
                            <div
                                key={guardian.relationshipId}
                                className="rounded-xl border border-slate-200 p-5"
                            >
                                <div className="flex items-start justify-between gap-4">
                                    <div>
                                        <p className="font-semibold text-slate-900">
                                            {guardian.fullName}
                                        </p>
                                        <p className="mt-1 text-sm text-slate-500">
                                            {guardian.relationship}
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
                                <div className="mt-5 space-y-3 text-sm text-slate-600">
                                    <p className="flex items-center gap-3">
                                        <ShieldCheck className="h-4 w-4 text-slate-400" />
                                        {guardian.parentNumber || "—"}
                                    </p>
                                    <p className="flex items-center gap-3">
                                        <Mail className="h-4 w-4 text-slate-400" />
                                        {guardian.email || "—"}
                                    </p>
                                    <p className="flex items-center gap-3">
                                        <Phone className="h-4 w-4 text-slate-400" />
                                        {guardian.phoneNumber || "—"}
                                    </p>
                                </div>
                            </div>
                        ))}
                    </div>
                ) : (
                    <EmptyState text="No parent or guardian linked." />
                )}
            </div>

            <div className="mt-6 rounded-2xl border border-slate-200 bg-white shadow-sm">
                <SectionHeader
                    icon={CalendarDays}
                    title="Academic History"
                    subtitle="Student enrollment history"
                />
                {enrollmentHistory.length ? (
                    <div className="overflow-x-auto">
                        <table className="w-full min-w-[800px]">
                            <thead className="bg-slate-50">
                                <tr className="border-y border-slate-200">
                                    {[
                                        "Academic Year",
                                        "Section",
                                        "Grade",
                                        "Class",
                                        "Enrollment Date",
                                        "Status",
                                    ].map((label) => (
                                        <TableHead key={label}>
                                            {label}
                                        </TableHead>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {enrollmentHistory.map((item) => (
                                    <tr
                                        key={item.id}
                                        className="border-b border-slate-100 last:border-b-0"
                                    >
                                        <TableCell strong>
                                            {item.academicYear}
                                        </TableCell>
                                        <TableCell>{item.section}</TableCell>
                                        <TableCell>{item.grade}</TableCell>
                                        <TableCell>{item.class}</TableCell>
                                        <TableCell>
                                            {formatDate(item.enrollmentDate)}
                                        </TableCell>
                                        <TableCell>
                                            <span
                                                className={
                                                    item.isCurrent
                                                        ? "rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-blue-700"
                                                        : "rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-600"
                                                }
                                            >
                                                {item.isCurrent
                                                    ? "Current"
                                                    : "Previous"}
                                            </span>
                                        </TableCell>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : (
                    <EmptyState text="No academic history found." />
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
                                onClick={() => setRegistrationData(null)}
                                className="flex h-9 w-9 cursor-pointer items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100"
                                aria-label="Close"
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
                                        {registrationData.registrationCode}
                                    </span>
                                    <button
                                        type="button"
                                        onClick={handleCopyCode}
                                        className="flex h-9 w-9 cursor-pointer items-center justify-center rounded-lg bg-white text-blue-600 hover:bg-blue-100"
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
                                    value={registrationData.student?.fullName}
                                />
                                <ModalInfoRow
                                    label="Index Number"
                                    value={registrationData.student?.indexNumber}
                                />
                                <ModalInfoRow
                                    label="Registered Email"
                                    value={registrationData.student?.maskedEmail}
                                />
                                <ModalInfoRow
                                    label="Expires In"
                                    value={
                                        String(
                                            registrationData.expiresInMinutes ??
                                            30
                                        ) + " minutes"
                                    }
                                />
                                <ModalInfoRow
                                    label="Maximum Attempts"
                                    value={registrationData.maxAttempts}
                                />
                            </div>

                            <div className="mt-6 rounded-xl border border-amber-100 bg-amber-50 px-4 py-3 text-xs leading-5 text-amber-700">
                                This code is temporary. If a new code is
                                generated, the previous unused code becomes
                                invalid.
                            </div>
                            <button
                                type="button"
                                onClick={() => setRegistrationData(null)}
                                className="mt-6 inline-flex h-11 w-full cursor-pointer items-center justify-center rounded-xl bg-slate-900 px-5 text-sm font-semibold text-white hover:bg-slate-800"
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

function InfoItem({ label, value }) {
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

function SectionHeader({ icon: Icon, title, subtitle }) {
    return (
        <div className="flex items-center gap-3 p-6">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                <Icon className="h-5 w-5" />
            </div>
            <div>
                <h2 className="font-semibold text-slate-950">{title}</h2>
                <p className="text-xs text-slate-500">{subtitle}</p>
            </div>
        </div>
    );
}

function EmptyState({ text }) {
    return (
        <div className="px-6 py-12 text-center text-sm text-slate-400">
            {text}
        </div>
    );
}

function ModalInfoRow({ label, value }) {
    return (
        <div className="flex items-center justify-between gap-6 border-b border-slate-100 pb-3 last:border-b-0">
            <span className="text-sm text-slate-500">{label}</span>
            <span className="text-right text-sm font-semibold text-slate-900">
                {value || "—"}
            </span>
        </div>
    );
}

function TableHead({ children }) {
    return (
        <th className="px-6 py-4 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            {children}
        </th>
    );
}

function TableCell({ children, strong = false }) {
    return (
        <td
            className={
                strong
                    ? "px-6 py-4 text-sm font-semibold text-slate-800"
                    : "px-6 py-4 text-sm text-slate-600"
            }
        >
            {children}
        </td>
    );
}