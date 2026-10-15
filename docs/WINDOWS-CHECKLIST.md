# Windows verification before release

Version 0.1.0. These checks require an actual Windows 10/11 PC. They have not been passed in the Linux build environment.

- Extract the portable ZIP completely. Double-click Start-CampaignStudio.cmd. Confirm the browser opens without a .NET/Node SDK installed, the login key is removed from the URL, and no network interface other than loopback is bound.
- Import docs/sample-contacts.csv with consent source “Demo data” and test permission checked: 2 added, 1 duplicate, 1 rejected.
- Export the skipped rows and all contacts. Check Unicode, CSV formula protection, and columns.
- Create a campaign, select the imported list, save, close, and restart. Verify the draft and contacts persist.
- Preview at desktop and mobile widths. Check keyboard navigation, 125/150/200% scaling, screen readers, and high contrast.
- Schedule Capture immediately. Confirm only two .eml files are captured, no SMTP connection occurs, and files open as multipart email with personalized names.
- Schedule Capture in the future. Restart before the due time. Confirm it starts only when the local server is running.
- Test pause/resume and cancellation with enough contacts to observe the queue. Accepted/in-flight messages cannot be recalled.
- Build the native WinUI shell with scripts/Build-Windows.ps1 on Windows. Test WebView2 on a clean PC, startup, close, downloads, port cleanup, and native error messages.
- For cloud mode, set CAMPAIGN_STUDIO_CLOUD_URL and CAMPAIGN_STUDIO_CLOUD_KEY before starting the native shell. Verify invalid TLS, offline errors, and top-level navigation restriction.
- Configure live SMTP only on a controlled test server. Test authentication, STARTTLS, quota errors, disconnect during DATA, and provider acceptance before a process crash.
- Check unsubscribe POST and confirmation GET from the actual public domain. Opening the link alone must not suppress a contact. SMTP provider/domain signing must cover the relevant unsubscribe headers.
- Test PostgreSQL transactions, simultaneous schedule/edit requests, backup/restore, and persistent suppression.
- Complete Microsoft/publisher signing and clean install/update tests before calling it a commercial installer.

The portable preview uses a browser window. The native shell source is included separately; do not describe the portable preview as a verified native WinUI release.
