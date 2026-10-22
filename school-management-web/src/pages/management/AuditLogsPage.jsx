import { useEffect, useRef, useState } from "react";
import api from "../../api/axios";

function fieldLabel(key) {
    const words = key.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z])([A-Z][a-z])/g, "$1 $2").replace(/[_-]/g, " ");
    return words.charAt(0).toUpperCase() + words.slice(1).toLowerCase().replace(/\bid\b/g, "ID");
}
function normalizeSnapshot(value) {
    if (Array.isArray(value)) return value.map(normalizeSnapshot);
    if (value && typeof value === "object") return Object.fromEntries(Object.entries(value).map(([key, item]) => [key.charAt(0).toLowerCase() + key.slice(1), normalizeSnapshot(item)]));
    return value;
}
function visibleField(key, value) {
    return value != null && value !== "" && !(Array.isArray(value) && !value.length)
        && !/Ids?$/.test(key) && key !== "isActive";
}

function SnapshotValue({ value }) {
    if (value == null || value === "") return <span className="text-slate-500">Not recorded</span>;
    if (typeof value === "boolean") return <span>{value ? "Yes" : "No"}</span>;
    if (Array.isArray(value)) return value.length ? <ul className="space-y-2">{value.map((item, index) => <li key={index} className="border-l-2 border-slate-200 pl-3"><SnapshotValue value={item} /></li>)}</ul> : <span className="text-slate-500">None</span>;
    if (typeof value === "object") return <dl className="space-y-3">{Object.entries(value).filter(([key, item]) => visibleField(key, item)).map(([key, item]) => <div key={key}><dt className="text-slate-500">{fieldLabel(key)}</dt><dd className="mt-1 whitespace-pre-wrap break-words font-medium"><SnapshotValue value={item} /></dd></div>)}</dl>;
    const raw = String(value);
    const text = /^(Specific|All)(Students|Staff|Parents|SectionHeads)$/.test(raw) ? fieldLabel(raw) : raw;
    return <span className="whitespace-pre-wrap break-words">{/^\d{2}:\d{2}:00$/.test(text) ? text.slice(0, 5) : text}</span>;
}

function Snapshot({ value }) {
    if (!value) return <p className="text-slate-500">No snapshot recorded.</p>;
    let parsed = value;
    try { parsed = JSON.parse(value); } catch { /* Older entries may contain plain text. */ }
    return <SnapshotValue value={normalizeSnapshot(parsed)} />;
}

function Changes({ entry }) {
    let before, after;
    try { before = normalizeSnapshot(JSON.parse(entry.oldValues)); after = normalizeSnapshot(JSON.parse(entry.newValues)); } catch { /* Plain-text legacy snapshots use the fallback below. */ }
    if (before && after && typeof before === "object" && typeof after === "object" && !Array.isArray(before) && !Array.isArray(after)) {
        const keys = [...new Set([...Object.keys(before), ...Object.keys(after)])].filter(key => !/Ids?$/.test(key) && JSON.stringify(before[key]) !== JSON.stringify(after[key]));
        return <section className="mt-5"><h3 className="font-semibold">What changed</h3>{keys.length ? <div className="mt-3 space-y-3">{keys.map(key => <div key={key} className="rounded-xl border border-slate-200 p-4"><h4 className="text-sm font-semibold">{fieldLabel(key)}</h4><div className="mt-3 grid gap-3 text-sm sm:grid-cols-2"><div className="min-w-0"><p className="mb-1 text-slate-500">Before</p><SnapshotValue value={before[key]} /></div><div className="min-w-0 rounded-lg bg-blue-50 p-3 text-blue-900"><p className="mb-1 text-blue-700">After</p><SnapshotValue value={after[key]} /></div></div></div>)}</div> : <p className="mt-2 text-sm text-slate-500">No field differences were recorded.</p>}</section>;
    }
    return <section className="mt-5"><h3 className="font-semibold">Recorded details</h3><div className="mt-3 grid gap-4 sm:grid-cols-2">{[["Before", entry.oldValues], ["After", entry.newValues]].filter(([, value]) => value).map(([label, value]) => <section key={label} className="min-w-0"><h4 className="text-sm font-semibold">{entry.action === "Create" ? "Created record" : label}</h4><div className="mt-2 overflow-auto rounded-xl bg-slate-50 p-4 text-sm"><Snapshot value={value} /></div></section>)}</div></section>;
}

function auditArea(entity) {
    if (entity === "MondayAssembly") return "Timetable · Monday assembly";
    if (entity?.startsWith("StaffLeave")) return `Staff leave · ${fieldLabel(entity)}`;
    if (entity?.startsWith("StudentBulk")) return `Students · ${fieldLabel(entity.replace("StudentBulk", ""))}`;
    return fieldLabel(entity || "School records");
}

function AuditDetails({ entry, onClose }) {
    const dialog = useRef(null);
    useEffect(() => {
        const element = dialog.current;
        const opener = document.activeElement;
        element.showModal();
        const previousOverflow = document.body.style.overflow;
        document.body.style.overflow = "hidden";
        return () => {
            element.close(); document.body.style.overflow = previousOverflow;
            requestAnimationFrame(() => { if (opener?.isConnected) opener.focus(); });
        };
    }, []);
    return <dialog ref={dialog} aria-labelledby="audit-details-title" onCancel={onClose}
        className="fixed inset-0 m-auto max-h-[90dvh] w-[calc(100%-2rem)] max-w-3xl overflow-y-auto rounded-2xl border border-slate-200 bg-white p-5 text-slate-900 shadow-xl backdrop:bg-slate-950/50 sm:p-6">
        <div className="flex items-center justify-between gap-4">
            <h2 id="audit-details-title" className="text-lg font-semibold">Change details</h2>
            <button type="button" onClick={onClose} className="cursor-pointer rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold hover:bg-slate-50">Close</button>
        </div>
        <dl className="mt-5 grid gap-4 text-sm sm:grid-cols-2">
            {[["Action", entry.action === "RemoveSectionHeadAssignment" ? "Section head assignment removed" : fieldLabel(entry.action || "Action")], ["Where", auditArea(entry.entityName)],
                ["When", new Date(/Z$|[+-]\d{2}:\d{2}$/.test(entry.createdAt) ? entry.createdAt : `${entry.createdAt}Z`).toLocaleString()],
                [{ create: "Created by", update: "Changed by", delete: "Deleted by", removesectionheadassignment: "Removed by" }[entry.action?.toLowerCase()] || "Performed by", entry.actorName || "Person not available in this record"]].map(([label, value]) => <div key={label} className="min-w-0"><dt className="text-slate-500">{label}</dt><dd className="mt-1 break-words">{value ?? "—"}</dd></div>)}
        </dl>
        <p className="mt-5 whitespace-pre-wrap break-words text-sm">{entry.description}</p>
        <Changes entry={entry} />
        {entry.recipientContext && <section className="mt-5 rounded-xl bg-slate-50 p-4 text-sm"><h3 className="font-semibold">Audience details</h3><p className="my-2 text-slate-500">{entry.recipientContext.note}</p><SnapshotValue value={entry.recipientContext.details} /></section>}
    </dialog>;
}

export default function AuditLogsPage() {
    const [data,setData]=useState(null);const [filters,setFilters]=useState({action:"",entityName:"",fromDate:"",toDate:""});const [error,setError]=useState("");const [busy,setBusy]=useState(false);const [selected,setSelected]=useState(null);
    async function load(page=1){setBusy(true);setError("");try{const r=await api.get("/audit-logs",{params:{page,pageSize:25,...Object.fromEntries(Object.entries(filters).filter(([,v])=>v))}});setData(r.data);}catch(e){setError(e.response?.data?.message||"Unable to load audit history.");}finally{setBusy(false);}}
    useEffect(()=>{const c=new AbortController();api.get("/audit-logs",{params:{page:1,pageSize:25},signal:c.signal}).then(r=>setData(r.data)).catch(e=>{if(!c.signal.aborted)setError(e.response?.data?.message||"Unable to load audit history.");});return()=>c.abort();},[]);
    return <main className="space-y-5"><h1 className="text-2xl font-bold">Audit Logs</h1><p className="text-sm text-slate-500">Review who changed school records and when.</p><form className="flex flex-wrap items-end gap-3" onSubmit={e=>{e.preventDefault();load();}}>{[["action","Action"],["entityName","Record type"],["fromDate","From"],["toDate","Through"]].map(([key,label])=><label className="text-sm" key={key}>{label}<input type={key.endsWith("Date")?"date":"text"} className="mt-1 block rounded-xl border border-slate-200 bg-white p-3" value={filters[key]} onChange={e=>setFilters({...filters,[key]:e.target.value})}/></label>)}<button disabled={busy} className="rounded-xl bg-blue-600 px-4 py-3 text-sm text-white">Search history</button></form>{error&&<p role="alert" className="text-red-700">{error}</p>}{data&&<><div className="overflow-auto rounded-xl border border-slate-200 bg-white"><table className="w-full text-left text-sm"><thead><tr>{["Time","Changed by","Action","Record","Description","Details"].map(h=><th className="p-3" key={h}>{h}</th>)}</tr></thead><tbody>{data.auditLogs.map(a=><tr className="border-t border-slate-200" key={a.id}><td className="p-3">{new Date(a.createdAt.endsWith("Z")?a.createdAt:`${a.createdAt}Z`).toLocaleString()}</td><td className="p-3">{a.actorName || "Name not recorded"}</td><td className="p-3">{a.action}</td><td className="p-3">{a.entityName} #{a.entityId}</td><td className="p-3">{a.description}</td><td className="p-3"><button type="button" className="cursor-pointer rounded-lg px-3 py-2 text-blue-700 hover:bg-blue-50 focus-visible:outline-2 focus-visible:outline-blue-600" aria-label={`View details for ${a.entityName} #${a.entityId}`} onClick={async event=>{event.currentTarget.focus();setSelected(a);try{const result=await api.get(`/audit-logs/${a.id}`);setSelected(current=>current?.id===a.id?result.data:current);}catch{setError("Unable to load additional audit details. Showing the saved entry.");}}}>View</button></td></tr>)}</tbody></table>{!data.totalCount&&<p className="p-4 text-sm">No changes match these filters.</p>}</div><div className="flex gap-4 text-sm"><button disabled={busy||data.page<=1} onClick={()=>load(data.page-1)}>Previous</button><span>Page {data.page} of {Math.max(1,data.totalPages)} · {data.totalCount} records</span><button disabled={busy||data.page>=data.totalPages} onClick={()=>load(data.page+1)}>Next</button></div></>}{selected&&<AuditDetails entry={selected} onClose={()=>setSelected(null)} />}</main>;
}
