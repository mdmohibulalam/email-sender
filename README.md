# Campaign Studio — working preview 0.2.0

A Windows-first email campaign application with a C#/.NET backend and an optional hosted cloud workspace. This is the first implementation milestone, not a completed SendBlaster replacement.

The repository now includes the 0.2 implementation built on the recovered 0.1 baseline. New features include encrypted SMTP connections and tests, bounded safe retries, recorded attempts, database upgrades, signup forms, POP3 processing, JotForm import, programmable JavaScript tags, block/source editing, HTML/EML import, attachments, calendar events, segments, list moves, exclusions, reusable content, and maintenance tools. See `docs/VALIDATION-V02.md` for current verification. The separately distributed 0.1 portable ZIP does not include these changes.

## Try the portable Windows preview

1. Extract **CampaignStudio-0.1.0.zip** to a folder and open **Windows-Preview**.
2. Double-click **Start-CampaignStudio.cmd**. It starts a private loopback server and opens your browser automatically. No Visual Studio, .NET SDK, Node, XAMPP, or Docker is required for this portable bundle.
3. Keep the console window open while using the workspace. Closing it stops local campaign execution.
4. Import **sample-contacts.csv**, map the columns, enter consent source **Demo data**, and check the permission box. Expected result: 2 contacts added, 1 duplicate skipped, 1 invalid row rejected.
5. Create a campaign, write a subject/message, save, and choose **Review & schedule → Test capture**. Download the personalized .eml files from the report.

The Windows portable binary was cross-compiled on Linux. It has **not been launched on a Windows PC**. It opens a browser interface; it is not the native WinUI installer. If Windows warns about an unsigned download, publisher signing has not been completed. Report the actual error before treating the app as verified on your PC.

Your local portable data is saved under `%LOCALAPPDATA%\CampaignStudio-Portable`. Secrets are protected with Windows DPAPI for the current user. Native-shell data uses `%LOCALAPPDATA%\CampaignStudio`. The two previews use separate databases. Back up the database and protected unsubscribe secret together, while the app is stopped. DPAPI secrets cannot simply be copied to another Windows account.

## Implemented in 0.2

- Contact CSV mapping, deduplication, permission/source recording, suppression, paging, and history export.
- Saved audience segments with All/Any rules; list merge/move; address/domain exclusions; previous-campaign accepted/new-contact targeting.
- Quick layout, ordered draggable blocks, two-column text blocks, undo/redo, sanitized HTML source, HTML/EML import, personalized preview, revision-checked autosave.
- Reusable templates/snippets/notes; uploaded immutable attachments for all or specific recipients; UTC iCalendar attachments; UTM campaign tags.
- Encrypted SMTP profiles, required TLS, credential verification without sending, sender/reply-to/envelope-return identities, persistent pacing and retry history.
- Persisted scheduling, pause/resume/cancel, safe transient retries, failed-only manual retry, Unknown holds, final unsubscribe gate after authentication, explicit hard-bounce suppression.
- SQLite/PostgreSQL migrations, verified legacy SQLite recovery snapshots, local workspace ownership lock, SQLite online backup and database statistics maintenance.

## Current boundaries

This remains a single-workspace preview. Real-provider tests, inbox-client rendering, Windows runtime verification, native installer signing, and cloud deployment remain release gates. There is no customer account/tenant system, licensing, billing, automatic desktop/cloud synchronization, open tracking, direct-to-MX sending, 100-connection sending, VBScript tags, native MailStyler launcher, or image crop/filter tool in this checkpoint. UTM parameters measure website traffic through your analytics; SMTP acceptance does not prove inbox delivery.

HTML source is retained in drafts; outgoing HTML and previews are sanitized, so active markup is removed. Each contact has one list; list merges move contacts and preserve suppression. Retry never automatically resends uncertain SMTP jobs. Back up attachments/captures and the encryption/unsubscribe secret with the database. PostgreSQL production upgrades require a verified server backup.

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

`Run-Dev.ps1` copies the temporary API key to your clipboard. Open http://127.0.0.1:5088 and paste it. The development unsubscribe/encryption secret is retained using Windows DPAPI. Keep it with your workspace backups.

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
