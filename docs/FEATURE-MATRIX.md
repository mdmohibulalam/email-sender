# Feature comparison — Campaign Studio 0.2
Reviewed against [SendBlaster's feature page](https://www.sendblaster.com/desktop-e-mail-marketing-software-features/) on 2026-10-15. This compares capabilities, not shared source code or copied assets.

**Implemented** means a code path exists with automated coverage of its main workflow. **Partial** means a narrower equivalent or incomplete integration. External services, delivered-message rendering, Windows installer trust, and scale require additional evidence. SMTP acceptance does not establish inbox delivery.

| # | Reference capability | Status | Campaign Studio implementation and boundary |
| --- | --- | --- | --- |
| 1 | Layout editor | Implemented | Quick layout and ordered blocks; headings, text, images, buttons, columns, dividers and spacing; undo/redo. |
| 2 | Source-only editing | Partial | Draft retains source; preview and output are sanitized. Arbitrary HTML/VML is not preserved byte for byte. |
| 3 | Template categories | Implemented | Categorized, editable library with nine original starter designs. |
| 4 | Image adjustment | Implemented | In-app crop, resize, brightness and grayscale; saves a new immutable PNG and embeds it in MIME. |
| 5 | HTML rendering engine | Partial | Escaped table-based block output and sanitized source preview. No certification across Outlook, Gmail and Apple Mail. |
| 6 | Ready-made templates | Partial | Nine original designs, not an equivalent large commercial collection. |
| 7 | Per-recipient attachments | Partial | Immutable uploads can be assigned to exact recipient emails; no arbitrary local-path expressions. Five MB per upload, twenty files/twenty MB per campaign. |
| 8 | MailStyler handoff | Partial | Trusted configured Windows executable is launched and its exported HTML loaded after exit. Actual MailStyler interoperability untested. |
| 9 | HTML and EML import | Implemented | HTML source plus binary-safe MIME import, subject, attachments and supported inline image references. Proprietary Outlook/CSS constructs are not guaranteed. |
| 10 | Recipient personalization | Implemented | Email/name/company fields, live contact preview and distinct recipient MIME. |
| 11 | Scripted tags | Partial | Bounded JavaScript expressions with contact data; no VBScript, CLR access, filesystem or network. |
| 12 | Calendar attachments | Implemented | Escaped UTF-8 folded UTC iCalendar output. |
| 13 | JotForm connection | Partial | Field preview, confirmed subscription/opt-out import and regional official API hosts. No live account verification yet. |
| 14 | Link checks | Implemented | Extracts rendered links; public HTTPS checks with public-address filtering and bounded requests. |
| 15 | Saved audience segments | Implemented | All/Any rule groups, validated fields and preview. |
| 16 | Unsubscribe propagation | Implemented | Canonical email has global suppression, including final dispatch gate and list moves. |
| 17 | Contact CSV import | Implemented | Header mapping, validation, consent source, duplicate accounting and export. |
| 18 | Follow-up audiences | Implemented | Target recipients accepted in a selected previous campaign. |
| 19 | Website subscriptions | Implemented | Public form, signed expiring double-opt-in confirmation and separate confirmation queue. Public HTTPS hosting is required outside localhost. |
| 20 | Email-based subscribe/opt-out | Partial | TLS POP3 profiles, UIDL receipts, subscribe confirmation, unsubscribe suppression and automatic polling. Live mailbox compatibility untested. |
| 21 | Advanced sorting | Implemented | Up to three validated sort fields/directions and stable ID tie-breaker; UI selectors rather than drag-to-sort. |
| 22 | List moves and merges | Implemented | Rule/segment moves and merge; each canonical contact belongs to one list and retains suppression. |
| 23 | Bounces and exclusions | Implemented | Address/domain exclusions; explicit matched 5.1.1 DSNs/hard recipient rejection suppress; policy failures are not treated as invalid addresses. Provider-specific DSNs need live checks. |
| 24 | Separate bounce address | Implemented | Configured SMTP envelope sender and reply-to identities; final Return-Path is set by the receiving server. |
| 25 | List and recipient capacity | Partial | No paid list-count limit; paged queries. No unlimited-scale claim or production load benchmark. |
| 26 | Consent history | Implemented | Dated import, form, opt-out and bounce events; history CSV export. |
| 27 | Previously unmailed audience | Implemented | Excludes accepted and uncertain recipients from a selected campaign to avoid accidental duplicates. |
| 28 | SMTP and POP3 profiles | Implemented | Multiple encrypted profiles, credential checks, pacing, bounded retries and inbox processing. External-provider verification remains. |
| 29 | Direct delivery | Partial | Optional MX ordering/null-MX handling with public-IP filtering, port 25 and mandatory STARTTLS; disabled by default. No live delivery test. |
| 30 | Duplicate prevention | Implemented | Canonical email uniqueness, conditional claims, attempt journal, no automatic resend after uncertain SMTP acceptance. |
| 31 | Segment dispatch | Implemented | Audience snapshot uses selected saved segment and suppression/exclusion rules. |
| 32 | Scheduling/startup | Implemented | Persisted schedule, drafts, pause/resume/cancel, optional minimized Windows login shortcut. App/service must remain running. |
| 33 | Interrupted campaign recovery | Implemented | Durable claims/leases, safe transient retries and capture reconciliation. Uncertain SMTP outcomes are held for manual review. |
| 34 | Delivery logs | Implemented | Recipient outcomes, attempt timeline, CSV and personalized EML download. |
| 35 | Parallel connections | Partial | Global/per-sender limits up to 100, pacing and cap tests. No 100-connection throughput benchmark. |
| 36 | Analytics integration | Partial | UTM campaign parameters plus optional signed open-image/click signals. No Google Analytics account/report connector or reliable human reading-time measurement. |
| 37 | Database tools | Implemented | SQLite online backup, integrity/statistics/optimization; provider migrations and verified SQLite upgrade recovery. PostgreSQL backups are operational tasks. |
| 38 | Utility widgets | Partial | Persistent notes, image editing, trusted program shortcuts and DNS domain lookup. Different layout; no Alexa integration. |
| 39 | Reusable text | Implemented | Saved snippets, notes and templates. |
| 40 | Saved drafts | Implemented | Revision-checked autosave, reload and preview. |
| 41 | General engine improvements | Partial | Migration/retry/concurrency/UI checks exist; this is not proof of commercial maturity or parity with unspecified internal optimizations. |
| 42 | Windows support | Partial | Native WinUI/WebView2 and self-contained portable builds with automated Windows checks. Unsigned; clean Windows 10/11 PC and accessibility verification remain release gates. |

## What is still outside this milestone
- Customer accounts, roles, tenant isolation, billing, licensing, automatic update delivery and desktop/cloud synchronization.
- A deployed public cloud instance, production monitoring/alerts, and tested disaster-recovery operations.
- Real-provider SMTP/POP3/JotForm/MailStyler tests, authenticated sending-domain setup, inbox rendering and deliverability assessment.
- A signed installer and clean-PC/manual assistive-technology testing.
- Performance and endurance evidence for large real audiences.

Do not call this a complete commercial SendBlaster replacement. The code now covers the main workflows, with the differences above stated explicitly.
