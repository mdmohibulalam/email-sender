# Validation — 0.2 development

15 October 2026. Linux x64; .NET SDK 10.0.100. C# build: zero warnings/errors. 94 functional/integration checks pass against loopback APIs and temporary SQLite databases. No real email was sent.

The original 38 checks still pass. Added checks cover credential encryption and wrong-key rejection, HTML sanitization, block URL validation, UTF-8 iCalendar folding, private-address link-check protection, legacy migrations with snapshot preservation, pagination overflow, encrypted credential API handling, saved audience rules, scheduling intersections/exclusions, draft revision conflicts, per-recipient attachment and calendar MIME, deferred retry timing and attempt history, prior-campaign targeting, Unknown holds and failed-only retry exclusions, sender invalidation and resume guard, unsubscribe after authentication, consent history, suppression-preserving list merge, reusable content, online backup, and exclusive workspace ownership.

The worker uses injected outcomes for deterministic retry/race checks. A real loopback TLS SMTP sandbox additionally checks certificate rejection, authentication without dispatch, SMTP result classification, and the final pre-DATA gate. It never relays messages. Double-opt-in and POP3 receipt processing use local transport fixtures. JotForm answer mapping uses synthetic JSON; no live account was accessed.

13 browser checks pass for connection forms, hidden passwords, visual audience rules, block edits, real-contact preview, persistence, autosave, schedule/capture/download, attempts, reusable content, narrow layout and JavaScript runtime errors. Browser harness is checked in under tests/browser.

Windows CI exposed and prompted fixes for backup-file sharing and a mixed-clock test. Native Windows build/runtime checks remain pending for the corrected source. Native PostgreSQL and browser checks are now included in GitHub Actions; results must be read from the actual run. Historical 0.1 checks are preserved in VALIDATION.md.
