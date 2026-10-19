import {
    Bell,
    BookOpen,
    CalendarDays,
    ChevronLeft,
    ChevronRight,
    ClipboardCheck,
    FileText,
    FilePenLine,
    GraduationCap,
    LayoutDashboard,
    LogOut,
    Megaphone,
    Menu,
    School,
    ShieldCheck,
    UserRound,
    Users,
    X,
} from "lucide-react";
import { useEffect, useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";
import StaffAccountMenu from "./StaffAccountMenu";
import api from "../../api/axios";

const menuItems = [
    { title: "School Settings", path: "/school-settings", icon: School, roles: ["Admin", "Principal", "Deputy Principal"] },
    {
        title: "Dashboard",
        path: "/dashboard",
        icon: LayoutDashboard,
    },
    {
        title: "Students",
        path: "/students",
        icon: GraduationCap,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"],
    },
    {
        title: "Children",
        path: "/parent/dashboard",
        icon: GraduationCap,
        roles: ["Parent"],
    },
    {
        title: "Parents",
        path: "/parents",
        icon: UserRound,
        roles: ["Admin", "Principal", "Deputy Principal"],
    },
    {
        title: "Staff",
        path: "/staff",
        icon: Users,
        roles: ["Admin", "Principal", "Deputy Principal"],
    },
    {
        title: "Section Head Assignments",
        path: "/section-head-assignments",
        icon: ShieldCheck,
        roles: ["Admin"],
    },
    {
        title: "Academic Setup",
        path: "/academic-setup",
        icon: BookOpen,
        roles: ["Admin", "Principal", "Deputy Principal"],
    },
    {
        title: "Student Promotion",
        path: "/promotion",
        icon: GraduationCap,
        roles: ["Admin", "Principal", "Deputy Principal"],
    },
    {
        title: "Teacher Assignments",
        path: "/teacher-assignments",
        icon: Users,
        roles: ["Admin", "Principal", "Deputy Principal"],
    },
    {
        title: "Attendance",
        path: "/attendance",
        icon: ClipboardCheck,
        roles: [
            "Admin",
            "Principal",
            "Deputy Principal",
            "Section Head",
            "Parent",
        ],
    },

    {
        title: "Attendance Approvals",
        path: "/section-head/attendance-approvals",
        icon: ClipboardCheck,
        roles: ["Section Head"],
    },

    {
        title: "Class Attendance",
        path: "/teacher/attendance",
        icon: ClipboardCheck,
        roles: ["Teacher"],
    },
    {
        title: "Timetable",
        path: "/timetable",
        icon: CalendarDays,
        roles: [
            "Admin",
            "Principal",
            "Deputy Principal",
        ],
    },

    {
        title: "My Timetable",
        path: "/teacher/timetable",
        icon: CalendarDays,
        roles: ["Teacher"],
    },
    {
        title: "Today & Next Class",
        path: "/teacher/today",
        icon: CalendarDays,
        roles: ["Teacher"],
    },

    {
        title: "Announcements",
        path: "/announcements",
        icon: Megaphone,
        roles: [
            "Admin",
            "Principal",
            "Deputy Principal",
            "Section Head",
            "Teacher",
            "Student",
            "Parent",
        ],
    },
    {
        title: "Special Classes",
        path: "/special-classes",
        icon: CalendarDays,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"],
    },
    {
        title: "Staff Leave",
        path: "/staff-leave",
        icon: CalendarDays,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"],
    },
    {
        title: "Staff Attendance",
        path: "/staff-attendance",
        icon: ClipboardCheck,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head"],
    },
    {
        title: "Staff Leave Allowance", path: "/staff-leave-allowance", icon: CalendarDays, roles: ["Admin"],
    },
    {
        title: "School Calendar", path: "/school-calendar", icon: CalendarDays,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"],
    },
    {
        title: "Notifications",
        path: "/notifications",
        icon: Bell,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"],
    },
    {
        title: "Reports",
        path: "/reports",
        icon: FileText,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head"],
    },
    {
        title: "Audit Logs",
        path: "/audit-logs",
        icon: ShieldCheck,
        roles: ["Admin", "Principal", "Deputy Principal"],
    },
    {
        title: "Marks",
        path: "/teacher/marks",
        icon: FilePenLine,
        roles: ["Teacher"],
    },
    {
        title: "Marks Review",
        path: "/section-head/marks-review",
        icon: ClipboardCheck,
        roles: ["Section Head"],
    },
    {
        title: "Results Publishing",
        path: "/results/publishing",
        icon: FileText,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"],
    },

    {
        title: "Exams & Tests",
        path: "/exams",
        icon: FileText,
        roles: ["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher"],
    },
];

export default function DashboardLayout() {
    const [schoolProfile, setSchoolProfile] = useState(null);
    useEffect(() => {
        let active = true;
        const load = () => api.get("/school-settings").then(r => { if (active) setSchoolProfile(r.data); }).catch(() => {});
        load(); window.addEventListener("school-settings-changed", load);
        return () => { active = false; window.removeEventListener("school-settings-changed", load); };
    }, []);
    const navigate = useNavigate();
    const { user, logout } = useAuth();
    const [unreadCount, setUnreadCount] = useState(0);
    useEffect(() => {
        let active = true;
        async function refreshUnread() {
            try {
                const { data } = await api.get("/notifications/my/unread-count");
                if (active) setUnreadCount(data.unreadCount ?? 0);
            } catch { /* Keep the last successful count during connection failures. */ }
        }
        if (!user?.email) return;
        refreshUnread();
        const timer = window.setInterval(refreshUnread, 30000);
        window.addEventListener("staff-notifications-changed", refreshUnread);
        window.addEventListener("focus", refreshUnread);
        return () => {
            active = false;
            window.clearInterval(timer);
            window.removeEventListener("staff-notifications-changed", refreshUnread);
            window.removeEventListener("focus", refreshUnread);
        };
    }, [user?.email]);

    const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false);
    const [collapsed, setCollapsed] = useState(
        () => localStorage.getItem("sidebarCollapsed") === "true"
    );
    const [tooltip, setTooltip] = useState(null);

    useEffect(() => {
        localStorage.setItem("sidebarCollapsed", String(collapsed));
        setTooltip(null);
    }, [collapsed]);

    const customStaff = !["Admin", "Principal", "Deputy Principal", "Section Head", "Teacher", "Student", "Parent"].some(role => user?.roles?.includes(role));
    const allowedMenuItems = menuItems.filter(
        (item) =>
            (customStaff && ["/dashboard", "/staff-leave", "/school-calendar", "/notifications"].includes(item.path)) ||
            !item.roles ||
            item.roles.some((role) => user?.roles?.includes(role))
    );

    const primaryRole = user?.roles?.[0] ?? "";
    const firstLetter =
        user?.fullName?.charAt(0)?.toUpperCase() || "U";

    async function handleLogout() {
        await logout();
        navigate("/login", { replace: true });
    }

    function showTooltip(event, title) {
        if (!collapsed || window.innerWidth < 1024) return;

        const rect = event.currentTarget.getBoundingClientRect();

        setTooltip({
            title,
            top: rect.top + rect.height / 2,
        });
    }

    function SidebarItem({ title, path, icon: Icon }) {
        return (
            <NavLink
                to={path}
                end={path === "/staff"}
                onMouseEnter={(event) => showTooltip(event, title)}
                onMouseLeave={() => setTooltip(null)}
                onClick={() => {
                    setMobileSidebarOpen(false);
                    setTooltip(null);
                }}
                className={({ isActive }) =>
                    `flex h-11 cursor-pointer items-center gap-3 rounded-xl px-3 text-sm font-medium transition-all duration-200 ${collapsed
                        ? "lg:justify-center lg:gap-0 lg:px-0"
                        : ""
                    } ${isActive
                        ? "bg-blue-600 text-white shadow-md shadow-blue-950/20"
                        : "text-slate-400 hover:bg-white/[0.06] hover:text-white"
                    }`
                }
            >
                <Icon className="h-5 w-5 shrink-0" />

                <span
                    className={`truncate ${collapsed ? "lg:hidden" : ""
                        }`}
                >
                    {title}
                </span>
            </NavLink>
        );
    }

    function UserCard() {
        return (
            <div className="rounded-xl bg-white/[0.05] p-4">
                <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-500/15 text-sm font-bold text-blue-300">
                        {firstLetter}
                    </div>

                    <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-semibold text-white">
                            {user?.fullName}
                        </p>
                        <p className="mt-0.5 truncate text-xs text-slate-400">
                            {user?.email}
                        </p>
                    </div>
                </div>

                <div className="mt-3">
                    <span className="inline-flex rounded-md bg-blue-500/10 px-2.5 py-1 text-xs font-medium text-blue-300">
                        {primaryRole}
                    </span>
                </div>

                <button
                    type="button"
                    onClick={handleLogout}
                    className="mt-4 flex w-full cursor-pointer items-center justify-center gap-2 rounded-lg border border-white/10 px-3 py-2 text-sm text-slate-300 transition hover:bg-white/[0.07] hover:text-white"
                >
                    <LogOut className="h-4 w-4" />
                    Sign out
                </button>
            </div>
        );
    }

    return (
        <div className="min-h-screen overflow-x-hidden bg-slate-50">
            {collapsed && tooltip && (
                <div
                    className="pointer-events-none fixed z-[9999] hidden -translate-y-1/2 rounded-lg bg-slate-900 px-3 py-2 text-xs font-medium text-white shadow-xl lg:block"
                    style={{
                        left: "88px",
                        top: `${tooltip.top}px`,
                    }}
                >
                    {tooltip.title}
                    <span className="absolute right-full top-1/2 -translate-y-1/2 border-[5px] border-transparent border-r-slate-900" />
                </div>
            )}

            {mobileSidebarOpen && (
                <div
                    className="fixed inset-0 z-40 bg-slate-950/55 backdrop-blur-sm lg:hidden"
                    onClick={() => setMobileSidebarOpen(false)}
                />
            )}

            <aside
                className={`fixed inset-y-0 left-0 z-50 flex w-72 flex-col overflow-hidden bg-slate-950 shadow-2xl shadow-slate-950/20 transition-[width,transform] duration-300 ease-in-out ${mobileSidebarOpen
                        ? "translate-x-0"
                        : "-translate-x-full"
                    } lg:translate-x-0 ${collapsed ? "lg:w-20" : "lg:w-72"
                    }`}
            >
                <div className="flex h-20 shrink-0 items-center justify-between border-b border-white/[0.08] px-5">
                    <div className="flex min-w-0 items-center gap-3">
                        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-blue-600 shadow-md shadow-blue-950/30">
                            {schoolProfile?.logoDataUrl ? <img src={schoolProfile.logoDataUrl} alt="School logo" className="h-8 w-8 object-contain" /> : <School className="h-6 w-6 text-white" />}
                        </div>

                        <div
                            className={`min-w-0 ${collapsed ? "lg:hidden" : ""
                                }`}
                        >
                            <p className="truncate text-sm font-semibold text-white">
                                {schoolProfile?.name || "School Management"}
                            </p>
                            <p className="truncate text-xs text-slate-400">
                                Education System
                            </p>
                        </div>
                    </div>

                    <button
                        type="button"
                        onClick={() => setMobileSidebarOpen(false)}
                        aria-label="Close menu"
                        className="ml-auto flex h-9 w-9 cursor-pointer items-center justify-center rounded-lg text-slate-400 transition hover:bg-white/10 hover:text-white lg:hidden"
                    >
                        <X className="h-5 w-5" />
                    </button>
                </div>

                <div
                    className={`hidden shrink-0 py-3 lg:flex ${collapsed
                            ? "justify-center"
                            : "justify-end px-4"
                        }`}
                >
                    <button
                        type="button"
                        onClick={() =>
                            setCollapsed((current) => !current)
                        }
                        className="flex h-8 w-8 cursor-pointer items-center justify-center rounded-lg border border-white/10 bg-white/[0.04] text-slate-400 transition hover:bg-white/10 hover:text-white"
                        title={
                            collapsed
                                ? "Expand sidebar"
                                : "Collapse sidebar"
                        }
                        aria-label={
                            collapsed
                                ? "Expand sidebar"
                                : "Collapse sidebar"
                        }
                    >
                        {collapsed ? (
                            <ChevronRight className="h-4 w-4" />
                        ) : (
                            <ChevronLeft className="h-4 w-4" />
                        )}
                    </button>
                </div>

                <nav
                    className={`min-h-0 flex-1 overflow-y-auto overflow-x-hidden pb-5 ${collapsed ? "px-4 lg:px-3" : "px-4"
                        } [&::-webkit-scrollbar]:w-1 [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:rounded-full [&::-webkit-scrollbar-thumb]:bg-slate-700`}
                >
                    <p
                        className={`mb-3 px-3 text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500 ${collapsed ? "lg:hidden" : ""
                            }`}
                    >
                        Main Menu
                    </p>

                    <div className="space-y-1.5">
                        {allowedMenuItems.map((item) => (
                            <SidebarItem
                                key={item.path}
                                {...item}
                            />
                        ))}
                    </div>
                </nav>

                <div className="shrink-0 border-t border-white/[0.08] p-3">
                    <div className="lg:hidden">
                        <UserCard />
                    </div>

                    {collapsed && (
                        <div className="hidden space-y-2 lg:block">
                            <div
                                onMouseEnter={(event) =>
                                    showTooltip(
                                        event,
                                        user?.fullName || "Account"
                                    )
                                }
                                onMouseLeave={() =>
                                    setTooltip(null)
                                }
                                className="mx-auto flex h-11 w-11 items-center justify-center rounded-xl bg-blue-500/15 text-sm font-bold text-blue-300"
                            >
                                {firstLetter}
                            </div>

                            <button
                                type="button"
                                onClick={handleLogout}
                                onMouseEnter={(event) =>
                                    showTooltip(event, "Sign out")
                                }
                                onMouseLeave={() =>
                                    setTooltip(null)
                                }
                                aria-label="Sign out"
                                className="mx-auto flex h-11 w-11 cursor-pointer items-center justify-center rounded-xl text-slate-400 transition hover:bg-white/[0.06] hover:text-white"
                            >
                                <LogOut className="h-5 w-5" />
                            </button>
                        </div>
                    )}

                    {!collapsed && (
                        <div className="hidden lg:block">
                            <UserCard />
                        </div>
                    )}
                </div>
            </aside>

            <div
                className={`min-h-screen transition-[padding] duration-300 ease-in-out ${collapsed ? "lg:pl-20" : "lg:pl-72"
                    }`}
            >
                <header className="sticky top-0 z-30 flex h-20 items-center justify-between border-b border-slate-200 bg-white/95 px-5 backdrop-blur-xl sm:px-7 lg:px-8">
                    <div className="flex items-center gap-4">
                        <button
                            type="button"
                            onClick={() =>
                                setMobileSidebarOpen(true)
                            }
                            aria-label="Open menu"
                            className="flex h-10 w-10 cursor-pointer items-center justify-center rounded-xl text-slate-600 transition hover:bg-slate-100 lg:hidden"
                        >
                            <Menu className="h-6 w-6" />
                        </button>

                        <div>
                            <h1 className="text-lg font-semibold text-slate-950">
                                {schoolProfile?.name || "School Management"}
                            </h1>
                            <p className="hidden text-sm text-slate-500 sm:block">
                                Manage your school from one place
                            </p>
                        </div>
                    </div>

                    <div className="flex items-center gap-3">
                        <button
                            type="button"
                            aria-label="Notifications"
                            onClick={() => navigate("/notifications")}
                            className="relative flex h-11 w-11 cursor-pointer items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-600 transition hover:border-blue-200 hover:bg-blue-50 hover:text-blue-700"
                        >
                            <Bell className="h-5 w-5" />
                            {unreadCount > 0 && <span className="absolute -right-1 -top-1 rounded-full bg-blue-600 px-1.5 text-xs font-semibold text-white" aria-label={`${unreadCount} unread notifications`}>{unreadCount > 99 ? "99+" : unreadCount}</span>}
                        </button>

                        <StaffAccountMenu />
                    </div>
                </header>

                <main className="min-h-[calc(100vh-5rem)] px-5 pb-12 pt-8 sm:px-7 lg:px-8 xl:px-10">
                    <Outlet />
                </main>
            </div>
        </div>
    );
}
