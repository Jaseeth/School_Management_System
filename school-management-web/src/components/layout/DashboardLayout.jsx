import {
    Bell,
    BookOpen,
    CalendarDays,
    ChevronDown,
    ChevronLeft,
    ChevronRight,
    ClipboardCheck,
    FileText,
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

import {
    useEffect,
    useState,
} from "react";

import {
    NavLink,
    Outlet,
    useNavigate,
} from "react-router-dom";

import { useAuth } from "../../context/AuthContext";

export default function DashboardLayout() {
    const navigate = useNavigate();

    const {
        user,
        logout,
    } = useAuth();

    // ============================================================
    // SIDEBAR STATE
    // ============================================================

    const [
        mobileSidebarOpen,
        setMobileSidebarOpen,
    ] = useState(false);

    const [
        collapsed,
        setCollapsed,
    ] = useState(() => {
        return (
            localStorage.getItem(
                "sidebarCollapsed"
            ) === "true"
        );
    });

    const [
        tooltip,
        setTooltip,
    ] = useState(null);

    useEffect(() => {
        localStorage.setItem(
            "sidebarCollapsed",
            collapsed.toString()
        );

        setTooltip(null);
    }, [collapsed]);

    // ============================================================
    // ROLE CHECK
    // ============================================================

    const hasAnyRole = (roles) => {
        if (
            !roles ||
            roles.length === 0
        ) {
            return true;
        }

        return roles.some(
            (role) =>
                user?.roles?.includes(
                    role
                )
        );
    };

    // ============================================================
    // MENU
    // ============================================================

    const menuItems = [
        {
            title: "Dashboard",
            path: "/dashboard",
            icon: LayoutDashboard,
        },

        {
            title: "Students",
            path: "/students",
            icon: GraduationCap,
            roles: [
                "Admin",
                "Principal",
                "Deputy Principal",
                "Section Head",
            ],
        },

        {
            title: "Children",
            path: "/parent/children",
            icon: GraduationCap,
            roles: [
                "Parent",
            ],
        },

        {
            title: "Parents",
            path: "/parents",
            icon: UserRound,
            roles: [
                "Admin",
                "Principal",
                "Deputy Principal",
            ],
        },

        {
            title: "Staff",
            path: "/staff",
            icon: Users,
            roles: [
                "Admin",
                "Principal",
                "Deputy Principal",
                "Section Head",
            ],
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
                "Teacher",
                "Parent",
            ],
        },

        {
            title: "Results",
            path: "/results",
            icon: BookOpen,
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
            title: "Timetable",
            path: "/timetable",
            icon: CalendarDays,
            roles: [
                "Admin",
                "Principal",
                "Deputy Principal",
                "Section Head",
                "Teacher",
                "Student",
            ],
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
            title: "Notifications",
            path: "/notifications",
            icon: Bell,
            roles: [
                "Teacher",
                "Student",
                "Parent",
            ],
        },

        {
            title: "Reports",
            path: "/reports",
            icon: FileText,
            roles: [
                "Admin",
                "Principal",
                "Deputy Principal",
                "Section Head",
            ],
        },

        {
            title: "Audit Logs",
            path: "/audit-logs",
            icon: ShieldCheck,
            roles: [
                "Admin",
                "Principal",
                "Deputy Principal",
            ],
        },
    ];

    const allowedMenuItems =
        menuItems.filter(
            (item) =>
                hasAnyRole(
                    item.roles
                )
        );

    // ============================================================
    // USER
    // ============================================================

    const primaryRole =
        user?.roles?.[0] ??
        "";

    const firstLetter =
        user?.fullName
            ?.charAt(0)
            ?.toUpperCase() ??
        "U";

    const handleLogout = () => {
        logout();

        navigate(
            "/login"
        );
    };

    // ============================================================
    // TOOLTIP
    // Desktop collapsed sidebar only
    // ============================================================

    const showTooltip = (
        event,
        title
    ) => {
        if (
            !collapsed ||
            window.innerWidth < 1024
        ) {
            return;
        }

        const rect =
            event.currentTarget
                .getBoundingClientRect();

        setTooltip({
            title,
            top:
                rect.top +
                rect.height / 2,
        });
    };

    const hideTooltip = () => {
        setTooltip(null);
    };

    // ============================================================
    // SIDEBAR ITEM
    // ============================================================

    const SidebarItem = ({
        title,
        path,
        icon: Icon,
    }) => {
        return (
            <NavLink
                to={path}

                onMouseEnter={(
                    event
                ) =>
                    showTooltip(
                        event,
                        title
                    )
                }

                onMouseLeave={
                    hideTooltip
                }

                onClick={() => {
                    setMobileSidebarOpen(
                        false
                    );

                    setTooltip(
                        null
                    );
                }}

                className={({
                    isActive,
                }) =>
                    `
                    flex
                    h-11
                    items-center
                    gap-3
                    rounded-xl
                    px-3
                    text-sm
                    font-medium
                    transition-all
                    duration-200

                    ${collapsed
                        ? "lg:justify-center lg:gap-0 lg:px-0"
                        : ""
                    }

                    ${isActive
                        ? "bg-blue-600 text-white shadow-md shadow-blue-950/20"
                        : "text-slate-400 hover:bg-white/[0.06] hover:text-white"
                    }
                    `
                }
            >
                <Icon
                    className="
                        h-5
                        w-5
                        shrink-0
                    "
                />

                <span
                    className={`
                        truncate

                        ${collapsed
                            ? "lg:hidden"
                            : ""
                        }
                    `}
                >
                    {title}
                </span>
            </NavLink>
        );
    };

    return (
        <div
            className="
                min-h-screen
                overflow-x-hidden
                bg-slate-50
            "
        >

            {/* ====================================================
                DESKTOP COLLAPSED TOOLTIP
            ==================================================== */}

            {collapsed &&
                tooltip && (
                    <div
                        className="
                            pointer-events-none
                            fixed
                            z-[9999]
                            hidden
                            -translate-y-1/2
                            rounded-lg
                            bg-slate-900
                            px-3
                            py-2
                            text-xs
                            font-medium
                            text-white
                            shadow-xl
                            lg:block
                        "
                        style={{
                            left: "88px",
                            top:
                                `${tooltip.top}px`,
                        }}
                    >
                        {
                            tooltip.title
                        }

                        <span
                            className="
                                absolute
                                right-full
                                top-1/2
                                -translate-y-1/2
                                border-[5px]
                                border-transparent
                                border-r-slate-900
                            "
                        />
                    </div>
                )}

            {/* ====================================================
                MOBILE OVERLAY
            ==================================================== */}

            {mobileSidebarOpen && (
                <div
                    className="
                        fixed
                        inset-0
                        z-40
                        bg-slate-950/55
                        backdrop-blur-sm
                        lg:hidden
                    "
                    onClick={() =>
                        setMobileSidebarOpen(
                            false
                        )
                    }
                />
            )}

            {/* ====================================================
                SIDEBAR
            ==================================================== */}

            <aside
                className={`
                    fixed
                    inset-y-0
                    left-0
                    z-50

                    flex
                    w-72
                    flex-col

                    overflow-hidden

                    bg-slate-950

                    shadow-2xl
                    shadow-slate-950/20

                    transition-[width,transform]
                    duration-300
                    ease-in-out

                    ${mobileSidebarOpen
                        ? "translate-x-0"
                        : "-translate-x-full"
                    }

                    lg:translate-x-0

                    ${collapsed
                        ? "lg:w-20"
                        : "lg:w-72"
                    }
                `}
            >

                {/* =================================================
                    BRAND
                ================================================= */}

                <div
                    className="
                        flex
                        h-20
                        shrink-0
                        items-center
                        justify-between

                        border-b
                        border-white/[0.08]

                        px-5
                    "
                >

                    <div
                        className="
                            flex
                            min-w-0
                            items-center
                            gap-3
                        "
                    >

                        <div
                            className="
                                flex
                                h-11
                                w-11
                                shrink-0
                                items-center
                                justify-center

                                rounded-xl

                                bg-blue-600

                                shadow-md
                                shadow-blue-950/30
                            "
                        >
                            <School
                                className="
                                    h-6
                                    w-6
                                    text-white
                                "
                            />
                        </div>

                        {/* MOBILE ALWAYS SHOWS TEXT
                            DESKTOP HIDES WHEN COLLAPSED */}

                        <div
                            className={`
                                min-w-0

                                ${collapsed
                                    ? "lg:hidden"
                                    : ""
                                }
                            `}
                        >
                            <p
                                className="
                                    truncate
                                    text-sm
                                    font-semibold
                                    text-white
                                "
                            >
                                School Management
                            </p>

                            <p
                                className="
                                    truncate
                                    text-xs
                                    text-slate-400
                                "
                            >
                                Education System
                            </p>
                        </div>

                    </div>

                    {/* MOBILE CLOSE */}

                    <button
                        type="button"

                        onClick={() =>
                            setMobileSidebarOpen(
                                false
                            )
                        }

                        className="
                            ml-auto

                            flex
                            h-9
                            w-9
                            items-center
                            justify-center

                            rounded-lg

                            text-slate-400

                            transition

                            hover:bg-white/10
                            hover:text-white

                            lg:hidden
                        "
                    >
                        <X
                            className="
                                h-5
                                w-5
                            "
                        />
                    </button>

                </div>

                {/* =================================================
                    DESKTOP COLLAPSE BUTTON
                ================================================= */}

                <div
                    className={`
                        hidden
                        shrink-0
                        py-3
                        lg:flex

                        ${collapsed
                            ? "justify-center"
                            : "justify-end px-4"
                        }
                    `}
                >
                    <button
                        type="button"

                        onClick={() =>
                            setCollapsed(
                                (current) =>
                                    !current
                            )
                        }

                        className="
                            flex
                            h-8
                            w-8
                            items-center
                            justify-center

                            rounded-lg

                            border
                            border-white/10

                            bg-white/[0.04]

                            text-slate-400

                            transition

                            hover:bg-white/10
                            hover:text-white
                        "

                        title={
                            collapsed
                                ? "Expand sidebar"
                                : "Collapse sidebar"
                        }
                    >
                        {collapsed ? (
                            <ChevronRight
                                className="
                                    h-4
                                    w-4
                                "
                            />
                        ) : (
                            <ChevronLeft
                                className="
                                    h-4
                                    w-4
                                "
                            />
                        )}
                    </button>
                </div>

                {/* =================================================
                    MENU
                ================================================= */}

                <nav
                    className={`
                        min-h-0
                        flex-1

                        overflow-y-auto
                        overflow-x-hidden

                        pb-5

                        ${collapsed
                            ? "px-4 lg:px-3"
                            : "px-4"
                        }

                        [&::-webkit-scrollbar]:w-1

                        [&::-webkit-scrollbar-track]:bg-transparent

                        [&::-webkit-scrollbar-thumb]:rounded-full

                        [&::-webkit-scrollbar-thumb]:bg-slate-700
                    `}
                >

                    {/* MOBILE ALWAYS SHOWS TITLE */}

                    <p
                        className={`
                            mb-3
                            px-3

                            text-[11px]
                            font-semibold
                            uppercase
                            tracking-[0.16em]
                            text-slate-500

                            ${collapsed
                                ? "lg:hidden"
                                : ""
                            }
                        `}
                    >
                        Main Menu
                    </p>

                    <div
                        className="
                            space-y-1.5
                        "
                    >
                        {
                            allowedMenuItems.map(
                                (item) => (
                                    <SidebarItem
                                        key={
                                            item.path
                                        }
                                        {...item}
                                    />
                                )
                            )
                        }
                    </div>

                </nav>

                {/* =================================================
                    USER AREA
                ================================================= */}

                <div
                    className="
                        shrink-0

                        border-t
                        border-white/[0.08]

                        p-3
                    "
                >

                    {/* =============================================
                        MOBILE USER CARD
                        Always full, regardless of collapsed state
                    ============================================= */}

                    <div
                        className="
                            rounded-xl
                            bg-white/[0.05]
                            p-4
                            lg:hidden
                        "
                    >

                        <div
                            className="
                                flex
                                items-center
                                gap-3
                            "
                        >

                            <div
                                className="
                                    flex
                                    h-10
                                    w-10
                                    shrink-0
                                    items-center
                                    justify-center

                                    rounded-xl

                                    bg-blue-500/15

                                    text-sm
                                    font-bold
                                    text-blue-300
                                "
                            >
                                {
                                    firstLetter
                                }
                            </div>

                            <div
                                className="
                                    min-w-0
                                    flex-1
                                "
                            >

                                <p
                                    className="
                                        truncate
                                        text-sm
                                        font-semibold
                                        text-white
                                    "
                                >
                                    {
                                        user?.fullName
                                    }
                                </p>

                                <p
                                    className="
                                        mt-0.5
                                        truncate
                                        text-xs
                                        text-slate-400
                                    "
                                >
                                    {
                                        user?.email
                                    }
                                </p>

                            </div>

                        </div>

                        <div
                            className="
                                mt-3
                            "
                        >
                            <span
                                className="
                                    inline-flex

                                    rounded-md

                                    bg-blue-500/10

                                    px-2.5
                                    py-1

                                    text-xs
                                    font-medium
                                    text-blue-300
                                "
                            >
                                {
                                    primaryRole
                                }
                            </span>
                        </div>

                        <button
                            type="button"

                            onClick={
                                handleLogout
                            }

                            className="
                                mt-4

                                flex
                                w-full
                                items-center
                                justify-center
                                gap-2

                                rounded-lg

                                border
                                border-white/10

                                px-3
                                py-2

                                text-sm
                                text-slate-300

                                transition

                                hover:bg-white/[0.07]
                                hover:text-white
                            "
                        >
                            <LogOut
                                className="
                                    h-4
                                    w-4
                                "
                            />

                            Sign out
                        </button>

                    </div>

                    {/* =============================================
                        DESKTOP COLLAPSED USER
                    ============================================= */}

                    {collapsed && (
                        <div
                            className="
                                hidden
                                space-y-2
                                lg:block
                            "
                        >

                            <button
                                type="button"

                                onMouseEnter={(
                                    event
                                ) =>
                                    showTooltip(
                                        event,
                                        user?.fullName ||
                                        "Account"
                                    )
                                }

                                onMouseLeave={
                                    hideTooltip
                                }

                                className="
                                    mx-auto

                                    flex
                                    h-11
                                    w-11
                                    items-center
                                    justify-center

                                    rounded-xl

                                    bg-blue-500/15

                                    text-sm
                                    font-bold
                                    text-blue-300

                                    transition

                                    hover:bg-blue-500/20
                                "
                            >
                                {
                                    firstLetter
                                }
                            </button>

                            <button
                                type="button"

                                onMouseEnter={(
                                    event
                                ) =>
                                    showTooltip(
                                        event,
                                        "Sign out"
                                    )
                                }

                                onMouseLeave={
                                    hideTooltip
                                }

                                onClick={
                                    handleLogout
                                }

                                className="
                                    mx-auto

                                    flex
                                    h-11
                                    w-11
                                    items-center
                                    justify-center

                                    rounded-xl

                                    text-slate-400

                                    transition

                                    hover:bg-white/[0.06]
                                    hover:text-white
                                "
                            >
                                <LogOut
                                    className="
                                        h-5
                                        w-5
                                    "
                                />
                            </button>

                        </div>
                    )}

                    {/* =============================================
                        DESKTOP EXPANDED USER
                    ============================================= */}

                    {!collapsed && (
                        <div
                            className="
                                hidden
                                rounded-xl
                                bg-white/[0.05]
                                p-4
                                lg:block
                            "
                        >

                            <div
                                className="
                                    flex
                                    items-center
                                    gap-3
                                "
                            >

                                <div
                                    className="
                                        flex
                                        h-10
                                        w-10
                                        shrink-0
                                        items-center
                                        justify-center

                                        rounded-xl

                                        bg-blue-500/15

                                        text-sm
                                        font-bold
                                        text-blue-300
                                    "
                                >
                                    {
                                        firstLetter
                                    }
                                </div>

                                <div
                                    className="
                                        min-w-0
                                    "
                                >

                                    <p
                                        className="
                                            truncate
                                            text-sm
                                            font-semibold
                                            text-white
                                        "
                                    >
                                        {
                                            user?.fullName
                                        }
                                    </p>

                                    <p
                                        className="
                                            mt-0.5
                                            truncate
                                            text-xs
                                            text-slate-400
                                        "
                                    >
                                        {
                                            user?.email
                                        }
                                    </p>

                                </div>

                            </div>

                            <div
                                className="
                                    mt-3
                                "
                            >
                                <span
                                    className="
                                        inline-flex

                                        rounded-md

                                        bg-blue-500/10

                                        px-2.5
                                        py-1

                                        text-xs
                                        font-medium
                                        text-blue-300
                                    "
                                >
                                    {
                                        primaryRole
                                    }
                                </span>
                            </div>

                            <button
                                type="button"

                                onClick={
                                    handleLogout
                                }

                                className="
                                    mt-4

                                    flex
                                    w-full
                                    items-center
                                    justify-center
                                    gap-2

                                    rounded-lg

                                    border
                                    border-white/10

                                    px-3
                                    py-2

                                    text-sm
                                    text-slate-300

                                    transition

                                    hover:bg-white/[0.07]
                                    hover:text-white
                                "
                            >
                                <LogOut
                                    className="
                                        h-4
                                        w-4
                                    "
                                />

                                Sign out
                            </button>

                        </div>
                    )}

                </div>

            </aside>

            {/* ====================================================
                MAIN AREA
            ==================================================== */}

            <div
                className={`
                    min-h-screen

                    transition-[padding]
                    duration-300
                    ease-in-out

                    ${collapsed
                        ? "lg:pl-20"
                        : "lg:pl-72"
                    }
                `}
            >

                {/* =================================================
                    HEADER
                ================================================= */}

                <header
                    className="
                        sticky
                        top-0
                        z-30

                        flex
                        h-20
                        items-center
                        justify-between

                        border-b
                        border-slate-200

                        bg-white/95

                        px-5

                        backdrop-blur-xl

                        sm:px-7
                        lg:px-8
                    "
                >

                    {/* LEFT */}

                    <div
                        className="
                            flex
                            items-center
                            gap-4
                        "
                    >

                        {/* MOBILE MENU */}

                        <button
                            type="button"

                            onClick={() =>
                                setMobileSidebarOpen(
                                    true
                                )
                            }

                            className="
                                flex
                                h-10
                                w-10
                                items-center
                                justify-center

                                rounded-xl

                                text-slate-600

                                transition

                                hover:bg-slate-100

                                lg:hidden
                            "
                        >
                            <Menu
                                className="
                                    h-6
                                    w-6
                                "
                            />
                        </button>

                        <div>

                            <h1
                                className="
                                    text-lg
                                    font-semibold
                                    text-slate-950
                                "
                            >
                                School Management
                            </h1>

                            <p
                                className="
                                    hidden
                                    text-sm
                                    text-slate-500
                                    sm:block
                                "
                            >
                                Manage your school from one place
                            </p>

                        </div>

                    </div>

                    {/* RIGHT */}

                    <div
                        className="
                            flex
                            items-center
                            gap-3
                        "
                    >

                        {/* NOTIFICATION */}

                        <button
                            type="button"

                            className="
                                relative

                                flex
                                h-11
                                w-11
                                items-center
                                justify-center

                                rounded-xl

                                border
                                border-slate-200

                                bg-white

                                text-slate-600

                                transition

                                hover:border-blue-200
                                hover:bg-blue-50
                                hover:text-blue-700
                            "
                        >
                            <Bell
                                className="
                                    h-5
                                    w-5
                                "
                            />

                            <span
                                className="
                                    absolute
                                    right-2.5
                                    top-2.5

                                    h-2
                                    w-2

                                    rounded-full

                                    bg-blue-600
                                "
                            />
                        </button>

                        {/* USER */}

                        <button
                            type="button"

                            className="
                                hidden
                                items-center
                                gap-3

                                rounded-xl

                                border
                                border-slate-200

                                bg-white

                                px-3
                                py-2

                                transition

                                hover:bg-slate-50

                                sm:flex
                            "
                        >

                            <div
                                className="
                                    flex
                                    h-9
                                    w-9
                                    items-center
                                    justify-center

                                    rounded-lg

                                    bg-blue-50

                                    text-sm
                                    font-bold
                                    text-blue-700
                                "
                            >
                                {
                                    firstLetter
                                }
                            </div>

                            <div
                                className="
                                    max-w-40
                                    text-left
                                "
                            >

                                <p
                                    className="
                                        truncate
                                        text-sm
                                        font-semibold
                                        text-slate-900
                                    "
                                >
                                    {
                                        user?.fullName
                                    }
                                </p>

                                <p
                                    className="
                                        truncate
                                        text-xs
                                        text-slate-500
                                    "
                                >
                                    {
                                        primaryRole
                                    }
                                </p>

                            </div>

                            <ChevronDown
                                className="
                                    h-4
                                    w-4
                                    text-slate-400
                                "
                            />

                        </button>

                    </div>

                </header>

                {/* =================================================
                    PAGE CONTENT
                ================================================= */}

                <main
                    className="
                        min-h-[calc(100vh-5rem)]

                        px-5
                        pb-12
                        pt-8

                        sm:px-7
                        sm:pt-8

                        lg:px-8
                        lg:pt-8

                        xl:px-10
                    "
                >
                    <Outlet />
                </main>

            </div>

        </div>
    );
}