# Browser push notifications

Implemented for staff, student and parent portals. Each Notifications page has Browser alerts controls: enable, disable and Send test alert. Permission is requested only after the person clicks Enable. In-app notifications continue working without browser permission.

## Local setup completed

- Firebase public web configuration and public VAPID key supplied by Jaseeth were saved to the API user-secret store under Firebase:Web. No values were committed to source.
- Firebase:CredentialsPath points to the local Admin SDK file identified by Jaseeth. Its private contents were not copied into the project or displayed. Firebase server authentication succeeded without sending a notification.
- Migration 20261019131235_ParentBrowserPush is applied to SchoolManagement_Dev. Existing parent mobile subscriptions remain unchanged; new browser subscriptions are marked separately.

Restart the API to load the saved settings. Refresh the frontend, open Notifications, click Enable browser alerts, allow the browser prompt, then click Send test alert. Try the same in the student and parent portals as needed. The test targets only the selected browser subscription for the authenticated account; it does not message other users or create a school announcement.

Keep the Admin SDK file at the configured path. If it is moved, update Firebase:CredentialsPath in user secrets. Production must use the hosting environment's secret store and a private credential file/service identity; developer user secrets are local only.

## Configuration

Firebase:ProjectId must match Firebase:Web:ProjectId. Required web keys: ApiKey, ProjectId, MessagingSenderId, AppId and VapidKey. AuthDomain is also saved. Analytics/storage settings are unnecessary for messaging and are not initialized.

The public config endpoint requires authentication and returns only client web settings. It never returns a service-account key, credential path, JWT key or SMTP settings. Firebase credential errors produce a clear test-delivery error; successful authentication alone does not prove browser delivery.

## Delivery and privacy

- The Firebase SDK is loaded on demand for token registration/renewal. One browser subscription can serve separately opted-in staff, student and parent accounts.
- The dedicated /school-push/ service-worker scope has no page caching or fetch interception. Its scripts must be served as JavaScript from /school-push-sw.js and /school-push-store.js, rather than an SPA HTML fallback.
- Server web messages are data-only and carry the portal and recipient account identity. The worker checks a local IndexedDB consent binding before showing a generic school-update notification. School, student and leave details stay inside the authenticated inbox.
- Clicking an alert opens the matching portal's Notifications page. Payload-supplied URLs cannot redirect it to another site.
- Sign-out disables that portal's local binding, clears displayed alerts and attempts to unregister its server subscription. Other portal settings remain separate. Local consent stays disabled even when the network is unavailable. IndexedDB revision checks prevent a delayed renewal from restoring older consent.
- Existing enabled tokens renew on app startup/focus and periodically while the app is open; renewed subscriptions are registered with the server. No automatic permission prompt is shown.
- Background delivery depends on browser support, OS notification settings and connectivity. HTTPS is required outside localhost. A fully quit browser may not receive pushes until the browser/OS messaging service runs again.

## Verification

64 backend integration cases passed, including subscription ownership, cross-account rejection, portal isolation, missing/mismatched config, generic data-only payloads and Firebase failure handling. Five JavaScript tests passed, including the actual worker handlers, delayed delivery after sign-out, safe click routing and concurrent consent updates. Production frontend build and lint passed (existing warnings remain). npm audit reports zero vulnerabilities after compatible transitive dependency patches.

A synthetic browser preview verified the missing-config state and Enable control. No browser permission was granted or real notification sent by the agent. Firebase OAuth authentication was verified separately. Final real delivery must be checked using the browser's Allow prompt and Send test alert.

References: https://firebase.google.com/docs/cloud-messaging/web/get-started and https://developer.mozilla.org/en-US/docs/Web/API/ServiceWorkerGlobalScope/push_event .

## Configuration refresh fix

The Browser alerts card now rechecks configuration on the Notifications Refresh button, on window focus and every 30 seconds. The API config response is marked no-store. A synthetic browser regression verified that a card initially showing missing settings changes to Enable browser alerts after server setup completes and Refresh is clicked, without remounting the page. Effective development configuration was also checked: all required fields are present and the server/web projects match; no key values were printed.
