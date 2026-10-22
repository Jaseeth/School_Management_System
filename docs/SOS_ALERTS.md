# SOS announcements

## Sending an alert

Admin, Principal and Deputy Principal can open **Announcements**, select **SOS — urgent alert with alarm**, enter the message and intended audience, choose an expiry, then review and confirm **Send SOS now**. Teacher and Section Head announcement permissions remain unchanged; they cannot send SOS.

SOS sends immediately. Its default expiry is two hours, with choices from one to 24 hours. Selected recipients need active login accounts. The recipient snapshot is saved when sending; changing classes or assignments later does not change that original audience. Normal announcements keep their existing behavior.

## Receiving an alert

The staff, student and parent portals show **Enable SOS sound** when sound is not enabled in that tab. Clicking it starts the browser audio context and requests browser notifications where supported. Background push uses the school's existing Firebase configuration; no SMS provider is added.

An active SOS opens a prominent dialog over the current portal page. With sound enabled, a repeating three-tone alarm plays. **Mute alarm** stops sound in this tab without acknowledging. **Resume alarm** starts it again. **I have seen this — acknowledge** saves the acknowledgement, marks the related notification read, and closes the alert. Reading an ordinary notification does not acknowledge SOS. Other waiting alerts appear next.

If sound cannot start, the dialog remains visible with a sound-enable button. Audio setup times out with guidance instead of remaining stuck. Reloading a page or opening a new tab may require enabling sound again. Normal navigation inside that portal preserves the audio context.

Active alerts are fetched on sign-in, focus, reconnection, push messages, and approximately every ten seconds while the browser permits timers. Acknowledged, cancelled or expired alerts stop appearing. A local expiry timer also stops an already displayed alert when the server is unreachable. A connection warning appears if status cannot be checked or acknowledgement cannot be saved.

## Management and history

Each SOS card offers **View acknowledgements**, showing names, staff/index/parent numbers, portal, and acknowledgement timestamps. Pending means no acknowledgement was recorded; it does not prove the notification reached the device. **Cancel SOS** stops the active alert on recipients' next successful status check. Cancellation retains the announcement and recipient history. SOS text cannot be edited after sending: cancel it and send a corrected alert.

The push worker shows a generic urgent notification without exposing message contents on the lock screen. SOS uses high web-push urgency and a distinct notification tag, and requests a persistent notification where supported. Existing account-binding and sign-out checks are preserved.

## Practical limits

This is a browser-based urgent messaging feature, not a guaranteed emergency alarm. A closed/stopped browser, sleeping or offline device, silent/Do Not Disturb settings, permission denial, background timer throttling, and operating-system policies can delay or suppress delivery or sound. A service worker cannot play a continuous custom alarm in a closed browser. The feature does not bypass these restrictions. Use the school's established emergency procedures for life-critical situations.

## Verification

- Three SQL integration cases cover sending permissions, selected recipients, active accounts, portal isolation, idempotent acknowledgement, read status, cancellation, expiry, normal-announcement separation and private/high-urgency push payloads.
- Six JavaScript tests exercise the real push-worker code, including SOS styling, notification persistence requests, malformed payloads, account replacement and sign-out suppression.
- Playwright covers SOS delivery and acknowledgement across staff, student and parent portals, management acknowledgement history and cancellation in Chrome and WebKit at 390 × 844. Chrome exercises real Web Audio oscillator creation, mute and resume. Windows headless WebKit did not start audio; its test checks the visible-alert fallback, not successful audible playback.
- Physical-phone delivery, speaker audibility, background/closed-browser behavior and live Firebase delivery still require testing on the school's supported devices. Automated tests use disposable databases and do not send SOS to real school users.
- Migration: `20261020202231_AnnouncementSosAlerts`. Browser results: `.local-tools/qa/results/SosBrowser.trx`. Screenshots: `.local-tools/qa/browser/sos-alert-{chrome,webkit}.png`.

## Simple manual check

1. Sign in as a Teacher in a separate browser/profile and enable SOS sound. Keep the portal open with device volume audible.
2. As Admin, create a clearly labelled test SOS addressed only to that Teacher.
3. Verify the dialog and sound, try mute/resume, and acknowledge.
4. Open acknowledgement history as Admin and verify the named Teacher's timestamp.
5. Send another test, cancel it, and verify it closes after the next status refresh.
6. Separately test allowed push notifications with the portal tab closed on each supported desktop/mobile device. Do not assume that closed-browser sound is guaranteed.
