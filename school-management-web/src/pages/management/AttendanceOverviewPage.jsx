import { useEffect, useState } from "react";
import api from "../../api/axios";

const input = "mt-1 block rounded-xl border border-slate-200 bg-white p-3 text-sm";

function localDate(date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function datePart(value) {
    return typeof value === "string" ? value.slice(0, 10) : "";
}

export default function AttendanceOverviewPage() {
    const [options, setOptions] = useState(null);
    const [data, setData] = useState(null);
    const [error, setError] = useState("");
    const [busy, setBusy] = useState(false);
    const [filters, setFilters] = useState(() => {
        const today = localDate(new Date());
        return { academicYearId: "", schoolClassId: "", from: today, to: today, search: "" };
    });
    const [page, setPage] = useState(1);

    useEffect(() => {
        const controller = new AbortController();
        api.get("/reports/options", { signal: controller.signal })
            .then(({ data: result }) => {
                if (controller.signal.aborted) return;
                setOptions(result);
                const today = result.today || localDate(new Date());
                const currentYear = result.years?.find(year => datePart(year.startDate) <= today && datePart(year.endDate) >= today)
                    || result.years?.find(year => year.isActive)
                    || result.years?.[0];
                setFilters(current => ({
                    ...current,
                    academicYearId: current.academicYearId || (currentYear ? String(currentYear.id) : ""),
                    from: today,
                    to: today,
                }));
            })
            .catch(error => {
                if (!controller.signal.aborted) setError(error.response?.data?.message || "Unable to load attendance options.");
            });
        return () => controller.abort();
    }, []);

    async function load(nextPage = 1, selectedFilters = filters) {
        if (!selectedFilters.academicYearId) return;
        setBusy(true);
        setError("");
        try {
            const response = await api.get("/attendance/overview", {
                params: {
                    ...selectedFilters,
                    schoolClassId: selectedFilters.schoolClassId || undefined,
                    page: nextPage,
                },
            });
            setData(response.data);
            setPage(response.data.page);
        } catch (requestError) {
            setError(requestError.response?.data?.message || "Unable to load attendance records.");
        } finally {
            setBusy(false);
        }
    }

    useEffect(() => {
        if (!options || !filters.academicYearId) return;
        load(1, filters);
        // Load today's records once the permitted academic year has been selected.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [options, filters.academicYearId]);

    function changeFilter(event) {
        const { name, value } = event.target;
        setFilters(current => ({ ...current, [name]: value }));
    }

    return (
        <main className="space-y-5">
            <div>
                <h1 className="text-2xl font-bold">Student Attendance Overview</h1>
                <p className="mt-2 text-sm text-slate-500">Today’s attendance is shown by default. Select a date range to review earlier records. This page does not mark attendance.</p>
            </div>

            <form className="flex flex-wrap items-end gap-3 rounded-xl border border-slate-200 bg-white p-5" onSubmit={event => { event.preventDefault(); load(1); }}>
                <label className="text-sm">Academic year<span className="required-marker" aria-hidden="true"> *</span>
                    <select required name="academicYearId" className={input} value={filters.academicYearId} onChange={changeFilter}>
                        <option value="">Select academic year</option>
                        {options?.years.map(year => <option key={year.id} value={year.id}>{year.name}</option>)}
                    </select>
                </label>
                <label className="text-sm">Class
                    <select name="schoolClassId" className={input} value={filters.schoolClassId} onChange={changeFilter}>
                        <option value="">All permitted classes</option>
                        {options?.classes.map(schoolClass => <option key={schoolClass.id} value={schoolClass.id}>{schoolClass.name}</option>)}
                    </select>
                </label>
                {[ ["from", "From"], ["to", "Through"] ].map(([key, label]) => (
                    <label key={key} className="text-sm">{label}<span className="required-marker" aria-hidden="true"> *</span>
                        <input required name={key} className={input} type="date" value={filters[key]} onChange={changeFilter} />
                    </label>
                ))}
                <label className="text-sm">Student index or name
                    <input name="search" className={input} value={filters.search} onChange={changeFilter} />
                </label>
                <button disabled={busy || !filters.academicYearId} className="rounded-xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50">
                    {busy ? "Loading…" : "View attendance"}
                </button>
            </form>

            {error && <p role="alert" className="text-red-700">{error}</p>}
            {data && <>
                <div className="flex flex-wrap gap-3">
                    {data.summary.map(summary => <p className="rounded-xl border border-slate-200 bg-white p-4 text-sm" key={summary.status}>{summary.status}: <strong>{summary.count}</strong></p>)}
                </div>
                <div className="overflow-auto rounded-xl border border-slate-200 bg-white">
                    <table className="w-full text-left text-sm">
                        <thead><tr>{["Date", "Index", "Student", "Class", "Status", "Remarks"].map(heading => <th className="p-3" key={heading}>{heading}</th>)}</tr></thead>
                        <tbody>{data.records.map(record => (
                            <tr className="border-t border-slate-200" key={record.id}>
                                {[record.attendanceDate.slice(0, 10), record.indexNumber, record.fullName,
                                    record.schoolClass, record.status, record.remarks || "—"].map((value, index) => (
                                    <td key={index} className="p-3">{value}</td>
                                ))}
                            </tr>
                        ))}</tbody>
                    </table>
                    {!data.total && <p className="p-4 text-sm text-slate-500">No attendance records match these filters.</p>}
                </div>
                <div className="flex items-center gap-4">
                    <button disabled={busy || page <= 1} onClick={() => load(page - 1)} className="disabled:opacity-50">Previous</button>
                    <span className="text-sm">Page {page} of {Math.max(1, data.totalPages)} · {data.total} records</span>
                    <button disabled={busy || page >= data.totalPages} onClick={() => load(page + 1)} className="disabled:opacity-50">Next</button>
                </div>
            </>}
        </main>
    );
}
