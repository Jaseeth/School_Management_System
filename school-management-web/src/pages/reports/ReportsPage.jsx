import { Fragment, useEffect, useState } from "react";
import api from "../../api/axios";
import { useAuth } from "../../context/AuthContext";

const types = { academic: "Student academic profile", studentResults: "Student results", classStudents: "Class student report", classAttendance: "Class attendance", classResults: "Class results" };
const peopleTypes = { sectionHeads: "Section Head assignments", teachers: "Teacher subject assignments", parents: "Parent links and accounts" };
const control = "mt-2 w-full rounded-xl border border-slate-200 bg-white p-3 text-sm";
const label = key => {
    const names = { presentDays: "Present (on time)", lateDays: "Late", academicYearName: "Academic year", gradeName: "Grade",
        className: "Class", sectionName: "Section", subjectName: "Subject", examName: "Exam", academicTermName: "Academic term", fullName: "Full name" };
    return names[key] || key.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, text => text.toUpperCase()).replace(/ Name$/, "");
};
function ReportContent({ value }) {
    if (value == null) return <span>—</span>;
    if (typeof value !== "object") return <span>{typeof value === "boolean" ? value ? "Yes" : "No" : String(value)}</span>;
    if (Array.isArray(value)) {
        if (!value.length) return <p className="text-sm text-slate-500">No records available.</p>;
        const columns = Object.keys(value[0]).filter(key => !/Id$/.test(key) && key !== "id" && (value[0][key] == null || typeof value[0][key] !== "object"));
        return <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50"><tr>{columns.map(key => <th className="whitespace-nowrap p-3 font-semibold" key={key}>{label(key)}</th>)}</tr></thead><tbody>{value.map((row, index) => <Fragment key={index}><tr className="border-t border-slate-100" >{columns.map(key => <td key={key} className="p-3"><ReportContent value={row[key]} /></td>)}</tr>{Object.entries(row).filter(([, item]) => item != null && typeof item === "object").map(([key, item]) => <tr key={key}><td colSpan={Math.max(columns.length, 1)} className="p-3"><h4 className="mb-2 font-semibold">{label(key)}</h4><ReportContent value={item} /></td></tr>)}</Fragment>)}</tbody></table></div>;
    }
    return <div className="space-y-5">{Object.entries(value).filter(([key]) => !/Id$/.test(key) && key !== "id").map(([key, item]) => typeof item === "object" && item !== null
        ? <section key={key}><h3 className="mb-3 font-semibold">{label(key)}</h3><ReportContent value={item} /></section>
        : <div key={key} className="flex justify-between gap-4 border-b border-slate-100 pb-2 text-sm"><span className="text-slate-500">{label(key)}</span><ReportContent value={item} /></div>)}</div>;
}
export default function ReportsPage() {
    const { hasRole } = useAuth();
    const [options, setOptions] = useState({ students: [], years: [], classes: [], terms: [], exams: [] });
    const [type, setType] = useState("");
    const [student, setStudent] = useState("");
    const [year, setYear] = useState("");
    const [schoolClass, setClass] = useState("");
    const [term, setTerm] = useState("");
    const [exam, setExam] = useState("");
    const [data, setData] = useState(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    useEffect(() => {
        let active = true;
        api.get("/reports/options").then(({ data }) => { if (active) setOptions(data); }).catch(err => { if (active) setError(err?.response?.data?.message || "Unable to load report selections."); });
        return () => { active = false; };
    }, []);
    const studentReport = ["academic", "studentResults"].includes(type);
    const peopleReport = Object.hasOwn(peopleTypes, type);
    const canViewPeopleReports = ["Admin", "Principal", "Deputy Principal"].some(hasRole);
    const availableTypes = canViewPeopleReports ? { ...types, ...peopleTypes } : types;
    const resultsReport = type.endsWith("Results");
    function url(pdf) {
        if (peopleReport) return `/reports/people/${type}${pdf ? '/pdf' : ''}`;
        const root = studentReport ? `/reports/students/${student}` : `/reports/classes/${schoolClass}`;
        const suffix = { academic: "academic-profile", studentResults: "results", classStudents: "students", classAttendance: "attendance", classResults: "results" }[type];
        return `${root}/${suffix}${pdf ? "/pdf" : ""}`;
    }
    async function generate(pdf) {
        if (busy) return;
        if (!type || (studentReport && !student) || (!studentReport && !peopleReport && !schoolClass) || (type !== "academic" && !year) || (resultsReport && !term) || (type === "classResults" && !exam)) { setError("Complete the report selections first."); return; }
        setBusy(true); setError("");
        try {
            const response = await api.get(url(pdf), { params: { academicYearId: year || undefined, academicTermId: term || undefined, examId: type === "classResults" ? exam : undefined }, responseType: pdf ? "blob" : "json" });
            if (!pdf) setData(response.data);
            else {
                const objectUrl = URL.createObjectURL(response.data);
                const anchor = document.createElement("a"); anchor.href = objectUrl; anchor.download = `${type}-${student || schoolClass}.pdf`; anchor.click();
                window.setTimeout(() => URL.revokeObjectURL(objectUrl), 10000);
            }
        } catch (err) { setError(err?.response?.status === 403 ? "This report is outside your assigned section or academic year." : err?.response?.data?.message || "Unable to generate the report."); }
        finally { setBusy(false); }
    }
    const select = (title, value, change, choices) => <label className="text-sm">{title}<select className={control} value={value} onChange={event => { change(event.target.value); setData(null); setError(""); }}><option value="">Select {title.toLowerCase()}</option>{choices.map(item => <option key={item.id} value={item.id}>{item.code ? `${item.code} — ` : ""}{item.name}</option>)}</select></label>;
    return <main className="space-y-6 p-6 lg:p-10"><h1 className="text-2xl font-bold">Reports</h1><p className="text-sm text-slate-500">View academic, attendance and published-result reports, then download a PDF.</p>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-600">{error}</p>}
        <section className="space-y-5 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><div className="grid gap-4 md:grid-cols-2">
            {select("Report", type, value => { setType(value); setStudent(""); setClass(""); setYear(""); setTerm(""); setExam(""); }, Object.entries(availableTypes).map(([id, name]) => ({ id, name })))}
            {type && !peopleReport && (studentReport ? select("Student", student, setStudent, Array.from(new Map(options.students.map(item => [item.id, item])).values())) : select("Class", schoolClass, setClass, options.classes))}
            {type && type !== "academic" && select("Academic year", year, value => { setYear(value); setTerm(""); setExam(""); }, options.years)}
            {resultsReport && select("Term", term, value => { setTerm(value); setExam(""); }, options.terms.filter(item => String(item.academicYearId) === year))}
            {type === "classResults" && select("Exam", exam, setExam, (options.exams ?? []).filter(item => String(item.academicTermId) === term))}
        </div><div className="flex gap-3"><button disabled={busy || !type} onClick={() => generate(false)} className="rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50">{busy ? "Generating…" : "View report"}</button><button disabled={busy || !type} onClick={() => generate(true)} className="rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold disabled:opacity-50">Download PDF</button></div></section>
        {data && <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><h2 className="mb-5 text-xl font-semibold">{availableTypes[type]}</h2><p className="mb-5 text-sm text-slate-500">{peopleReport ? "Assignments and account links recorded in the system." : "Present and Late are shown separately. Attendance percentage includes both. Results include published marks only."}</p><ReportContent value={data} /></section>}
    </main>;
}
