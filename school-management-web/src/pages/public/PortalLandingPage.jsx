import {
    ArrowRight,
    BookOpen,
    GraduationCap,
    School,
    ShieldCheck,
    Users,
} from "lucide-react";
import { Link } from "react-router-dom";

const portals = [
    {
        title: "Student Portal",
        description:
            "Check your timetable, attendance, results, and school updates.",
        path: "/student/login",
        action: "Student sign in",
        icon: GraduationCap,
        accent: "bg-blue-50 text-blue-700",
    },
    {
        title: "Staff Portal",
        description:
            "Access your school workspace with your staff ID or email.",
        path: "/login",
        action: "Staff sign in",
        icon: BookOpen,
        accent: "bg-violet-50 text-violet-700",
    },
    {
        title: "Parent Portal",
        description:
            "Stay informed about your children’s learning and progress.",
        path: "/parent/login",
        action: "Parent sign in",
        icon: Users,
        accent: "bg-emerald-50 text-emerald-700",
    },
];

export default function PortalLandingPage() {
    return (
        <div className="min-h-screen bg-slate-50 text-slate-950">
            <header className="relative z-10 border-b border-slate-200 bg-white">
                <div className="mx-auto flex min-h-20 max-w-7xl items-center justify-between gap-4 px-5 sm:px-8">
                    <Link
                        to="/"
                        className="flex cursor-pointer items-center gap-3"
                        aria-label="School Management home"
                    >
                        <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-white">
                            <School className="h-6 w-6" />
                        </span>

                        <span>
                            <span className="block text-sm font-bold leading-tight sm:text-base">
                                School Management
                            </span>
                            <span className="block text-xs text-slate-500">
                                School Portal
                            </span>
                        </span>
                    </Link>

                    <nav
                        className="flex items-center gap-3 text-sm font-semibold sm:gap-6"
                        aria-label="Main navigation"
                    >
                        <a
                            href="#about"
                            className="hidden cursor-pointer text-slate-600 hover:text-blue-700 sm:inline"
                        >
                            About
                        </a>
                        <a
                            href="#portals"
                            className="cursor-pointer rounded-lg bg-blue-600 px-4 py-2.5 text-white transition hover:bg-blue-700"
                        >
                            Choose a portal
                        </a>
                    </nav>
                </div>
            </header>

            <main>
                <section className="relative isolate overflow-hidden bg-slate-950 text-white">
                    <div className="pointer-events-none absolute -left-32 top-0 h-80 w-80 rounded-full bg-blue-500/20 blur-3xl" />
                    <div className="pointer-events-none absolute -right-20 bottom-0 h-96 w-96 rounded-full bg-indigo-500/25 blur-3xl" />

                    <div className="mx-auto grid max-w-7xl items-center gap-10 px-5 py-16 sm:px-8 sm:py-20 lg:grid-cols-[1.15fr_0.85fr] lg:gap-16 lg:py-28">
                        <div className="relative">
                            <p className="inline-flex rounded-full border border-blue-300/20 bg-blue-300/10 px-4 py-2 text-xs font-semibold uppercase tracking-widest text-blue-200">
                                Welcome to our school portal
                            </p>

                            <h1 className="mt-7 max-w-2xl text-4xl font-bold leading-tight tracking-tight sm:text-5xl lg:text-6xl">
                                A connected place for our school community.
                            </h1>

                            <p className="mt-6 max-w-xl text-base leading-8 text-slate-300 sm:text-lg">
                                Find your learning information, school updates,
                                and the tools you need in one place.
                            </p>

                            <a
                                href="#portals"
                                className="mt-8 inline-flex cursor-pointer items-center gap-2 rounded-xl bg-blue-600 px-6 py-3.5 text-sm font-semibold text-white transition hover:bg-blue-500"
                            >
                                Explore the portals
                                <ArrowRight className="h-4 w-4" />
                            </a>
                        </div>

                        <div
                            className="relative mx-auto w-full max-w-md lg:max-w-none"
                            aria-hidden="true"
                        >
                            <div className="rounded-[2rem] border border-white/15 bg-white/10 p-5 shadow-2xl backdrop-blur-sm sm:p-8">
                                <div className="rounded-2xl border border-white/15 bg-slate-900/70 p-6 sm:p-8">
                                    <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-blue-500 text-white shadow-lg shadow-blue-500/20">
                                        <School className="h-8 w-8" />
                                    </div>

                                    <div className="mt-9 h-3 w-32 rounded-full bg-blue-300/80" />
                                    <div className="mt-4 h-3 w-full rounded-full bg-white/25" />
                                    <div className="mt-3 h-3 w-4/5 rounded-full bg-white/15" />

                                    <div className="mt-8 grid grid-cols-3 gap-3">
                                        {[GraduationCap, BookOpen, Users].map(
                                            (Icon, index) => (
                                                <div
                                                    key={index}
                                                    className="flex aspect-square items-center justify-center rounded-2xl border border-white/10 bg-white/10"
                                                >
                                                    <Icon className="h-7 w-7 text-blue-200" />
                                                </div>
                                            )
                                        )}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </section>

                <section
                    id="about"
                    className="scroll-mt-20 bg-white px-5 py-16 sm:px-8 sm:py-20"
                >
                    <div className="mx-auto max-w-7xl md:flex md:items-end md:justify-between md:gap-12">
                        <div className="max-w-2xl">
                            <p className="text-sm font-semibold uppercase tracking-widest text-blue-600">
                                Our community
                            </p>
                            <h2 className="mt-3 text-3xl font-bold tracking-tight sm:text-4xl">
                                Everything in its place.
                            </h2>
                            <p className="mt-5 text-base leading-8 text-slate-600">
                                Students can follow their academic journey,
                                parents can stay involved, and staff can access
                                the school information they need.
                            </p>
                        </div>

                        <div className="mt-7 inline-flex items-center gap-3 rounded-2xl bg-blue-50 px-5 py-4 text-sm font-medium text-blue-900 md:mt-0">
                            <ShieldCheck className="h-6 w-6 shrink-0 text-blue-600" />
                            Sign in to access your own information.
                        </div>
                    </div>
                </section>

                <section
                    id="portals"
                    className="scroll-mt-20 px-5 py-16 sm:px-8 sm:py-20"
                >
                    <div className="mx-auto max-w-7xl">
                        <p className="text-sm font-semibold uppercase tracking-widest text-blue-600">
                            Get started
                        </p>
                        <h2 className="mt-3 text-3xl font-bold tracking-tight sm:text-4xl">
                            Choose your portal
                        </h2>
                        <p className="mt-4 text-slate-600">
                            Select the portal that belongs to you.
                        </p>

                        <div className="mt-9 grid gap-5 md:grid-cols-3">
                            {portals.map(
                                ({
                                    title,
                                    description,
                                    path,
                                    action,
                                    icon: Icon,
                                    accent,
                                }) => (
                                    <Link
                                        key={path}
                                        to={path}
                                        className="group flex min-h-64 cursor-pointer flex-col rounded-2xl border border-slate-200 bg-white p-6 shadow-sm transition hover:-translate-y-1 hover:border-blue-200 hover:shadow-xl focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 sm:p-7"
                                    >
                                        <span
                                            className={`flex h-13 w-13 items-center justify-center rounded-xl ${accent}`}
                                        >
                                            <Icon className="h-6 w-6" />
                                        </span>

                                        <h3 className="mt-6 text-xl font-bold">
                                            {title}
                                        </h3>
                                        <p className="mt-3 flex-1 text-sm leading-6 text-slate-600">
                                            {description}
                                        </p>
                                        <span className="mt-7 inline-flex items-center gap-2 text-sm font-semibold text-blue-700">
                                            {action}
                                            <ArrowRight className="h-4 w-4 transition group-hover:translate-x-1" />
                                        </span>
                                    </Link>
                                )
                            )}
                        </div>
                    </div>
                </section>
            </main>

            <footer className="border-t border-slate-200 bg-white px-5 py-8 sm:px-8">
                <div className="mx-auto flex max-w-7xl flex-col gap-3 text-sm text-slate-500 sm:flex-row sm:items-center sm:justify-between">
                    <p>© {new Date().getFullYear()} School Management.</p>
                    <p>For account help, contact your school office.</p>
                </div>
            </footer>
        </div>
    );
}