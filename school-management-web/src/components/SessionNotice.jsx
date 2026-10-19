import { useEffect, useState } from "react";
export default function SessionNotice(){
 const [notice,setNotice]=useState(null);
 useEffect(()=>{const listener=e=>setNotice(e.detail);window.addEventListener("portal-session-expired",listener);return()=>window.removeEventListener("portal-session-expired",listener);},[]);
 if(!notice)return null;
 const portal=window.location.pathname.startsWith("/student/")?"student":window.location.pathname.startsWith("/parent/")?"parent":"staff";
 if(notice.portal!==portal)return null;
 return <aside role="alert" className="fixed inset-x-4 top-4 z-50 mx-auto flex max-w-3xl flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900 shadow-lg"><p>{notice.message}</p><a className="font-semibold underline" href={portal==="staff"?"/login":`/${portal}/login`}>Sign in</a><button onClick={()=>setNotice(null)} aria-label="Dismiss session notice">Close</button></aside>;
}
