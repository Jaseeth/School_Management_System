import { GraduationCap } from "lucide-react";
import { useAuth } from "../../context/AuthContext";
import PromotionPanel from "./PromotionPanel";

export default function PromotionPage() {
    const { user } = useAuth();
    if (!["Admin", "Principal", "Deputy Principal"].some(role => user?.roles?.includes(role))) {
        return <p className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600">Sign in with a school management role to manage student promotion.</p>;
    }
    return <main className="space-y-6">
        <header>
            <div className="flex items-center gap-3"><GraduationCap className="h-6 w-6 text-blue-600" /><h1 className="text-2xl font-bold text-slate-950">Student Promotion</h1></div>
            <p className="mt-2 text-sm leading-6 text-slate-500">Students move up automatically. Select only those who should repeat next year.</p>
        </header>
        <PromotionPanel />
    </main>;
}
