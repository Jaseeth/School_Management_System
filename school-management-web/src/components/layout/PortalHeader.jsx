import { useEffect, useId, useRef, useState } from "react";
import { ChevronDown, GraduationCap, LogOut, UserRound } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { disableBrowserPush } from "../../services/browserPush";

export default function PortalHeader({ portal }) {
    const navigate = useNavigate();
    const [busy, setBusy] = useState(false);
    const [open, setOpen] = useState(false);
    const container = useRef(null);
    const trigger = useRef(null);
    const menu = useRef(null);
    const menuId = useId();
    const role = portal === "student" ? "Student" : "Parent";
    let user;
    try { user = JSON.parse(localStorage.getItem(`${portal}User`) || "null"); }
    catch { user = null; }

    useEffect(() => {
        if (!open) return;
        menu.current?.querySelector("button")?.focus();
        const outside = event => {
            if (!container.current?.contains(event.target)) setOpen(false);
        };
        const escape = event => {
            if (event.key === "Escape") {
                setOpen(false);
                trigger.current?.focus();
            }
        };
        document.addEventListener("pointerdown", outside);
        document.addEventListener("keydown", escape);
        return () => {
            document.removeEventListener("pointerdown", outside);
            document.removeEventListener("keydown", escape);
        };
    }, [open]);

    async function signOut() {
        if (busy) return;
        setBusy(true);
        await disableBrowserPush(portal).catch(() => {});
        localStorage.removeItem(`${portal}AccessToken`);
        localStorage.removeItem(`${portal}User`);
        navigate(`/${portal}/login`, { replace: true });
    }

    return (
        <header className="border-b border-slate-200 bg-white">
            <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-5 py-4 sm:px-7 lg:px-8">
                <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-blue-600 text-white">
                        <GraduationCap className="h-5 w-5" aria-hidden="true" />
                    </div>
                    <div>
                        <p className="font-bold text-slate-950">School Management</p>
                        <p className="text-xs text-slate-500">{portal === "student" ? "Student" : "Parent"} Portal</p>
                    </div>
                </div>
                <div className="relative" ref={container} onBlur={event => {
                    if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false);
                }}>
                    <button type="button" ref={trigger} aria-label="Account menu" aria-haspopup="menu"
                        aria-expanded={open} aria-controls={open ? menuId : undefined}
                        onClick={() => setOpen(value => !value)}
                        className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-2 py-2 transition hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-blue-600 sm:gap-3 sm:px-3">
                        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-50 text-sm font-bold text-blue-700">
                            {user?.fullName?.charAt(0)?.toUpperCase() || role.charAt(0)}
                        </span>
                        <span className="hidden max-w-40 text-left sm:block">
                            <span className="block truncate text-sm font-semibold text-slate-900">{user?.fullName || "My profile"}</span>
                            <span className="block text-xs text-slate-500">{role}</span>
                        </span>
                        <ChevronDown className="h-4 w-4 text-slate-400" aria-hidden="true" />
                    </button>
                    {open && <div id={menuId} ref={menu} role="menu" aria-label="Account menu"
                        onKeyDown={event => {
                            if (!["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) return;
                            event.preventDefault();
                            const items = [...event.currentTarget.querySelectorAll("button:not(:disabled)")];
                            const index = items.indexOf(document.activeElement);
                            const next = event.key === "Home" ? 0 : event.key === "End" ? items.length - 1
                                : (index + (event.key === "ArrowDown" ? 1 : -1) + items.length) % items.length;
                            items[next]?.focus();
                        }}
                        className="absolute right-0 z-50 mt-2 w-52 rounded-xl border border-slate-200 bg-white p-1.5 shadow-lg">
                        <button type="button" role="menuitem" onClick={() => {
                            setOpen(false);
                            navigate(`/${portal}/profile`);
                        }} className="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50 focus-visible:bg-slate-50">
                            <UserRound className="h-4 w-4" aria-hidden="true" />My profile
                        </button>
                        <button type="button" role="menuitem" onClick={signOut} disabled={busy}
                            className="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50 focus-visible:bg-slate-50 disabled:opacity-50">
                            <LogOut className="h-4 w-4" aria-hidden="true" />{busy ? "Signing out…" : "Sign out"}
                        </button>
                    </div>}
                </div>
            </div>
        </header>
    );
}
