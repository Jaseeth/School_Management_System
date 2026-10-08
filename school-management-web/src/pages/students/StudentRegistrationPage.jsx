import {
    ArrowLeft,
    ArrowRight,
    Check,
    CheckCircle2,
    Eye,
    EyeOff,
    GraduationCap,
    KeyRound,
    LockKeyhole,
    Mail,
    ShieldCheck,
} from "lucide-react";

import {
    useEffect,
    useState,
} from "react";

import {
    Link,
    useNavigate,
} from "react-router-dom";

import {
    studentRegistrationApi,
} from "../../api/studentRegistrationApi";


const steps = [
    {
        number: 1,
        title: "Verify Student",
    },
    {
        number: 2,
        title: "Email Verification",
    },
    {
        number: 3,
        title: "Create Password",
    },
];


export default function StudentRegistrationPage() {

    const navigate =
        useNavigate();

    const [
        currentStep,
        setCurrentStep,
    ] = useState(1);


    // ============================================================
    // FORM
    // ============================================================

    const [
        form,
        setForm,
    ] = useState({
        indexNumber: "",
        registrationCode: "",
        otp: "",
        password: "",
        confirmPassword: "",
    });


    // ============================================================
    // STUDENT DATA
    // ============================================================

    const [
        studentData,
        setStudentData,
    ] = useState(null);


    // ============================================================
    // OTP
    // ============================================================

    const [
        resendSeconds,
        setResendSeconds,
    ] = useState(0);


    // ============================================================
    // PASSWORD VISIBILITY
    // ============================================================

    const [
        showPassword,
        setShowPassword,
    ] = useState(false);

    const [
        showConfirmPassword,
        setShowConfirmPassword,
    ] = useState(false);


    // ============================================================
    // UI
    // ============================================================

    const [
        loading,
        setLoading,
    ] = useState(false);

    const [
        error,
        setError,
    ] = useState("");

    const [
        success,
        setSuccess,
    ] = useState("");

    const [
        registrationCompleted,
        setRegistrationCompleted,
    ] = useState(false);


    // ============================================================
    // RESEND TIMER
    // ============================================================

    useEffect(() => {

        if (resendSeconds <= 0) {
            return;
        }

        const timer =
            setInterval(
                () => {

                    setResendSeconds(
                        (current) =>
                            current <= 1
                                ? 0
                                : current - 1
                    );

                },
                1000
            );

        return () =>
            clearInterval(timer);

    }, [resendSeconds]);


    // ============================================================
    // FORM CHANGE
    // ============================================================

    const handleChange =
        (event) => {

            const {
                name,
                value,
            } =
                event.target;

            let newValue =
                value;

            if (
                name ===
                "registrationCode"
            ) {

                newValue =
                    value
                        .toUpperCase()
                        .replace(
                            /[^A-Z0-9]/g,
                            ""
                        )
                        .slice(
                            0,
                            6
                        );

            }

            if (
                name === "otp"
            ) {

                newValue =
                    value
                        .replace(
                            /\D/g,
                            ""
                        )
                        .slice(
                            0,
                            6
                        );

            }

            setForm(
                (current) => ({
                    ...current,

                    [name]:
                        newValue,
                })
            );

            setError("");
            setSuccess("");

        };


    // ============================================================
    // STEP 1
    // VALIDATE REGISTRATION CODE
    // ============================================================

    const handleValidate =
        async (
            event
        ) => {

            event.preventDefault();

            setError("");
            setSuccess("");

            if (
                !form
                    .indexNumber
                    .trim()
            ) {

                setError(
                    "Index number is required."
                );

                return;

            }

            if (
                !form
                    .registrationCode
                    .trim()
            ) {

                setError(
                    "Registration code is required."
                );

                return;

            }

            try {

                setLoading(true);

                const response =
                    await studentRegistrationApi
                        .validateCode({
                            indexNumber:
                                form
                                    .indexNumber
                                    .trim(),

                            registrationCode:
                                form
                                    .registrationCode
                                    .trim(),
                        });

                setStudentData(
                    response.data
                );


                // REQUEST OTP IMMEDIATELY
                const otpResponse =
                    await studentRegistrationApi
                        .requestEmailOtp({
                            indexNumber:
                                form
                                    .indexNumber
                                    .trim(),

                            registrationCode:
                                form
                                    .registrationCode
                                    .trim(),
                        });

                setStudentData(
                    (current) => ({
                        ...current,

                        maskedEmail:
                            otpResponse
                                .data
                                ?.maskedEmail ||
                            response
                                .data
                                ?.student
                                ?.maskedEmail,

                        studentName:
                            otpResponse
                                .data
                                ?.studentName ||
                            response
                                .data
                                ?.student
                                ?.fullName,
                    })
                );

                setResendSeconds(
                    otpResponse
                        .data
                        ?.resendAfterSeconds ??
                    60
                );

                setCurrentStep(2);

                setSuccess(
                    "Verification code sent to your registered email."
                );

            }
            catch (err) {

                console.error(
                    "Student registration validation failed:",
                    err
                );

                setError(
                    getApiError(
                        err,
                        "Unable to validate registration details."
                    )
                );

            }
            finally {

                setLoading(false);

            }

        };


    // ============================================================
    // RESEND OTP
    // ============================================================

    const handleResendOtp =
        async () => {

            if (
                resendSeconds > 0
            ) {
                return;
            }

            try {

                setLoading(true);
                setError("");
                setSuccess("");

                const response =
                    await studentRegistrationApi
                        .requestEmailOtp({
                            indexNumber:
                                form
                                    .indexNumber
                                    .trim(),

                            registrationCode:
                                form
                                    .registrationCode
                                    .trim(),
                        });

                setResendSeconds(
                    response
                        .data
                        ?.resendAfterSeconds ??
                    60
                );

                setSuccess(
                    "A new verification code has been sent."
                );

            }
            catch (err) {

                console.error(
                    "OTP resend failed:",
                    err
                );

                const retry =
                    err?.response
                        ?.data
                        ?.retryAfterSeconds;

                if (retry) {
                    setResendSeconds(
                        retry
                    );
                }

                setError(
                    getApiError(
                        err,
                        "Unable to resend verification code."
                    )
                );

            }
            finally {

                setLoading(false);

            }

        };


    // ============================================================
    // STEP 2
    // VERIFY OTP
    // ============================================================

    const handleVerifyOtp =
        async (
            event
        ) => {

            event.preventDefault();

            setError("");
            setSuccess("");

            if (
                form.otp.length !== 6
            ) {

                setError(
                    "Enter the 6-digit verification code."
                );

                return;

            }

            try {

                setLoading(true);

                await studentRegistrationApi
                    .verifyEmailOtp({
                        indexNumber:
                            form
                                .indexNumber
                                .trim(),

                        registrationCode:
                            form
                                .registrationCode
                                .trim(),

                        otp:
                            form
                                .otp
                                .trim(),
                    });

                setCurrentStep(3);

                setSuccess(
                    "Email verified successfully."
                );

            }
            catch (err) {

                console.error(
                    "OTP verification failed:",
                    err
                );

                setError(
                    getApiError(
                        err,
                        "Unable to verify OTP."
                    )
                );

            }
            finally {

                setLoading(false);

            }

        };


    // ============================================================
    // STEP 3
    // COMPLETE REGISTRATION
    // ============================================================

    const handleComplete =
        async (
            event
        ) => {

            event.preventDefault();

            setError("");
            setSuccess("");

            if (
                !form.password
            ) {

                setError(
                    "Password is required."
                );

                return;

            }

            if (
                form.password.length < 6
            ) {

                setError(
                    "Password must contain at least 6 characters."
                );

                return;

            }

            if (
                form.password !==
                form.confirmPassword
            ) {

                setError(
                    "Passwords do not match."
                );

                return;

            }

            try {

                setLoading(true);

                const response =
                    await studentRegistrationApi
                        .completeRegistration({
                            indexNumber:
                                form
                                    .indexNumber
                                    .trim(),

                            registrationCode:
                                form
                                    .registrationCode
                                    .trim(),

                            password:
                                form.password,

                            confirmPassword:
                                form
                                    .confirmPassword,
                        });

                setRegistrationCompleted(
                    true
                );

                setSuccess(
                    response
                        .data
                        ?.message ||
                    "Student account created successfully."
                );

            }
            catch (err) {

                console.error(
                    "Account creation failed:",
                    err
                );

                setError(
                    getApiError(
                        err,
                        "Unable to create student account."
                    )
                );

            }
            finally {

                setLoading(false);

            }

        };


    // ============================================================
    // COMPLETED SCREEN
    // ============================================================

    if (
        registrationCompleted
    ) {

        return (

            <PublicPageShell>

                <div className="mx-auto w-full max-w-md text-center">

                    <div className="mx-auto flex h-20 w-20 items-center justify-center rounded-full bg-emerald-50 text-emerald-600">

                        <CheckCircle2 className="h-10 w-10" />

                    </div>

                    <h1 className="mt-6 text-3xl font-bold tracking-tight text-slate-950">
                        Account Created
                    </h1>

                    <p className="mt-3 text-sm leading-6 text-slate-500">
                        Your student account has been created successfully. You can now sign in using your registered account.
                    </p>

                    <button
                        type="button"
                        onClick={() =>
                            navigate(
                                "/login"
                            )
                        }
                        className="mt-8 inline-flex h-12 w-full items-center justify-center rounded-xl bg-blue-600 px-6 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700"
                    >
                        Go to Login
                    </button>

                </div>

            </PublicPageShell>

        );

    }


    return (

        <PublicPageShell>

            <div className="mx-auto w-full max-w-lg">

                {/* ================================================
                    HEADER
                ================================================ */}

                <div className="text-center">

                    <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-600 text-white shadow-lg shadow-blue-600/20">
                        <GraduationCap className="h-7 w-7" />
                    </div>

                    <h1 className="mt-5 text-3xl font-bold tracking-tight text-slate-950">
                        Student Registration
                    </h1>

                    <p className="mt-2 text-sm leading-6 text-slate-500">
                        Activate your student account using the registration code provided by your school.
                    </p>

                </div>


                {/* ================================================
                    STEPPER
                ================================================ */}

                <div className="mt-8 flex items-start justify-between">

                    {steps.map(
                        (
                            step,
                            index
                        ) => {

                            const completed =
                                currentStep >
                                step.number;

                            const active =
                                currentStep ===
                                step.number;

                            return (

                                <div
                                    key={
                                        step.number
                                    }
                                    className="relative flex flex-1 flex-col items-center"
                                >

                                    {index !== 0 && (

                                        <div
                                            className={`absolute right-1/2 top-5 h-0.5 w-full ${completed ||
                                                    active
                                                    ? "bg-blue-600"
                                                    : "bg-slate-200"
                                                }`}
                                        />

                                    )}

                                    <div
                                        className={`relative z-10 flex h-10 w-10 items-center justify-center rounded-full border-2 text-sm font-bold ${completed
                                                ? "border-emerald-500 bg-emerald-500 text-white"
                                                : active
                                                    ? "border-blue-600 bg-blue-600 text-white"
                                                    : "border-slate-200 bg-white text-slate-400"
                                            }`}
                                    >
                                        {completed ? (
                                            <Check className="h-4 w-4" />
                                        ) : (
                                            step.number
                                        )}
                                    </div>

                                    <p
                                        className={`mt-2 text-center text-[11px] font-semibold ${active
                                                ? "text-blue-600"
                                                : completed
                                                    ? "text-emerald-600"
                                                    : "text-slate-400"
                                            }`}
                                    >
                                        {step.title}
                                    </p>

                                </div>

                            );

                        }
                    )}

                </div>


                {/* ================================================
                    CARD
                ================================================ */}

                <div className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-xl shadow-slate-200/50 sm:p-8">

                    {error && (

                        <div className="mb-5 rounded-xl border border-red-100 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
                            {error}
                        </div>

                    )}

                    {success && (

                        <div className="mb-5 rounded-xl border border-emerald-100 bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-700">
                            {success}
                        </div>

                    )}


                    {/* ============================================
                        STEP 1
                    ============================================ */}

                    {currentStep === 1 && (

                        <form
                            onSubmit={
                                handleValidate
                            }
                        >

                            <div className="flex items-center gap-3">

                                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                                    <KeyRound className="h-5 w-5" />
                                </div>

                                <div>

                                    <h2 className="font-semibold text-slate-950">
                                        Verify your details
                                    </h2>

                                    <p className="text-xs text-slate-500">
                                        Enter your student index number and registration code.
                                    </p>

                                </div>

                            </div>


                            <div className="mt-7 space-y-5">

                                <FormField
                                    label="Index Number"
                                    name="indexNumber"
                                    value={
                                        form
                                            .indexNumber
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    placeholder="Enter your index number"
                                    disabled={
                                        loading
                                    }
                                />

                                <FormField
                                    label="Registration Code"
                                    name="registrationCode"
                                    value={
                                        form
                                            .registrationCode
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    placeholder="Example: FZCCAB"
                                    maxLength={6}
                                    disabled={
                                        loading
                                    }
                                    className="uppercase tracking-[0.2em]"
                                />

                            </div>


                            <button
                                type="submit"
                                disabled={
                                    loading
                                }
                                className="mt-7 inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-6 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                            >

                                {loading ? (
                                    <>
                                        <Spinner />
                                        Verifying...
                                    </>
                                ) : (
                                    <>
                                        Continue
                                        <ArrowRight className="h-4 w-4" />
                                    </>
                                )}

                            </button>

                        </form>

                    )}


                    {/* ============================================
                        STEP 2
                    ============================================ */}

                    {currentStep === 2 && (

                        <form
                            onSubmit={
                                handleVerifyOtp
                            }
                        >

                            <div className="flex items-center gap-3">

                                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                                    <Mail className="h-5 w-5" />
                                </div>

                                <div>

                                    <h2 className="font-semibold text-slate-950">
                                        Verify your email
                                    </h2>

                                    <p className="text-xs text-slate-500">
                                        We sent a 6-digit code to your registered email.
                                    </p>

                                </div>

                            </div>


                            <div className="mt-6 rounded-xl border border-blue-100 bg-blue-50 p-4">

                                <p className="text-xs text-blue-600">
                                    Verification code sent to
                                </p>

                                <p className="mt-1 text-sm font-semibold text-slate-900">
                                    {
                                        studentData
                                            ?.maskedEmail ||
                                        studentData
                                            ?.student
                                            ?.maskedEmail ||
                                        "Registered email"
                                    }
                                </p>

                            </div>


                            <div className="mt-6">

                                <label className="mb-2 block text-sm font-semibold text-slate-700">
                                    Verification Code
                                </label>

                                <input
                                    type="text"
                                    inputMode="numeric"
                                    autoComplete="one-time-code"
                                    name="otp"
                                    value={
                                        form.otp
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    placeholder="000000"
                                    maxLength={6}
                                    disabled={
                                        loading
                                    }
                                    className="h-14 w-full rounded-xl border border-slate-200 bg-white px-4 text-center text-2xl font-bold tracking-[0.35em] text-slate-950 outline-none transition placeholder:text-slate-300 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                                />

                            </div>


                            <div className="mt-4 flex items-center justify-between gap-4">

                                <button
                                    type="button"
                                    onClick={() => {

                                        setCurrentStep(
                                            1
                                        );

                                        setForm(
                                            (current) => ({
                                                ...current,
                                                otp: "",
                                            })
                                        );

                                        setError("");
                                        setSuccess("");

                                    }}
                                    disabled={
                                        loading
                                    }
                                    className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-800"
                                >
                                    <ArrowLeft className="h-4 w-4" />
                                    Back
                                </button>


                                <button
                                    type="button"
                                    onClick={
                                        handleResendOtp
                                    }
                                    disabled={
                                        loading ||
                                        resendSeconds > 0
                                    }
                                    className="text-sm font-semibold text-blue-600 transition hover:text-blue-700 disabled:cursor-not-allowed disabled:text-slate-400"
                                >
                                    {resendSeconds > 0
                                        ? `Resend in ${resendSeconds}s`
                                        : "Resend Code"}
                                </button>

                            </div>


                            <button
                                type="submit"
                                disabled={
                                    loading
                                }
                                className="mt-7 inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-6 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                            >

                                {loading ? (
                                    <>
                                        <Spinner />
                                        Verifying...
                                    </>
                                ) : (
                                    <>
                                        Verify Email
                                        <ShieldCheck className="h-4 w-4" />
                                    </>
                                )}

                            </button>

                        </form>

                    )}


                    {/* ============================================
                        STEP 3
                    ============================================ */}

                    {currentStep === 3 && (

                        <form
                            onSubmit={
                                handleComplete
                            }
                        >

                            <div className="flex items-center gap-3">

                                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-700">
                                    <LockKeyhole className="h-5 w-5" />
                                </div>

                                <div>

                                    <h2 className="font-semibold text-slate-950">
                                        Create your password
                                    </h2>

                                    <p className="text-xs text-slate-500">
                                        Choose a secure password for your student account.
                                    </p>

                                </div>

                            </div>


                            <div className="mt-7 space-y-5">

                                <PasswordField
                                    label="Password"
                                    name="password"
                                    value={
                                        form.password
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    visible={
                                        showPassword
                                    }
                                    onToggle={() =>
                                        setShowPassword(
                                            (current) =>
                                                !current
                                        )
                                    }
                                    disabled={
                                        loading
                                    }
                                />

                                <PasswordField
                                    label="Confirm Password"
                                    name="confirmPassword"
                                    value={
                                        form
                                            .confirmPassword
                                    }
                                    onChange={
                                        handleChange
                                    }
                                    visible={
                                        showConfirmPassword
                                    }
                                    onToggle={() =>
                                        setShowConfirmPassword(
                                            (current) =>
                                                !current
                                        )
                                    }
                                    disabled={
                                        loading
                                    }
                                />

                            </div>


                            <div className="mt-5 rounded-xl border border-slate-200 bg-slate-50 p-4">

                                <p className="text-xs font-semibold text-slate-700">
                                    Your account will use:
                                </p>

                                <p className="mt-2 text-sm text-slate-500">
                                    {
                                        studentData
                                            ?.student
                                            ?.fullName ||
                                        studentData
                                            ?.studentName ||
                                        "Student"
                                    }
                                </p>

                                <p className="mt-1 text-sm font-medium text-slate-700">
                                    {
                                        studentData
                                            ?.student
                                            ?.maskedEmail ||
                                        studentData
                                            ?.maskedEmail
                                    }
                                </p>

                            </div>


                            <button
                                type="submit"
                                disabled={
                                    loading
                                }
                                className="mt-7 inline-flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-6 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                            >

                                {loading ? (
                                    <>
                                        <Spinner />
                                        Creating Account...
                                    </>
                                ) : (
                                    <>
                                        <CheckCircle2 className="h-4 w-4" />
                                        Create Account
                                    </>
                                )}

                            </button>

                        </form>

                    )}

                </div>


                {/* ================================================
                    LOGIN LINK
                ================================================ */}

                <p className="mt-6 text-center text-sm text-slate-500">
                    Already have an account?{" "}

                    <Link
                        to="/student/login"
                        className="font-semibold text-blue-600 hover:text-blue-700"
                    >
                        Sign in
                    </Link>
                </p>

            </div>

        </PublicPageShell>

    );

}


// ============================================================
// PUBLIC PAGE SHELL
// ============================================================

function PublicPageShell({
    children,
}) {

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


            <main className="flex min-h-[calc(100vh-5rem)] items-center justify-center px-5 py-10 sm:px-7 lg:px-8">

                {children}

            </main>

        </div>

    );

}


// ============================================================
// FORM FIELD
// ============================================================

function FormField({
    label,
    className = "",
    ...props
}) {

    return (

        <div>

            <label className="mb-2 block text-sm font-semibold text-slate-700">
                {label}
            </label>

            <input
                {...props}
                className={`h-12 w-full rounded-xl border border-slate-200 bg-white px-4 text-sm text-slate-950 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10 ${className}`}
            />

        </div>

    );

}


// ============================================================
// PASSWORD FIELD
// ============================================================

function PasswordField({
    label,
    visible,
    onToggle,
    ...props
}) {

    return (

        <div>

            <label className="mb-2 block text-sm font-semibold text-slate-700">
                {label}
            </label>

            <div className="relative">

                <input
                    {...props}
                    type={
                        visible
                            ? "text"
                            : "password"
                    }
                    className="h-12 w-full rounded-xl border border-slate-200 bg-white px-4 pr-12 text-sm text-slate-950 outline-none transition placeholder:text-slate-400 focus:border-blue-500 focus:ring-4 focus:ring-blue-500/10"
                />

                <button
                    type="button"
                    onClick={
                        onToggle
                    }
                    className="absolute right-3 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
                >
                    {visible ? (
                        <EyeOff className="h-4 w-4" />
                    ) : (
                        <Eye className="h-4 w-4" />
                    )}
                </button>

            </div>

        </div>

    );

}


// ============================================================
// SPINNER
// ============================================================

function Spinner() {

    return (
        <div className="h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />
    );

}


// ============================================================
// API ERROR
// ============================================================

function getApiError(
    err,
    fallback
) {

    const data =
        err?.response?.data;

    if (
        Array.isArray(
            data?.errors
        )
    ) {

        return data.errors.join(
            " "
        );

    }

    if (
        typeof data?.message ===
        "string"
    ) {

        return data.message;

    }

    return fallback;

}