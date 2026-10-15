# Validation — 0.2 development

15 October 2026. Linux x64; .NET SDK 10.0.100. C# build: zero warnings/errors. 72 functional/integration checks pass against loopback APIs and temporary SQLite databases. No real email was sent.

The original 38 checks still pass. Added checks cover credential encryption and wrong-key rejection, HTML sanitization, block URL validation, UTF-8 iCalendar folding, private-address link-check protection, legacy migrations with snapshot preservation, pagination overflow, encrypted credential API handling, saved audience rules, scheduling intersections/exclusions, draft revision conflicts, per-recipient attachment and calendar MIME, deferred retry timing and attempt history, prior-campaign targeting, Unknown holds and failed-only retry exclusions, sender invalidation and resume guard, unsubscribe after authentication, consent history, suppression-preserving list merge, reusable content, online backup, and exclusive workspace ownership.

SMTP outcomes are injected for the new worker checks. These are not real-provider or TLS interoperability tests. Browser and Windows build/runtime checks are still pending for this checkpoint. PostgreSQL migrations are generated; native PostgreSQL integration is still pending. Historical 0.1 checks are preserved in VALIDATION.md.
