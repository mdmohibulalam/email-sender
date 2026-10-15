# Windows Email Marketing Software — Development Blueprint

Prepared: 14 October 2026 (Asia/Dhaka)
Product model: Windows desktop + cloud, selected by the user.
Working title: Campaign Studio. This is a placeholder, not an approved brand.

## 1. Product direction

Build a native Windows email marketing application for businesses managing permission-based audiences. Users should be able to import contacts, clean a list, design an email, select a sender, review the audience, schedule a campaign, and understand the result without reading technical logs.

The differentiation should be usability, dependable execution, useful list-quality tools, and understandable reporting. Additional features alone do not establish a competitive advantage.

This deliverable is an architecture and implementation specification. No application, installer, cloud deployment, or sending provider has been created by this document.

The competitor review uses SendBlaster's public product pages. Its installed application, subscription services, and every edge case have not been tested. Create a hands-on parity checklist before describing the final product as matching every feature.

## 2. Competitor baseline

SendBlaster presents a desktop campaign application with online tracking integrations. The following condensed baseline comes from its official features, release notes, and edition comparison [S1–S4].

| Area | Published capabilities to cover |
| --- | --- |
| Authoring | Visual layout, HTML source, categorized templates, inline image editing |
| Content reuse | Drafts, snippets, HTML/EML import and HTML export |
| Personalization | Merge fields, programmable tags, recipient-specific attachments |
| Audience | Lists, custom fields, segments, imports, merging, moving |
| Recipient operations | Deduplication, filters, CSV export, list printing |
| Subscription | Web/email signup, unsubscribe handling, consent history |
| Suppression | Address/domain exclusions, bounces, cross-list unsubscribe |
| Transport | SMTP profiles, POP3 profiles, direct delivery, concurrent connections |
| Campaign execution | Scheduling, interruption recovery, sending history |
| Follow-ups | Previously contacted or newly added audience selection |
| Integrations | MailStyler, JotForm, website forms, analytics/tracking |
| Extras | Calendar attachments, widgets, backup, maintenance, password protection |

Compatibility can mean an equivalent user outcome. It does not require reproducing a third-party product's code, branding, proprietary template collection, or obsolete implementation choices.

## 3. Recommended technology

This is an engineering recommendation for a Windows-first product, not a claim that one framework is universally best.

| Layer | Proposed choice | Purpose |
| --- | --- | --- |
| Desktop application | C# + .NET 10 LTS | Strong Windows integration and a shared language for desktop/cloud |
| Native interface | WinUI 3 + stable Windows App SDK | Fluent controls, accessibility, DPI support, system themes |
| Desktop architecture | MVVM; isolated application/domain/infrastructure projects | Keep presentation separate from sending and persistence |
| Local database | SQLite + EF Core 10 | Offline contacts, drafts, cached reports, durable local jobs |
| Email designer | React + TypeScript inside WebView2 | Flexible block editing without replacing the native app shell |
| Email generation | MJML compiler plus controlled HTML import | Produce email HTML; MJML itself is not the visual editor |
| SMTP and mailbox access | MailKit + MimeKit | SMTP transport, MIME messages, IMAP/POP3 ingestion |
| Cloud API | ASP.NET Core 10 | Authentication, synchronization, forms, webhooks, tracking |
| Cloud database | PostgreSQL + EF Core/Npgsql 10 | Tenant data, consent, suppression, delivery state and job records |
| Cloud workers | .NET Worker Service + durable PostgreSQL jobs | Scheduling, controlled dispatch, event processing |
| Optional cache | Redis when measurements justify it | Cache and distributed rate coordination; not the only job record |
| Images/assets | S3-compatible object storage | Published campaign assets with retention controls |
| Secrets | Windows credential protection locally; cloud secret store | Keep credentials outside contact databases and exported backups |
| Release | Signed installer, versioned updates, Windows CI | Repeatable installation, repair, migration and update verification |
| Tests | xUnit, provider sandbox tests, Windows UI automation | Verify the real failure modes |

Microsoft identifies Windows App SDK as its recommended platform for new Windows desktop applications [S6]. .NET 10 is an LTS release, with support listed through November 2028 [S5]. Pin supported stable package versions during repository setup and maintain them.

WebView2 hosts the editor and preview [S7]. Keep imported email content in a separate restricted view without privileged native bridges. Bundle the editor for offline use. Prototype the local MJML compilation route early: a packaged compiler/runtime must work without asking end users to install Node.

Use a modular monolith API and separate worker processes initially. Avoid microservices until deployment or scaling needs justify them.

The user's existing Laravel experience can support a Laravel cloud API instead. ASP.NET Core is the default here because sharing language, contracts and domain logic reduces complexity for a new product. There is no need for PHP, XAMPP or WAMP on the customer's PC.

## 4. Desktop and cloud responsibilities

| Capability | Windows application | Cloud service |
| --- | --- | --- |
| Contacts | Offline editing, import/export, selected local storage | Explicitly synchronized workspace data |
| Email design | Drafts, editor, previews, validation | Asset publishing and campaign version storage |
| Local campaign execution | Sends while its worker is active and PC is online | Receives hosted unsubscribe and delivery events |
| Hosted campaign execution | Shows status and requests pause/cancel | Runs independently of the PC |
| Subscription | Shows consent and suppression state | Public forms, confirmation and unsubscribe endpoints |
| Reporting | Cached reports, export and investigation | Provider events, tracking and aggregation |

Every campaign chooses exactly one execution owner: local or cloud. Never run both queues for the same campaign. Transfers require a versioned handover and acknowledgment.

Cloud sending requires uploading the selected campaign and audience and providing an authorized sending connection. Explain those requirements in the product before scheduling.

A cloud-scheduled campaign continues when the Windows PC is shut down. A local campaign cannot send while the PC is off; closing or sleeping the PC must show its actual execution consequence.

The cloud must remain available for hosted unsubscribe endpoints even for campaigns sent locally. A local worker must synchronize suppressions before dispatch; pause sending when it cannot obtain sufficiently current suppression state.

## 5. Proposed product scope

These are design requirements for our product, not assertions that SendBlaster lacks them.

### A. Contacts and audiences

- Stream CSV/TXT imports with encoding detection, column mapping and a small preview.
- Add XLSX and JSON after the import pipeline is stable; require explicit mappings.
- Store custom fields, tags, list memberships, contact notes and subscription status.
- Normalize surrounding whitespace and domain representation; preserve original input.
- Do not merge all plus-addresses or Gmail-style dot variants by default.
- Offer duplicate-resolution previews, merge rules, undoable import batches and rejected-row exports.
- Support saved segments with AND/OR groups, counts and readable explanations.
- Freeze the intended audience at scheduling; still check suppression immediately before each send.
- Provide deterministic CSV export and print-friendly lists.
- Import historical unsubscribes and bounces as suppressions, not active subscribers.
- Record consent source, event time, list scope and confirmation history.
- Display import completion as a small success message with Close and Export results.

### B. List quality

Use separate filters for Syntax, DNS/MX, Provider classification, Subscription and Sending result.

Suggested DNS states: Not checked, Mail routing found, Null MX, Domain missing, No route found, Temporary DNS failure, and Unknown. Correctly handle the SMTP implicit-MX fallback where applicable. A DNS timeout is not proof of an invalid mailbox.

Suggested other classifications: duplicate, role address, disposable domain, free-mail provider, hard bounce and suppressed. Attach the observation time and reason to every result.

MX or domain checks cannot prove a mailbox exists. Catch-all and SMTP-probe results can be uncertain. Avoid presenting "verified" as a guarantee of delivery. A paid verification integration can be optional, with explicit data transfer and usage cost.

Cache DNS results according to record lifetimes, limit concurrency, allow cancellation and export the exact filter selection.

### C. Email designer

- Blocks: text, heading, image, button, divider, spacer, columns, social links and footer.
- Template library with original or appropriately licensed designs.
- Brand presets, reusable blocks, snippets, undo/redo, autosave and version history.
- Merge-field insertion with fallback values and sample-contact preview.
- Mobile/desktop width previews; actual mail-client test messages for rendering validation.
- HTML source mode, plain-text alternative, controlled HTML/EML import and export.
- Preserve original imported source; avoid promising lossless round trips through a visual editor.
- Image crop/resize/compression and a published-asset library.
- Standard attachments, validated per-contact attachments and calendar invitations.
- Link validation, missing-image checks, missing-merge-data checks and message-size estimates.
- Conditional personalization through a constrained expression system. Do not run arbitrary imported JavaScript or VBScript with desktop privileges.

MJML generates email HTML; it does not eliminate the need for Gmail, Outlook and Apple Mail testing [S8].

### D. Sending connections

- Named SMTP profiles with TLS, authentication testing and secure credential storage.
- Sender identity, reply-to, provider-dependent envelope sender, and authorized domain selection.
- OAuth-capable mailbox connections where the chosen provider supports them.
- Start with SMTP; add provider APIs for better delivery events and provider idempotency.
- Model each provider's rate, concurrency, daily quota and retry semantics.
- Support custom domains for links and unsubscribe endpoints when configured.
- Domain setup assistant for SPF, DKIM and DMARC with actionable observations.
- Mailbox connections for subscription messages and bounce ingestion.
- Keep direct-to-recipient-MX sending as a later specialist option only if a valid operational need is established. Building a full mail transfer agent is a separate delivery/infrastructure project.
- Never rotate identities or providers to evade rate limits.

### E. Campaign execution

Wizard: Audience → Design → Sender → Review → Send/Schedule.

Review shows eligible recipients, exclusions and their reasons, selected sender, campaign size, sample personalization, tracking choice, unsubscribe footer, timezone and execution location.

Provide draft, schedule, pause, resume, cancel, duplicate and follow-up actions. Store IANA timezone plus the resolved UTC schedule and show daylight-saving ambiguity when relevant.

Use a durable recipient ledger with Pending, Claimed, Sending, Accepted, Deferred, Failed, Unknown, Suppressed and Cancelled states. Delivery and engagement events are separate records.

Persist campaign ID, recipient ID, message version, stable message ID, execution owner, attempts, lease expiry, provider ID and next-attempt time. A unique campaign/recipient/message key prevents creating duplicate jobs.

A plain SMTP send cannot guarantee exactly-once delivery. A server may accept a message just before a connection or process fails. Mark that ambiguous outcome Unknown; do not blindly retry it. Reconcile through provider events where possible or require a deliberate retry decision. Use provider idempotency where available.

Apply bounded exponential backoff with jitter to eligible transient failures. Respect provider Retry-After instructions. Suppress permanent recipient failures. Suspend a connection after authentication or persistent policy failures.

Recheck unsubscribe and complaint suppression at dispatch. Pause prevents further dispatch after acknowledgment; messages already accepted cannot be recalled.

### F. Hosted subscriptions and integrations

- Hosted subscription forms and embeddable website forms with confirmation flow.
- Public unsubscribe/preference pages and one-click unsubscribe POST handling.
- Workspace-wide suppression with clearly named optional list preferences.
- Signed webhooks with deduplication, replay protection, tenant isolation and idempotent updates.
- Provider bounce/complaint ingestion and a mailbox fallback where supported.
- APIs for website customer/signup events and approved commerce/CRM integrations.
- Follow-up segments based on explicit prior campaign history.
- Add JotForm integration or an import equivalent after confirming current API access.
- An external-editor workflow can replace a dedicated MailStyler integration if licensing or integration access is unavailable.

Do not unsubscribe on a tracking link's GET request alone: scanners and preview services fetch links automatically. The actual one-click unsubscribe protocol uses POST [S9].

### G. Reports

Report Accepted, Delivered where supported, Deferred, Hard bounce, Soft bounce, Complaint, Unsubscribed, Observed open and Observed click.

Accepted means the transport accepted the message. It does not prove inbox placement. For SMTP providers without delivery events, show delivery status as unavailable.

Privacy features affect open observations [S12]. Security scanners can also visit links. Label engagement measurements as observations, explain limitations, and separate suspected automated events where possible. Do not promise accurate reading time.

Provide campaign comparisons, provider/domain breakdowns, recipient history, time-series charts, CSV export and UTM link support. Add A/B tests after reporting semantics and audience randomization are tested. Define success metrics and minimum sample sizes before selecting a winner.

### H. Commercial product operations

- Workspaces, tenant boundaries, role permissions and optional campaign approvals.
- Signed installation and update artifacts with staged rollout and recovery.
- License activation, trial behavior, device management and offline grace period.
- Encrypted backups with tested restoration; preserve suppression history after restores.
- Diagnostics with credential redaction and opt-in support bundles.
- Data retention, account deletion and export workflows.
- Original English interface plus a localization framework; add Bangla if market demand supports it.
- Optional AI assistance for subject/content drafts and plain-language segmentation. Keep it optional and human-reviewed.
- Keyboard access, screen-reader labels, high contrast, light/dark themes and DPI scaling.

A lifetime desktop license and ongoing hosted services have different economics. Sending-provider charges, hosting, storage and optional AI usage must be accounted for separately.

## 6. Interaction design

Navigation: Overview, Contacts, Campaigns, Templates, Connections, Reports, Settings and Help.

Use consistent typography, quiet colors, readable density and restrained emphasis. Provide searchable, virtualized contact lists. Keep import/send progress visible with cancellation and recovery.

Show simple controls first and advanced options on demand. A dropdown needs a clear label and an explanation of the effect of its selection.

Every option needs help covering: what it does, when to use it, its default, an example, and what changes in the result. Link help from the relevant control, not only from a separate guide.

Avoid large completion dialogs. Keep detailed results on the relevant screen, with concise completion feedback.

## 7. Data model

| Entity | Key responsibility |
| --- | --- |
| Workspace/User/Role | Ownership and access |
| Contact/CustomField/List/ListMember/Tag | Audience data |
| ConsentEvent/Suppression | Immutable consent evidence and sending exclusions |
| ImportBatch/ImportRowIssue | Recovery, audit and rejected rows |
| Segment/SegmentRule | Saved audience queries |
| Template/TemplateVersion/Asset | Versioned content and media |
| SenderConnection/SenderIdentity | Authorized transports; secret references |
| Campaign/CampaignVersion | Immutable scheduled content |
| CampaignRecipient | Audience snapshot and durable delivery state |
| SendAttempt | Transport attempts and ambiguous outcomes |
| DeliveryEvent/EngagementEvent | Provider and observed tracking facts |
| SubscriptionForm/ConfirmationToken | Hosted signup |
| SyncOutbox/SyncCursor | Reliable desktop synchronization |
| License/Device/AuditEvent | Commercial operation and accountability |

All hosted records require explicit workspace ownership checks. Do not accept a client-supplied tenant identifier as proof of authorization.

Do not resolve suppression or consent conflicts with generic "last write wins." A suppression should override a stale subscription snapshot; a valid later resubscription requires explicit new consent.

## 8. Development order and acceptance gates

| Milestone | Deliverable | Required proof before expanding |
| --- | --- | --- |
| 1. Desktop foundation | Native shell, contacts, CSV import/export, draft persistence | Windows build/install; restart preserves data; large import does not freeze UI |
| 2. Campaign authoring | Designer, originals templates, merge preview, connection settings | Offline editor; rendering checks; secrets absent from exports |
| 3. Local sending pilot | Durable SMTP queue and controlled test sending | Crash/restart tests; rate-limit tests; ambiguous SMTP outcome handled |
| 4. Cloud foundation | Login, forms, unsubscribe, provider events, hosted execution | Tenant-isolation tests; unsubscribe blocks pending jobs; PC-off scheduling |
| 5. Competitive parity | Remaining baseline imports, mailbox flows, attachments, segments and integrations | Hands-on parity checklist with evidence |
| 6. Differentiation | List-quality tools, automation, A/B testing, improved analytics | Measured usability and result-quality checks |
| 7. Commercial release | Licensing, installer, updates, backup/restore, documentation | Clean-PC install; upgrade/rollback; restore preserves suppressions |

Start with one chosen provider and controlled pilot campaigns. Add transports after their behavior is covered by a provider contract test suite.

This is a multi-milestone commercial application. A credible delivery estimate follows the first Windows prototype, selected provider tests and an agreed release checklist; no fixed schedule is promised here.

## 9. Test plan

- Imports: malformed rows, quoted multiline CSV, different encodings, duplicate rules, cancellation and restart.
- List quality: transient DNS failures, null MX, implicit MX fallback and cached observations.
- Scheduling: UTC conversion, DST transitions, PC sleep and selected execution owner.
- Dispatch: worker crash before/after acceptance, lease expiry, retries, rate quotas and pause acknowledgment.
- Suppression: unsubscribe during a running campaign, stale desktop cache, duplicate webhooks and restore from backup.
- MIME: text/HTML alternatives, Unicode, attachments, header injection prevention and merge escaping.
- Isolation: unauthorized workspace access and event/recipient identifier substitution.
- Designer: keyboard navigation, high DPI, screen readers, protected preview and native-bridge restrictions.
- Release: signed installer, clean install, database migrations, update recovery and backup restoration.
- Performance: progressively test 10k, 100k and 1m-contact datasets on a documented reference PC. Record import time, query latency, memory, responsiveness and disk use rather than claiming unlimited capacity.

No real campaign should be sent simply to test throughput without an explicitly selected test audience and sender.

## 10. Repository and local setup

Proposed repository projects:

- CampaignStudio.Desktop — WinUI application and view models.
- CampaignStudio.Domain — contacts, consent and campaign state rules.
- CampaignStudio.Application — import, scheduling, suppression and campaign workflows.
- CampaignStudio.Infrastructure — SQLite, MailKit and OS credential adapters.
- CampaignStudio.Api — authenticated cloud APIs and public subscription endpoints.
- CampaignStudio.Worker — durable job execution and event reconciliation.
- CampaignStudio.Contracts — versioned desktop/API contracts.
- CampaignStudio.EmailDesigner — React/TypeScript editor and MJML pipeline.
- CampaignStudio.Tests — meaningful unit, integration and contract tests.
- docs — user guide, delivery semantics, release runbook and parity evidence.

Development needs a Windows machine with supported Visual Studio/Windows tooling, .NET 10 SDK and Windows SDK. Build and UI-test the WinUI app on Windows CI. Use Docker for local cloud dependencies, not as a requirement for desktop customers.

Keep production credentials outside source control. A sandbox SMTP server and provider sandbox should cover development sending. Installable releases require the publisher's code-signing identity and a chosen update distribution path.

## 11. Sources

Competitor review:
- [S1] https://www.sendblaster.com/
- [S2] https://www.sendblaster.com/desktop-e-mail-marketing-software-features/
- [S3] https://www.sendblaster.com/whats-new-in-sendblaster-4/
- [S4] https://www.sendblaster.com/comparative-table-free-newsletter-software-vs-pro-bulk-emailer/

Technical basis:
- [S5] https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- [S6] https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/
- [S7] https://learn.microsoft.com/en-us/microsoft-edge/webview2/
- [S8] https://mjml.io/
- [S9] https://support.google.com/mail/answer/81126?hl=en
- [S10] https://github.com/jstedfast/MailKit
- [S11] https://www.npgsql.org/efcore/release-notes/10.0.html
- [S12] https://www.apple.com/legal/privacy/data/en/mail-privacy-protection/
- [S13] https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0

These sources establish the published baseline and technical capabilities. The proposed architecture, workflows and release gates are our design decisions. Source availability and supported stable package versions must be checked again at implementation.

