import {
    ArrowLeft,
    Bell,
    BellRing,
    CheckCheck,
    Clock3,
    GraduationCap,
} from "lucide-react";

import {
    useEffect,
    useState,
} from "react";

import {
    useNavigate,
} from "react-router-dom";

import {
    studentPortalApi,
} from "../../api/studentPortalApi";


export default function StudentNotificationsPage() {

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
        actionLoading,
        setActionLoading,
    ] = useState(false);

    const [
        error,
        setError,
    ] = useState("");


    useEffect(() => {
        loadNotifications();
    }, []);


    const loadNotifications =
        async () => {

            try {

                setLoading(true);
                setError("");

                const response =
                    await studentPortalApi
                        .getNotifications();

                setData(
                    response.data
                );

            }
            catch (err) {

                console.error(
                    "Unable to load notifications:",
                    err
                );

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to load notifications."
                );

            }
            finally {

                setLoading(false);

            }

        };


    const handleNotificationClick =
        async (
            notification
        ) => {

            if (notification.isRead) {
                return;
            }

            try {

                await studentPortalApi
                    .markNotificationRead(
                        notification.id
                    );

                setData(
                    (current) => ({
                        ...current,

                        unreadCount:
                            Math.max(
                                0,
                                (current.unreadCount ?? 0) - 1
                            ),

                        notifications:
                            current.notifications.map(
                                (item) =>
                                    item.id ===
                                        notification.id
                                        ? {
                                            ...item,
                                            isRead: true,
                                        }
                                        : item
                            ),
                    })
                );

            }
            catch (err) {

                console.error(
                    "Unable to mark notification as read:",
                    err
                );

            }

        };


    const handleMarkAllRead =
        async () => {

            try {

                setActionLoading(true);

                await studentPortalApi
                    .markAllNotificationsRead();

                setData(
                    (current) => ({
                        ...current,

                        unreadCount: 0,

                        notifications:
                            current.notifications.map(
                                (item) => ({
                                    ...item,
                                    isRead: true,
                                })
                            ),
                    })
                );

            }
            catch (err) {

                setError(
                    err?.response
                        ?.data
                        ?.message ||
                    "Unable to update notifications."
                );

            }
            finally {

                setActionLoading(false);

            }

        };


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


            <main className="mx-auto w-full max-w-7xl px-5 py-8 sm:px-7 lg:px-8 lg:py-10">

                <button
                    type="button"
                    onClick={() =>
                        navigate(
                            "/student/dashboard"
                        )
                    }
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-slate-900"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to Dashboard
                </button>


                <div className="mt-6 flex flex-col justify-between gap-5 sm:flex-row sm:items-end">

                    <div>

                        <p className="text-sm font-semibold text-blue-600">
                            Updates
                        </p>

                        <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-950">
                            Notifications
                        </h1>

                        <p className="mt-2 text-sm text-slate-500">
                            View your school alerts and important updates.
                        </p>

                    </div>


                    {(data?.unreadCount ?? 0) > 0 && (

                        <button
                            type="button"
                            disabled={
                                actionLoading
                            }
                            onClick={
                                handleMarkAllRead
                            }
                            className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                        >

                            <CheckCheck className="h-4 w-4" />

                            {actionLoading
                                ? "Updating..."
                                : "Mark all as read"}

                        </button>

                    )}

                </div>


                {loading && (

                    <div className="mt-10 flex justify-center">

                        <div className="h-8 w-8 animate-spin rounded-full border-4 border-slate-200 border-t-blue-600" />

                    </div>

                )}


                {error && (

                    <div className="mt-8 rounded-xl border border-red-100 bg-red-50 p-4 text-sm text-red-700">
                        {error}
                    </div>

                )}


                {!loading &&
                    !error &&
                    data && (

                        <>

                            <div className="mt-8 grid gap-4 sm:grid-cols-3">

                                <SummaryCard
                                    icon={
                                        <Bell className="h-5 w-5" />
                                    }
                                    label="Total"
                                    value={
                                        data.totalNotifications ??
                                        0
                                    }
                                />

                                <SummaryCard
                                    icon={
                                        <BellRing className="h-5 w-5" />
                                    }
                                    label="Unread"
                                    value={
                                        data.unreadCount ??
                                        0
                                    }
                                />

                                <SummaryCard
                                    icon={
                                        <CheckCheck className="h-5 w-5" />
                                    }
                                    label="Read"
                                    value={
                                        (data.totalNotifications ?? 0) -
                                        (data.unreadCount ?? 0)
                                    }
                                />

                            </div>


                            {data.notifications?.length > 0 ? (

                                <div className="mt-6 space-y-3">

                                    {data.notifications.map(
                                        (
                                            notification
                                        ) => (

                                            <button
                                                key={
                                                    notification.id
                                                }
                                                type="button"
                                                onClick={() =>
                                                    handleNotificationClick(
                                                        notification
                                                    )
                                                }
                                                className={`w-full cursor-pointer rounded-2xl border p-5 text-left shadow-sm transition hover:border-blue-200 hover:shadow-md ${notification.isRead
                                                        ? "border-slate-200 bg-white"
                                                        : "border-blue-100 bg-blue-50/50"
                                                    }`}
                                            >

                                                <div className="flex gap-4">

                                                    <div
                                                        className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${notification.isRead
                                                                ? "bg-slate-100 text-slate-500"
                                                                : "bg-blue-100 text-blue-700"
                                                            }`}
                                                    >
                                                        <BellRing className="h-5 w-5" />
                                                    </div>


                                                    <div className="min-w-0 flex-1">

                                                        <div className="flex flex-wrap items-center justify-between gap-3">

                                                            <div className="flex items-center gap-2">

                                                                <h2 className="font-semibold text-slate-950">
                                                                    {
                                                                        notification.title
                                                                    }
                                                                </h2>

                                                                {!notification.isRead && (

                                                                    <span className="h-2 w-2 rounded-full bg-blue-600" />

                                                                )}

                                                            </div>


                                                            <div className="inline-flex items-center gap-2 text-xs text-slate-500">

                                                                <Clock3 className="h-3.5 w-3.5" />

                                                                {
                                                                    formatDate(
                                                                        notification.createdAt
                                                                    )
                                                                }

                                                            </div>

                                                        </div>


                                                        <p className="mt-2 text-sm leading-6 text-slate-600">
                                                            {
                                                                notification.message
                                                            }
                                                        </p>


                                                        <div className="mt-3 flex flex-wrap gap-2">

                                                            <span className="rounded-full bg-slate-100 px-2.5 py-1 text-[11px] font-semibold text-slate-600">
                                                                {
                                                                    formatType(
                                                                        notification.type
                                                                    )
                                                                }
                                                            </span>

                                                            {!notification.isRead && (

                                                                <span className="text-xs font-semibold text-blue-600">
                                                                    Click to mark as read
                                                                </span>

                                                            )}

                                                        </div>

                                                    </div>

                                                </div>

                                            </button>

                                        )
                                    )}

                                </div>

                            ) : (

                                <div className="mt-6 rounded-2xl border border-slate-200 bg-white p-12 text-center shadow-sm">

                                    <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-xl bg-slate-100 text-slate-400">
                                        <Bell className="h-7 w-7" />
                                    </div>

                                    <h2 className="mt-4 font-semibold text-slate-950">
                                        No notifications
                                    </h2>

                                    <p className="mt-2 text-sm text-slate-500">
                                        You do not have any notifications at the moment.
                                    </p>

                                </div>

                            )}

                        </>

                    )}

            </main>

        </div>

    );

}


function SummaryCard({
    icon,
    label,
    value,
}) {

    return (

        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">

            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-blue-600">
                {icon}
            </div>

            <p className="mt-4 text-xs font-medium uppercase tracking-wide text-slate-500">
                {label}
            </p>

            <p className="mt-1 text-xl font-bold text-slate-950">
                {value}
            </p>

        </div>

    );

}


function formatType(
    value
) {

    if (!value) {
        return "Notification";
    }

    return value
        .replace(
            /([a-z])([A-Z])/g,
            "$1 $2"
        );
}


function formatDate(
    value
) {

    if (!value) {
        return "—";
    }

    const date =
        new Date(value);

    return date.toLocaleString(
        undefined,
        {
            year: "numeric",
            month: "short",
            day: "numeric",
            hour: "numeric",
            minute: "2-digit",
        }
    );

}