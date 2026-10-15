# Validation — Campaign Studio 0.2
Date: 2026-10-15. **All three CI jobs passed. No real email was sent.**

## Reproducible evidence
[Successful workflow 38698313858](https://github.com/mdmohibulalam/email-sender/actions/runs/38698313858) tests source commit [9a5e9084cf3fa8de2e1347038ac98ae9ed417baf](https://github.com/mdmohibulalam/email-sender/commit/9a5e9084cf3fa8de2e1347038ac98ae9ed417baf). The subsequent documentation-only commit records these results without changing tested code.

| Area | Observed result |
| --- | --- |
| Linux functional/integration suite | 121 checks passed, including six real PostgreSQL checks |
| Windows functional suite | 115 checks passed; PostgreSQL is tested separately on Linux |
| Chromium browser workflows | 18 checks passed, including Cancel behavior, image editing and 360px overflow |
| API/native dependency audits | No known vulnerable packages reported by configured NuGet sources |
| npm audit | Zero reported vulnerabilities |
| Docker | Build/start, health, bearer authentication, writable data and non-root execution passed; live sending disabled |
| Portable Windows package | Self-contained publish, isolated startup, migrations, PID-matched ready handshake and authenticated settings passed |
| Native Windows package | Publish and actual WinUI startup passed; local API, DPAPI, authenticated WebView and rendered workspace verified |
| Inno Setup installer | Compiled; silent installation, installed native startup and uninstall passed on the Windows CI runner |
| Download artifact | CampaignStudio-native-windows-preview contains native, portable and installer folders |

The six PostgreSQL checks cover contact import deduplication, recipient capture, attempt recording, migration/model consistency, empty engagement aggregation and persistence across host restart. The service is PostgreSQL 17, not an emulator.

## Meaningful regression coverage
- Global suppression/exclusions and a final check after SMTP authentication but before DATA.
- Durable claims, deferred backoff, known-failure-only retry and Unknown outcome holds.
- Two concurrent sender connections, no premature completion and one recorded attempt per accepted recipient.
- Checksummed PNG upload, signed image URLs and MIME CID embedding.
- Signed engagement links, forged redirect rejection and distinct recipient reporting.
- Binary EML attachments, supported inline images and outgoing plain-text link URLs.
- Validated multi-field sort and foreign keys rejecting orphan attempts.
- Legacy SQLite upgrade and verified snapshot restoration after simulated partial schema/data changes.
- Direct-delivery MX ordering/null-MX handling and disabled-by-default operation.
- Block editing, live contact preview, image filters, draft save/reload, autosave, scheduling capture, EML access, attempts, reusable content, categories, public form consent and browser error checks.
- Cancel buttons do not submit connection forms.

Production SMTP transport is exercised against a local TLS sandbox: certificate rejection, verification without sending, acceptance before QUIT disconnect, transient failure, permanent missing recipient, policy rejection, authentication failure, uncertain disconnect and the pre-DATA gate. The sandbox never relays. Injected transport checks test queue behavior rather than external deliverability.

## Practical limits
Passing assertions do not prove every mail client or service interoperates correctly. SQLite FK rebuilds emit a non-transactional migration warning; snapshot/marker recovery is implemented and tested instead of claiming the rebuild is atomic.

The Windows runner verifies actual package startup and installation, but is not a clean Windows 10/11 consumer-PC or accessibility certification. The installer is unsigned and requires WebView2 Runtime.

No live-provider SMTP/POP3/JotForm test, actual MailStyler run, public cloud deployment, sender-domain authentication, inbox rendering certification, reliable human reading-time measurement or 100-connection load benchmark is claimed. See FEATURE-MATRIX.md for all 42 capability mappings and remaining production work.
