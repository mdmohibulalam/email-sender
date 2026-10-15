# Validation — Campaign Studio 0.1.0

Implementation and verification performed on 14 October 2026, Asia/Dhaka. Test environment: Linux x64; .NET SDK 10.0.100; Chromium headless shell 151; temporary SQLite database. No real email was sent.

| Check | Result | Meaning / boundary |
| --- | --- | --- |
| C# API/Core/Test build | Passed, zero warnings/errors | Compiles on .NET 10 in the test environment |
| Functional/integration suite | 38 checks passed | Actual loopback API, SQLite, MIME generation, and worker state changes |
| Browser workflow | 10 checks passed | Actual import, editor, preview, scheduling, report, download, help, and narrow-width rendering |
| JavaScript syntax | Passed | `node --check` |
| NuGet transitive vulnerability audit | No vulnerable packages reported by the available NuGet source | An audit result at build time, not a security certification |
| Windows x64 portable publish | Passed | Self-contained PE x64 binary, bundling .NET/ASP.NET Core runtime 10.0.13 |
| Native WinUI build on Linux | Blocked | Windows XAML compiler could not run; native shell needs a Windows build |
| Native WinUI/portable launcher on Windows | Not performed | No Windows desktop is available in this environment |
| PostgreSQL/Docker cloud deployment | Not performed | Configuration and adapters are included, but no hosted instance exists |
| Real SMTP/TLS/provider verification | Not performed | Capture tests send no mail; a controlled provider test is still required |
| Installer/signing/update verification | Not performed | No signed installer, licensing, or updater is included |

## Functional checks

CSV BOM/quotes/multiline parsing; malformed CSV rejection; formula-safe export; email normalization; syntax/header-injection checks; safe HTML/link rendering; missing-name fallback; API authentication; permission/source requirement; import classification; invalid mapping; frozen audience; schedule idempotence; immutable scheduled message; pause; token tampering; public confirmation; GET does not unsubscribe; one-click POST; suppression survives reimport; queued suppression; actual capture files; campaign completion; personalized multipart MIME and unsubscribe headers; honest delivery reporting; draft duplication; expired attempt becomes Unknown; review state; future schedule; cancellation; live-send guard; empty-audience guard; transaction rollback; contact exports; persistence across contexts; contact/suppression persistence after host restart; draft persistence after host restart; Unknown recipient persistence after host restart.

## Browser checks

Launch key removed from URL; small two-button import success dialog; duplicate/invalid handling visible in the audience; safe personalized email preview; mobile canvas width; review/schedule/capture workflow; actual .eml download; guide content; narrow overview without document overflow; zero JavaScript runtime errors during the workflow.

Internal desktop/editor/mobile screenshots were visually inspected. They are not included in the deliverable. The initial narrow-layout overflow was fixed and the browser workflow passed afterward.

The test executable is included in source. Browser verification used a temporary Playwright harness with example contacts. Do not interpret these checks as complete competitor parity, commercial readiness, Windows verification, or confirmed cloud/SMTP operation.
