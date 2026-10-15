# Using Campaign Studio 0.2

## Open a workspace
Use the native Windows application or the portable Start-CampaignStudio.cmd launcher. The native window runs a private local API and injects its temporary workspace key into the trusted view. The portable launcher opens your browser and supplies the key through the launcher flow. For a hosted workspace, open its HTTPS URL and enter the workspace key.

Local data is separate: native uses %LOCALAPPDATA%\CampaignStudio; portable uses %LOCALAPPDATA%\CampaignStudio-Portable. Keep the application/service running for schedules, inbox polling and confirmation messages. Sleep or shutdown delays work until restart.

## Audience and lists
Open Audience to import a CSV, map columns and record a consent source. Review the added, duplicate and invalid counts. Use sort selectors for up to three fields. Suppression is global for the normalized email and survives list moves. Workspace → Lists & segments creates All/Any rules, previews matches, manages exclusions and moves/merges audiences. A canonical contact belongs to one list.

## Connections
Workspace → Connections manages encrypted SMTP and POP3 profiles. An SMTP connection test verifies TLS/authentication without sending. Set sender, reply-to, envelope return address, retry cap, delay and parallel connection limit. A direct profile requires explicit server configuration and suitable network access; ordinary SMTP is the default.

Live scheduling additionally requires the server live-send switch and a public HTTPS base URL. Do not use arbitrary public addresses for testing. First use capture mode, then an authorized test audience through your provider.

## Design and preview
Choose Campaigns → New, fill the subject/preheader and pick a starter template. Quick layout handles a simple message. Blocks supports adding/reordering content, columns, images and buttons. Undo/redo edits before saving.

Source mode retains your source as a draft; sanitized HTML is used for preview/sending. Import HTML or EML to reuse an existing design. EML imports preserve binary attachments and supported CID images. Review the result: proprietary Outlook markup is not guaranteed.

Choose an image from your computer or select an uploaded asset. Edit to crop/resize/adjust brightness or grayscale, then save a new asset and select it for the block. Uploaded image URLs are signed for preview and converted into CID resources for sending.

Select a real contact in preview to check merge fields. JavaScript tags accept bounded expressions over the offered contact fields. They cannot access your computer or the network. Review output and links before scheduling. External designer opens only the executable/export path configured by the workspace operator; save/export the HTML before closing that program.

## Attachments, events and reusable content
Select uploads for the whole campaign or assign each file to an exact recipient email. Limits: five MB per file, twenty selected files and twenty MB combined. Add a calendar event using UTC start/end. Workspace → Library stores snippets, templates and notes.

## Schedule and monitor
Save, then choose Review & schedule. Inspect the fresh audience count, selected sender, excluded/suppressed contacts and delivery mode. Capture mode creates personalized EML files without SMTP. Live mode requires explicit confirmation and a verified connection. Choose now or a future date.

Reports shows recipient state and attempt history. Accepted means the SMTP server accepted the message, not that it reached an inbox. Unknown means acceptance could not be established safely; the worker will not automatically resend. Review provider evidence before any manual action. Pause/resume/cancel operate on unsent work. Retrying failed jobs does not retry Accepted or Unknown jobs.

Choose a previous campaign in audience options for follow-ups or previously unmailed contacts. The latter excludes both Accepted and Unknown outcomes.

## Subscriptions and integrations
Workspace → Integrations & forms creates public double-opt-in forms. A GET opens the confirmation page; a POST confirms, protecting against link prefetch. Local preview links cannot receive confirmations from the public internet.

POP3 profiles use TLS, UIDL receipts and a mapped list. Subscribe requests queue a confirmation; Unsubscribe suppresses. Matched permanent DSNs may suppress the original recipient. Messages remain on the remote server.

JotForm integration previews bounded submissions and maps email/name/opt-out fields. Choose the correct official regional API host and explicitly confirm imports. API keys are sent in a header and are not stored as a reusable account connection.

## Analytics and tools
Optional engagement tracking adds signed click redirects and an open image for live SMTP messages. Images can be blocked, prefetched or cached; link scanners can click links. These are signals, not proof that a person read a message. UTM campaign tags support website analytics; this application does not connect to a Google Analytics account.

Workspace → Tools contains notes, image tools, DNS lookup, configured trusted shortcuts and database maintenance. Domain lookup does not measure reputation or inbox placement.

## Backups and secrets
Stop the local application and back up the entire workspace data directory, including the database, attachments, captures and protected secrets. Native/portable Windows secrets use current-user DPAPI: copying to a different Windows account is not sufficient for recovery. SQLite online backup covers the database only; copy assets/secrets separately. Keep and test PostgreSQL backups before hosted upgrades.

Legacy SQLite upgrades make a verified snapshot and record an interruption marker. On restart, an interrupted upgrade restores that verified database before retrying. Do not edit recovery markers or remove backups while recovering.

## Current support boundary
This is a single-workspace preview with one administrative key. It has no customer accounts, roles, automatic desktop/cloud sync or signed automatic updates. See FEATURE-MATRIX.md and VALIDATION-V02.md for precise gaps and verification.
