import {
    ArrowLeft,
    Bell,
    BellRing,
    Clock3,
    GraduationCap,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { parentPortalApi } from "../../api/parentPortalApi";

export default function ParentNotificationsPage() {
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [pendingId, setPendingId] = useState(null);
    const [error, setError] = useState("");

    useEffect(() => {
        let active = true;

        parentPortalApi.getNotifications()
            .then(({ data: response }) => {
                if (active) setData(response);
            })
            .catch((err) => {
                if (active) {
                    setError(
                        err?.response?.data?.message ||
                        "Unable to load notifications."
                    );
                }
            })
            .finally(() => {
                if (active) setLoading(false);
            });

        return () => {
            active = false;
        };
    }, []);

    async function markRead(notification) {
        if (notification.isRead || pendingId !== null) return;

        setPendingId(notification.id);
        setError("");

        try {
            const { data: response } =
                await parentPortalApi.markNotificationRead(
                    notification.id
                );

            setData((current) => ({
                ...current,
                notifications: current.notifications.map((item) =>
                    item.id === notification.id
                        ? {
                            ...item,
                            isRead: response.isRead,
                            readAt: response.readAt,
                        }
                        : item
                ),
            }));
        } catch (err) {
            setError(
                err?.response?.data?.message ||
                "Unable to mark the notification as read."
            );
        } finally {
            setPendingId(null);
        }
    }

    const notifications = data?.notifications ?? [];
    const unreadCount = notifications.filter(
        (item) => !item.isRead
    ).length;

    return (
        <div className="min-h-screen bg-slate-50">
            <header className="border-b border-slate-200 bg-white">
                <div className="mx-auto flex max-w-6xl items-center gap-3 px-5 py-4">
                    <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-white">
                        <GraduationCap className="h-6 w-6" />
                    </span>
                    <div>
                        <p className="font-bold text-slate-950">
                            School Management
                        </p>
                        <p className="text-xs text-slate-500">
                            Parent Portal
                        </p>
                    </div>
                </div>
            </header>

            <main className="mx-auto max-w-6xl px-5 py-9">
                <Link
                    to="/parent/dashboard"
                    className="inline-flex cursor-pointer items-center gap-2 text-sm font-semibold text-slate-600 hover:text-blue-700"
                >
                    <ArrowLeft className="h-4 w-4" />
                    Back to My Children
                </Link>

                <div className="mt-7 flex items-center gap-3 text-blue-600">
                    <Bell className="h-6 w-6" />
                    <span className="text-sm font-semibold">
                        Parent Updates
                    </span>
                </div>

                <h1 className="mt-2 text-3xl font-bold text-slate-950">
                    Notifications
                </h1>

                <p className="mt-2 text-sm text-slate-600">
                    Latest notifications sent to your parent account.
                </p>

                {loading && (
                    <p role="status" className="mt-8 text-slate-600">
                        Loading notifications...
                    </p>
                )}

                {error && (
                    <p
                        role="alert"
                        className="mt-7 rounded-xl border border-red-100 bg-red-50 p-4 text-sm text-red-700"
                    >
                        {error}
                    </p>
                )}

                {!loading && data && (
                    <>
                        <div className="mt-7 grid gap-4 sm:grid-cols-2">
                            <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                                <p className="text-sm text-slate-500">
                                    Recent notifications
                                </p>
                                <p className="mt-2 text-2xl font-bold text-slate-950">
                                    {data.count ?? notifications.length}
                                </p>
                            </div>

                            <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                                <p className="text-sm text-slate-500">
                                    Unread in recent notifications
                                </p>
                                <p className="mt-2 text-2xl font-bold text-slate-950">
                                    {unreadCount}
                                </p>
                            </div>
                        </div>

                        {notifications.length ? (
                            <div className="mt-6 space-y-3">
                                {notifications.map((notification) => (
                                    <article
                                        key={notification.id}
                                        className={`rounded-2xl border p-5 shadow-sm ${notification.isRead
                                                ? "border-slate-200 bg-white"
                                                : "border-blue-200 bg-blue-50/60"
                                            }`}
                                    >
                                        <div className="flex items-start gap-4">
                                            <span
                                                className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${notification.isRead
                                                        ? "bg-slate-100 text-slate-500"
                                                        : "bg-blue-100 text-blue-700"
                                                    }`}
                                            >
                                                <BellRing className="h-5 w-5" />
                                            </span>

                                            <div className="min-w-0 flex-1">
                                                <div className="flex flex-wrap items-start justify-between gap-2">
                                                    <h2 className="font-semibold text-slate-950">
                                                        {notification.title}
                                                    </h2>

                                                    <span className="inline-flex items-center gap-1 text-xs text-slate-500">
                                                        <Clock3 className="h-3.5 w-3.5" />
                                                        {formatDate(
                                                            notification.createdAt
                                                        )}
                                                    </span>
                                                </div>

                                                <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-6 text-slate-700">
                                                    {notification.message}
                                                </p>

                                                <div className="mt-3 flex flex-wrap items-center gap-3">
                                                    <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-slate-600">
                                                        {formatType(
                                                            notification.type
                                                        )}
                                                    </span>

                                                    {notification.isRead ? (
                                                        <span className="text-xs font-semibold text-slate-500">
                                                            Read
                                                        </span>
                                                    ) : (
                                                        <button
                                                            type="button"
                                                            disabled={
                                                                pendingId !==
                                                                null
                                                            }
                                                            onClick={() =>
                                                                markRead(
                                                                    notification
                                                                )
                                                            }
                                                            className="cursor-pointer rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                                                        >
                                                            {pendingId ===
                                                                notification.id
                                                                ? "Updating..."
                                                                : "Mark as read"}
                                                        </button>
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                    </article>
                                ))}
                            </div>
                        ) : (
                            <p className="mt-6 rounded-2xl border border-slate-200 bg-white p-8 text-center text-slate-600">
                                You have no notifications yet.
                            </p>
                        )}
                    </>
                )}
            </main>
        </div>
    );
}

function formatDate(value) {
    if (!value) return "—";

    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "—"
        : date.toLocaleString();
}

function formatType(value) {
    return value
        ? value.replace(/([a-z])([A-Z])/g, "$1 $2")
        : "Notification";
}