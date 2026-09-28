import { ChevronDown, LogOut, UserRound } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../context/AuthContext";

export default function StaffAccountMenu() {
    const { user, logout } = useAuth();
    const navigate = useNavigate();
    const [open, setOpen] = useState(false);
    const containerRef = useRef(null);

    useEffect(() => {
        if (!open) return;

        const closeOutside = (event) => {
            if (!containerRef.current?.contains(event.target)) {
                setOpen(false);
            }
        };

        const closeEscape = (event) => {
            if (event.key === "Escape") setOpen(false);
        };

        document.addEventListener("pointerdown", closeOutside);
        document.addEventListener("keydown", closeEscape);

        return () => {
            document.removeEventListener("pointerdown", closeOutside);
            document.removeEventListener("keydown", closeEscape);
        };
    }, [open]);

    const signOut = () => {
        setOpen(false);
        logout();
        navigate("/login", { replace: true });
    };

    return (
        <div className="relative" ref={containerRef}>
            <button
                type="button"
                aria-haspopup="menu"
                aria-expanded={open}
                aria-label="Account menu"
                onClick={() => setOpen((current) => !current)}
                className="flex cursor-pointer items-center gap-2 rounded-xl border border-slate-200 bg-white px-2 py-2 transition hover:bg-slate-50 sm:gap-3 sm:px-3"
            >
                <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-50 text-sm font-bold text-blue-700">
                    {user?.fullName?.charAt(0)?.toUpperCase() || "U"}
                </span>

                <span className="hidden max-w-40 text-left sm:block">
                    <span className="block truncate text-sm font-semibold text-slate-900">
                        {user?.fullName}
                    </span>
                    <span className="block truncate text-xs text-slate-500">
                        {user?.roles?.[0]}
                    </span>
                </span>

                <ChevronDown className="hidden h-4 w-4 text-slate-400 sm:block" />
            </button>

            {open && (
                <div
                    role="menu"
                    className="absolute right-0 z-50 mt-2 w-52 rounded-xl border border-slate-200 bg-white p-1.5 shadow-lg"
                >
                    <button
                        type="button"
                        role="menuitem"
                        onClick={() => {
                            setOpen(false);
                            navigate("/staff/my-profile");
                        }}
                        className="flex w-full cursor-pointer items-center gap-2 rounded-lg px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50"
                    >
                        <UserRound className="h-4 w-4" />
                        My Profile
                    </button>

                    <button
                        type="button"
                        role="menuitem"
                        onClick={signOut}
                        className="flex w-full cursor-pointer items-center gap-2 rounded-lg px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50"
                    >
                        <LogOut className="h-4 w-4" />
                        Sign out
                    </button>
                </div>
            )}
        </div>
    );
}