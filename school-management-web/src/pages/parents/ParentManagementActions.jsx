import { useRef, useState } from "react";
import { Eye, EyeOff } from "lucide-react";
import { parentManagementApi } from "../../api/parentManagementApi";

const buttonClass =
    "cursor-pointer rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50";

const primaryButtonClass =
    "cursor-pointer rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50";

export default function ParentManagementActions({ parent, onUpdated }) {
    const [mode, setMode] = useState(null);
    const [saving, setSaving] = useState(false);
    const busyRef = useRef(false);
    const [error, setError] = useState("");

    const [profile, setProfile] = useState({
        fullName: parent.fullName || "",
        email: parent.email || "",
        phoneNumber: parent.phoneNumber || "",
    });

    const [passwords, setPasswords] = useState({
        password: "",
        confirmPassword: "",
    });

    const hasAccount = Boolean(parent.applicationUserId);

    function openEdit() {
        setProfile({
            fullName: parent.fullName || "",
            email: parent.email || "",
            phoneNumber: parent.phoneNumber || "",
        });
        setError("");
        setMode("edit");
    }

    function openAccount() {
        setPasswords({ password: "", confirmPassword: "" });
        setError("");
        setMode("account");
    }

    function cancel() {
        setMode(null);
        setError("");
        setPasswords({ password: "", confirmPassword: "" });
    }

    async function handleSave(event) {
        event.preventDefault();

        if (busyRef.current) return;

        setError("");

        if (!profile.fullName.trim()) {
            setError("Full name is required.");
            return;
        }

        busyRef.current = true;
        setSaving(true);

        try {
            const response = await parentManagementApi.updateParent(
                parent.id,
                {
                    fullName: profile.fullName.trim(),
                    email: profile.email.trim() || null,
                    phoneNumber: profile.phoneNumber.trim() || null,
                    isActive: parent.isActive,
                }
            );

            setMode(null);
            onUpdated(
                response.data?.message || "Parent updated successfully."
            );
        } catch (err) {
            setError(getErrorMessage(err));
        } finally {
            busyRef.current = false;
            setSaving(false);
        }
    }

    async function handleCreateAccount(event) {
        event.preventDefault();

        if (busyRef.current) return;

        setError("");

        if (!passwords.password || !passwords.confirmPassword) {
            setError("Enter and confirm the password.");
            return;
        }

        if (passwords.password !== passwords.confirmPassword) {
            setError("Password and confirmation do not match.");
            return;
        }

        busyRef.current = true;
        setSaving(true);

        try {
            const response = await parentManagementApi.createAccount({
                parentGuardianId: parent.id,
                password: passwords.password,
                confirmPassword: passwords.confirmPassword,
            });

            setPasswords({ password: "", confirmPassword: "" });
            setMode(null);

            onUpdated(
                response.data?.message ||
                "Parent login account created successfully."
            );
        } catch (err) {
            setError(getErrorMessage(err));
        } finally {
            busyRef.current = false;
            setSaving(false);
        }
    }

    if (hasAccount) {
        return (
            <p className="mt-5 rounded-xl bg-emerald-50 p-3 text-sm text-emerald-700">
                A login account is already linked to this parent.
            </p>
        );
    }

    return (
        <div className="mt-6 border-t border-slate-100 pt-5">
            {!mode && (
                <>
                    <div className="flex flex-wrap gap-3">
                        <button
                            type="button"
                            onClick={openEdit}
                            className={buttonClass}
                        >
                            Edit Parent
                        </button>

                        <button
                            type="button"
                            onClick={openAccount}
                            disabled={!parent.isActive || !parent.email?.trim()}
                            className={primaryButtonClass}
                        >
                            Create Login Account
                        </button>
                    </div>

                    {!parent.email?.trim() && (
                        <p className="mt-3 text-sm text-slate-500">
                            Add an email using Edit Parent before creating
                            the login account.
                        </p>
                    )}

                    {!parent.isActive && (
                        <p className="mt-3 text-sm text-slate-500">
                            The parent must be active to create a login account.
                        </p>
                    )}
                </>
            )}

            {mode === "edit" && (
                <form onSubmit={handleSave}>
                    <h3 className="font-bold text-slate-950">
                        Edit Parent
                    </h3>

                    <fieldset disabled={saving} className="mt-4">
                        <div className="grid gap-4 sm:grid-cols-2">
                            <InputField
                                label="Full Name"
                                value={profile.fullName}
                                maxLength={200}
                                required
                                onChange={(event) => {
                                    setProfile((current) => ({
                                        ...current,
                                        fullName: event.target.value,
                                    }));
                                    setError("");
                                }}
                            />

                            <InputField
                                label="Email"
                                type="email"
                                value={profile.email}
                                maxLength={256}
                                onChange={(event) => {
                                    setProfile((current) => ({
                                        ...current,
                                        email: event.target.value,
                                    }));
                                    setError("");
                                }}
                            />

                            <InputField
                                label="Phone Number"
                                type="tel"
                                value={profile.phoneNumber}
                                maxLength={50}
                                onChange={(event) => {
                                    setProfile((current) => ({
                                        ...current,
                                        phoneNumber: event.target.value,
                                    }));
                                    setError("");
                                }}
                            />
                        </div>
                    </fieldset>

                    <ErrorMessage message={error} />

                    <div className="mt-5 flex flex-wrap gap-3">
                        <button
                            type="submit"
                            disabled={saving}
                            className={primaryButtonClass}
                        >
                            {saving ? "Saving..." : "Save Changes"}
                        </button>

                        <button
                            type="button"
                            disabled={saving}
                            onClick={cancel}
                            className={buttonClass}
                        >
                            Cancel
                        </button>
                    </div>
                </form>
            )}

            {mode === "account" && (
                <form onSubmit={handleCreateAccount}>
                    <h3 className="font-bold text-slate-950">
                        Create Parent Login Account
                    </h3>

                    <p className="mt-2 break-all text-sm text-slate-500">
                        Login email: {parent.email}
                    </p>

                    <fieldset disabled={saving} className="mt-4">
                        <div className="grid gap-4 sm:grid-cols-2">
                            <PasswordField
                                label="Password"
                                value={passwords.password}
                                onChange={(event) => {
                                    setPasswords((current) => ({
                                        ...current,
                                        password: event.target.value,
                                    }));
                                    setError("");
                                }}
                            />

                            <PasswordField
                                label="Confirm Password"
                                value={passwords.confirmPassword}
                                onChange={(event) => {
                                    setPasswords((current) => ({
                                        ...current,
                                        confirmPassword: event.target.value,
                                    }));
                                    setError("");
                                }}
                            />
                        </div>
                    </fieldset>

                    <ErrorMessage message={error} />

                    <div className="mt-5 flex flex-wrap gap-3">
                        <button
                            type="submit"
                            disabled={saving}
                            className={primaryButtonClass}
                        >
                            {saving ? "Creating..." : "Create Login Account"}
                        </button>

                        <button
                            type="button"
                            disabled={saving}
                            onClick={cancel}
                            className={buttonClass}
                        >
                            Cancel
                        </button>
                    </div>
                </form>
            )}
        </div>
    );
}

function InputField({ label, ...props }) {
    return (
        <label className="block text-sm font-semibold text-slate-700">
            {label}
            <input
                {...props}
                className="mt-2 block w-full rounded-xl border border-slate-200 px-4 py-3 font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50"
            />
        </label>
    );
}

function PasswordField({ label, value, onChange }) {
    const [visible, setVisible] = useState(false);

    return (
        <div>
            <label className="block text-sm font-semibold text-slate-700">
                {label}

                <div className="relative mt-2">
                    <input
                        type={visible ? "text" : "password"}
                        value={value}
                        onChange={onChange}
                        autoComplete="new-password"
                        required
                        className="block w-full rounded-xl border border-slate-200 py-3 pl-4 pr-12 font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100 disabled:bg-slate-50"
                    />

                    <button
                        type="button"
                        onClick={() => setVisible((current) => !current)}
                        aria-label={
                            visible ? `Hide ${label}` : `Show ${label}`
                        }
                        aria-pressed={visible}
                        className="absolute right-3 top-1/2 -translate-y-1/2 cursor-pointer rounded p-1 text-slate-500 hover:text-slate-900 disabled:cursor-not-allowed"
                    >
                        {visible ? (
                            <EyeOff className="h-5 w-5" />
                        ) : (
                            <Eye className="h-5 w-5" />
                        )}
                    </button>
                </div>
            </label>
        </div>
    );
}

function ErrorMessage({ message }) {
    if (!message) return null;

    return (
        <p
            role="alert"
            className="mt-4 rounded-xl bg-red-50 p-3 text-sm text-red-700"
        >
            {message}
        </p>
    );
}

function getErrorMessage(error) {
    const body = error?.response?.data;

    const errors = Array.isArray(body?.errors)
        ? body.errors
        : Object.values(body?.errors ?? {}).flat();

    if (errors.length) return errors.join(" ");
    if (body?.message) return body.message;

    if (!error?.response) {
        return "Cannot reach the backend. Check that the API is running.";
    }

    return "The request failed. Refresh the parent details before retrying.";
}