# Campaign Studio 0.2.2 — working preview

Windows email-campaign software with a .NET API, a native WinUI/WebView2 shell, and an optional PostgreSQL-backed hosted workspace. This is a single-workspace preview, not a finished commercial service.

The main campaign workflows are implemented: contact import/segments/suppressions, block and HTML design, image editing, EML import, personalized attachments, calendar events, reusable content, encrypted SMTP/POP3 profiles, double-opt-in forms, JotForm import, scheduling/retries, parallel dispatch, reporting, optional engagement signals and maintenance tools.

Version 0.2.2 fixes form dropdown sizing/alignment, audience sort labels, connection examples and Review & schedule validation/error handling. See [0.2.2 validation](docs/VALIDATION-V022.md).

Version 0.2.1 moves Connections beside Workspace and adds Settings/Help dropdowns, saved appearance palettes, Light/Dark/System themes, searchable help, keyboard shortcuts, workspace/license status and manual release checks. See [0.2.1 validation](docs/VALIDATION-V021.md).

**Start here:** [User guide](docs/USER-GUIDE.md) · [42-feature comparison](docs/FEATURE-MATRIX.md) · [Validation evidence](docs/VALIDATION-V02.md) · [Windows release checks](docs/WINDOWS-CHECKLIST.md).

## Run and test
Install a patched .NET 10 SDK. The native build additionally requires Windows SDK/WinUI tooling on Windows.

```powershell
dotnet run --project tests/CampaignStudio.Tests
./scripts/Run-Dev.ps1
# On Windows:
./scripts/Build-Windows.ps1
```

Run-Dev.ps1 starts localhost:5088 and copies a temporary workspace key to your clipboard. Functional tests use temporary data, a local TLS SMTP sandbox and example addresses; they do not send real emails.

```sh
cd tests/browser
npm ci
npx playwright install --with-deps chromium
npm test
# From repository root:
bash scripts/Test-Docker.sh
```

To exercise PostgreSQL integration, set STUDIO_TEST_POSTGRES to a connection string for an expendable test database before running the functional suite. CI runs it against PostgreSQL 17.

On Linux/macOS, set Studio__ApiKey and Studio__TokenSecret to distinct random values of at least 32 characters, set Studio__DataDirectory, and run:
```sh
dotnet run --project src/CampaignStudio.Api -- --urls http://127.0.0.1:5088
```

## Windows packages
The GitHub Actions workflow builds the native shell and self-contained portable API, launches both on Windows, then builds an unsigned Inno Setup installer. Download packages only from a successful workflow run.

The native executable is CampaignStudio.Desktop.exe. The portable package uses Start-CampaignStudio.cmd and opens a browser; keep its console open. The installer offers optional desktop and minimized login shortcuts. WebView2 Runtime must be available; an offline runtime is not bundled.

Local native data: %LOCALAPPDATA%\CampaignStudio. Portable data: %LOCALAPPDATA%\CampaignStudio-Portable. The packages use separate workspaces. Windows secrets use current-user DPAPI: back up the data directory while stopped, including protected secrets, database, images, attachments and captures. Restoring under another Windows account requires a migration strategy.

The previously shared CampaignStudio-0.1.0.zip is historical and does not contain the current features. This repository's 0.2 build artifacts supersede it.

## Configure a cloud pilot
No public cloud instance has been provisioned. Copy .env.example to .env, replace the example secrets/passwords, and run docker compose up --build -d. The compose file uses PostgreSQL 17, durable volumes and localhost port 8080. Put an HTTPS reverse proxy in front of it. Start with one app instance and capture mode. Test full database/assets/secret backups and restores.

For the native shell to use that hosted workspace, configure CAMPAIGN_STUDIO_CLOUD_URL as an HTTPS origin and CAMPAIGN_STUDIO_CLOUD_KEY as its administrative key. You may also open the hosted URL in a browser. This does not synchronize a local database to the cloud.

## Delivery settings
Use Workspace → Connections to save and verify an authorized SMTP profile. STARTTLS is mandatory except implicit TLS on port 465; normal certificate verification remains enabled. Sender/reply-to/envelope identities, pacing, retries and parallel connection caps are configurable.

Live delivery also requires Studio__EnableLiveSending=true and Studio__PublicBaseUrl with a reachable HTTPS base URL. The native shell accepts these environment variables too. Capture mode remains the default. Use an authorized test audience and validate SPF/DKIM/DMARC plus actual received MIME before sending campaigns.

Studio__MaxConcurrentSends controls global parallelism (default 4; range 1–100). Each profile has its own cap and pacing. This is a configuration bound, not evidence of 100-connection throughput.

Optional direct-to-MX delivery additionally requires Studio__EnableDirectSending=true, outgoing port 25, public recipient MX addresses and STARTTLS. No live direct-delivery interoperability test has been performed. Ordinary provider SMTP is the recommended default for this preview.

External designer integration accepts Studio__ExternalDesignerExecutable (trusted Windows executable) and Studio__ExternalDesignerOutput (exported HTML path). Tools shortcuts are configured by Studio__ShortcutsJson, an operator-provided name-to-executable mapping. Browser clients cannot submit arbitrary executable paths.

## Reliability and limits
Claims and attempts are durable. Known transient failures may retry with bounded backoff. Unknown SMTP outcomes are held rather than automatically resent. Accepted means server acceptance, not inbox delivery. Global suppression is checked again after authentication and before SMTP DATA.

Draft source HTML is retained but outgoing HTML is sanitized. JavaScript tags are bounded expressions without computer/network access; VBScript is unsupported. EML import covers supported MIME inline images and attachments, not every proprietary mail-client construct.

Signed open images and click redirects are optional for live campaigns. Image blocking, privacy proxies and link scanners affect results. Reports do not claim reliable human opens, reading time or Google Analytics account integration. UTM parameters support website analytics separately.

No accounts, roles, tenant isolation, billing, licensing, automatic updates or desktop/cloud synchronization are implemented. Signing, clean-PC verification, external service interoperability, inbox rendering, production monitoring and scale testing remain release work. See the feature matrix before treating this as full SendBlaster parity.

SQLite upgrades use verified recovery snapshots because some schema rebuild operations cannot be atomic. PostgreSQL migrations are included; production PostgreSQL backups/restores remain operator responsibilities.

Working title “Campaign Studio” is provisional. No competitor code, templates or branding are included.
