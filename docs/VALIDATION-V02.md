# Validation — reconstructed 0.2 source
Date: 2026-10-15. No real email was sent.

## Verified automated evidence
[Workflow 38697365647](https://github.com/mdmohibulalam/email-sender/actions/runs/38697365647), commit 0cd07759a42336f7e387a511320537ef927502b6:
- Linux functional suite: **121 checks passed**, including six real PostgreSQL integration checks.
- Browser suite: **17 checks passed**, including editor/reload, source/blocks, image adjustment, scheduling capture, report attempts, saved content, signup forms, mobile overflow and browser error checks.
- API dependency audit: no known vulnerable packages reported by configured NuGet sources at check time. npm audit: zero reported vulnerabilities.
- Docker: image built and ran as a non-root user; health, bearer authentication, writable data directory and disabled live-send setting verified.
- Windows functional suite: **115 checks passed** (same functional suite excluding PostgreSQL integration).
- Windows self-contained API: published and launched; PID-matched ready handshake, migrations and authenticated settings passed.
- Native WinUI initially compiled but failed startup. [Workflow 38697828967](https://github.com/mdmohibulalam/email-sender/actions/runs/38697828967), commit dea2322d724748dc783eff2abfc1d8b2b0829719, passes the native authenticated workspace startup check after adding WinUI resources and packaging support. A later browser rerun exposed a Cancel button submitting its form; that behavior is corrected with a regression check in the subsequent source.

The six PostgreSQL checks cover contact import deduplication, recipient capture, attempt recording, migration/model consistency, empty engagement aggregation and persistence across host restart. The provider is a real PostgreSQL 17 service, not an emulator.

## Checks added for the recovered feature set
- PNG checksums and signed image access; MIME CID embedding.
- Concurrent claims with a two-connection sender limit, no premature campaign completion and one recorded attempt per accepted recipient.
- Signed open-image/click endpoints, forged-link rejection and distinct recipient reporting.
- Binary EML attachment preservation, inline image references and outgoing text-link URLs.
- Validated multi-field sort and orphan-row foreign-key rejection.
- Restoring a verified pre-upgrade SQLite snapshot after simulated partial schema/data changes, including marker cleanup.
- Null-MX handling, ordered direct-delivery targets and disabled-by-default transport.
- Production SMTP transport against a local TLS sandbox: untrusted certificates, verification without message submission, accepted-before-QUIT-disconnect, transient rejection, explicit missing-recipient rejection, policy rejection, authentication failure, uncertain disconnect and final pre-DATA suppression gate.

The SMTP sandbox accepts only local test traffic and never relays. Injected transport tests verify dispatch logic, not external deliverability.

## Interpretation
These are executable assertions, not a claim that every user workflow or every email client has been tested. SQLite's FK table rebuild migration emits a non-transactional-operation warning; verified snapshot/marker recovery is implemented and exercised. The warning is not evidence of an atomic migration.

No external-provider SMTP/POP3/JotForm or actual MailStyler test has occurred. No public cloud service, sender-domain authentication, inbox rendering, 100-connection load test, signed publisher installer or clean Windows 10/11 manual verification is claimed. Feature boundaries are in FEATURE-MATRIX.md.

## Release workflow
The read-only CI workflow now includes installer install/start/uninstall smoke testing and a native dependency audit. Refer to the latest successful run for those final checks; no manual clean-PC or publisher-signing claim is made.
