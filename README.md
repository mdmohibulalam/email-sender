# Campaign Studio — working preview 0.1.0

A Windows-first email campaign application with a C#/.NET backend and an optional hosted cloud workspace. This is the first implementation milestone, not a completed SendBlaster replacement.

This repository contains the recovered **0.1.0 source baseline**. The portable download described below is distributed separately; compiled executables are not committed here. See [source status](docs/SOURCE-STATUS.md) for recovery and verification details.

## Try the portable Windows preview

1. Extract **CampaignStudio-0.1.0.zip** to a folder and open **Windows-Preview**.
2. Double-click **Start-CampaignStudio.cmd**. It starts a private loopback server and opens your browser automatically. No Visual Studio, .NET SDK, Node, XAMPP, or Docker is required for this portable bundle.
3. Keep the console window open while using the workspace. Closing it stops local campaign execution.
4. Import **sample-contacts.csv**, map the columns, enter consent source **Demo data**, and check the permission box. Expected result: 2 contacts added, 1 duplicate skipped, 1 invalid row rejected.
5. Create a campaign, write a subject/message, save, and choose **Review & schedule → Test capture**. Download the personalized .eml files from the report.

The Windows portable binary was cross-compiled on Linux. It has **not been launched on a Windows PC**. It opens a browser interface; it is not the native WinUI installer. If Windows warns about an unsigned download, publisher signing has not been completed. Report the actual error before treating the app as verified on your PC.

Your local portable data is saved under `%LOCALAPPDATA%\CampaignStudio-Portable`. Secrets are protected with Windows DPAPI for the current user. Native-shell data uses `%LOCALAPPDATA%\CampaignStudio`. The two previews use separate databases. Back up the database and protected unsubscribe secret together, while the app is stopped. DPAPI secrets cannot simply be copied to another Windows account.

## Implemented

- Quiet, responsive interface with top navigation, contextual empty states, and a complete guide for the available controls.
- Contacts, one-list-per-contact organization, permission/source recording, paging, search, status/list filters, and cross-workspace suppression.
- UTF-8 CSV import with column mapping, quoted multiline parsing, deduplication, rejected-row exports, and a compact success dialog with Close and Export list.
- Original newsletter, announcement, and welcome templates. Structured editor for heading, body, button, accent, preheader, footer, and merge fields.
- Safe HTML generation, desktop/mobile preview, text alternative, personalized MIME email, draft persistence, and HTML/CSV exports.
- Durable recipient snapshots, scheduling in UTC, pause/resume/cancel, duplicate-as-draft, and test capture files.
- TLS SMTP implementation behind server configuration and per-campaign confirmation. Live SMTP is **not verified against a real provider**.
- Signed unsubscribe tokens, confirmation GET, one-click POST, and suppression of pending recipients.
- SMTP acceptance is reported separately from delivery. Interrupted sends become Unknown and are not automatically retried.
- SQLite local storage, PostgreSQL adapter for hosted instances, Docker configuration, and a WinUI 3/WebView2 shell source project.

## Boundaries of this version

- One workspace and one administrator access key per server. No account login, tenant management, team roles, licensing, billing, or automatic local/cloud synchronization.
- The email editor is a bundled HTML/JavaScript interface backed by C# HTML rendering. It does not yet use the blueprint's React/MJML designer, drag-and-drop blocks, attachments, arbitrary HTML import, image uploads, or template versioning.
- No delivery webhooks, bounce/complaint ingestion, engagement tracking, DNS/MX validation, automated follow-ups, A/B tests, or provider rate/quota profiles. Live transport sends serially with a minimum one-second interval; this is not a throughput claim.
- Failures are held for review. Bounded automatic retries and provider reconciliation are future work. A duplicate campaign is a new send: confirm that no Unknown recipient was already accepted before using it as a retry.
- Desktop local execution requires the PC/server to stay on. Hosted execution continues while the cloud server remains running, even if the Windows PC is off. Only that server's audience/drafts participate; no synchronization is implied.
- PostgreSQL configuration is supplied but has not been integration-tested here. Database initialization uses `EnsureCreated` for a new pilot database; schema migrations and upgrade/rollback paths are not implemented.
- The native WinUI shell, portable launcher, clean-PC installation, code signing, and Windows UI accessibility require Windows testing. There is no signed installer in this release.

## Source projects

| Project | Responsibility |
| --- | --- |
| `CampaignStudio.Core` | Contact rules, CSV parsing, models, merge fields, safe email rendering |
| `CampaignStudio.Api` | Authenticated APIs, database, unsubscribe endpoints, worker, bundled interface |
| `CampaignStudio.Desktop` | Native WinUI window hosting the local or cloud interface through WebView2 |
| `CampaignStudio.Tests` | Executable functional/integration checks using a temporary SQLite database and loopback API |

## Build and test

Install a supported patched **.NET 10 SDK**. On Windows, the native project also needs the Windows SDK and Windows App SDK/WinUI development tools. Use the Windows checklist before calling the native shell usable.

```powershell
dotnet run --project tests/CampaignStudio.Tests
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Run-Dev.ps1
# Native Windows shell (run on Windows):
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Windows.ps1
```

`Run-Dev.ps1` copies the temporary API key to your clipboard. Open http://127.0.0.1:5088 and paste it. This development script creates a new unsubscribe secret each run; use the portable launcher or a stable configured secret for persistent hosted links.

On Linux/macOS, supply `Studio__ApiKey`, `Studio__TokenSecret`, and `Studio__DataDirectory`, then run:

```sh
dotnet run --project src/CampaignStudio.Api -- --urls http://127.0.0.1:5088
```

Both secrets must be distinct random values at least 32 characters long. Do not put production secrets in source control. Tests use temporary data and example addresses; they send no real email.

## Host a cloud pilot

The cloud source is ready to configure for a pilot. **No live cloud instance has been provisioned or deployed.** A server, domain, HTTPS certificate, durable PostgreSQL database, and SMTP account are needed for a live service.

1. Copy `.env.example` to `.env`; replace all example values with separate strong random secrets. Use a hexadecimal database password to avoid connection-string delimiters.
2. Run `docker compose up --build -d`. It binds the application to `127.0.0.1:8080`; PostgreSQL is not publicly exposed.
3. Place an HTTPS reverse proxy on your domain that forwards to that local port. Use one app instance/worker during this pilot. Back up PostgreSQL, captures, and the unsubscribe secret. Docker deployment has not been run in this environment.
4. Keep Capture mode until the real-provider test checklist passes. For the native desktop cloud connection, set `CAMPAIGN_STUDIO_CLOUD_URL=https://your-domain` and `CAMPAIGN_STUDIO_CLOUD_KEY=<workspace key>` before starting the native shell. You can also open the cloud URL in a browser and sign in with that key.
5. Before production use, implement account authentication, schema migrations, provider events, operational monitoring, domain authentication checks, and the remaining release gates from the original blueprint.

### SMTP configuration

Set server environment variables below, outside the repository, and restart the service. The ready check requires live sending enabled, a sender email, a host, and an HTTPS public base URL. That check does not validate domain authentication or credentials. STARTTLS is required on ports other than 465; implicit TLS is used on 465. Certificate validation is not disabled.

```text
Studio__EnableLiveSending=true
Studio__SmtpHost=your-authorized-smtp-host
Studio__SmtpPort=587
Studio__SmtpUser=your-username
Studio__SmtpPassword=your-secret
Studio__FromEmail=your-authorized-sender
Studio__FromName=Your company
Studio__PublicBaseUrl=https://your-domain
```

Use an authorized test audience first. Configure SPF/DKIM/DMARC through the provider and verify one-click unsubscribe/header signing with actual received messages. No provider test, domain setup, or real campaign was performed while generating this deliverable.

For hosted builds, credentials and suppressions belong to this workspace. The pilot does not isolate multiple customers in one database; use a separate instance/database/key if evaluating another workspace.

## Validation evidence

See `docs/VALIDATION.md` for actual build and check results. See `docs/WINDOWS-CHECKLIST.md` for checks still required. The original blueprint is included under `docs` to keep future milestones visible.

Working title “Campaign Studio” is provisional. No publisher identity, product license, or competitor branding has been adopted.
