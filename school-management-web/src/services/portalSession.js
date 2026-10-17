export function getPortalToken(portal) {
    const key = `${portal}AccessToken`;
    const saved = localStorage.getItem(key);
    if (saved) return saved;
    const legacy = localStorage.getItem("accessToken");
    if (!legacy) return null;
    try {
        const payload = JSON.parse(atob(legacy.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
        const rawRoles = payload.role ?? payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? [];
        const roles = Array.isArray(rawRoles) ? rawRoles : [rawRoles];
        const matches = portal === "student" ? roles.includes("Student") : portal === "parent" ? roles.includes("Parent")
            : roles.some(role => ["Admin", "Teacher", "Section Head", "Principal", "Deputy Principal"].includes(role));
        if (!matches) return null;
        localStorage.setItem(key, legacy);
        localStorage.removeItem("accessToken");
        return legacy;
    } catch { return null; }
}
