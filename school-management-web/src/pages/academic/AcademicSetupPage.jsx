import { useState } from "react";
import { BookOpen } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import AcademicSetupPanel from "./AcademicSetupPanel";

const tabs = [
    { id: "years", label: "Academic Years" },
    { id: "sections", label: "Sections" },
    { id: "grades", label: "Grades" },
    { id: "classes", label: "Classes" },
    { id: "subjects", label: "Subjects" },
];

export default function AcademicSetupPage() {
    const { user } = useAuth();
    const allowed = ["Admin", "Principal", "Deputy Principal"].some(
        (role) => user?.roles?.includes(role)
    );
    const [tab, setTab] = useState("years");
    const [saving, setSaving] = useState(false);

    if (!allowed) return (
        <p className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600">
            Sign in with a school management role to manage academic setup.
        </p>
    );

    return (
        <div className="space-y-6">
            <div>
                <div className="flex items-center gap-3">
                    <BookOpen className="h-6 w-6 text-blue-600" />
                    <h1 className="text-2xl font-bold text-slate-950">Academic Setup</h1>
                </div>
                <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">
                    Set up academic years, sections, grades, classes and subjects for your school.
                    Create a section before adding its grades, then add classes within each grade.
                </p>
            </div>

            <nav aria-label="Academic setup categories" className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3">
                {tabs.map((item) => (
                    <button key={item.id} type="button" aria-current={tab === item.id ? "page" : undefined}
                        disabled={saving} onClick={() => setTab(item.id)}
                        className={`cursor-pointer rounded-xl px-4 py-3 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50 ${tab === item.id ? "bg-blue-600 text-white" : "text-slate-600 hover:bg-slate-50 hover:text-blue-700"}`}>
                        {item.label}
                    </button>
                ))}
            </nav>

            <AcademicSetupPanel key={tab} type={tab} saving={saving} setSaving={setSaving} />
        </div>
    );
}
