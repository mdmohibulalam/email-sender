# Validation — Campaign Studio 0.2.2

Source commit: a3853f44c2f533029988c2628d5842b91093f0b4.
[Successful workflow 39051544705](https://github.com/mdmohibulalam/email-sender/actions/runs/39051544705), 2026-10-17. All three jobs passed. No real email was sent.

| Check | Result |
| --- | --- |
| Local .NET build | Zero warnings/errors |
| Local/Windows functional suite | 129 checks passed |
| CI functional/PostgreSQL suite | 135 checks passed |
| Chromium workflows | 53 checks passed |
| API/native NuGet and npm audits | No known vulnerable packages reported by configured sources |
| Docker | Build, startup, authentication, capture setting and non-root checks passed |
| Portable Windows | Publish, isolated startup, migrations and authenticated API passed |
| Native Windows | Publish and actual WinUI/WebView workspace startup passed |
| Installer | Compilation, installation, installed native startup and uninstall passed |

Browser regressions cover placeholders on every connection input, empty credential values, matching select/input sizing, missing-subject guidance and focus, empty-audience guidance, empty/past date rejection, explicit Start now, inline scheduling errors/retry, duplicate-submit prevention, actual immediate capture, actual future UTC scheduling, one-line sorting labels, labelled library-row alignment, dark dropdowns and narrow layouts. All previous 0.2.1 browser workflows remain covered.

Reviewed CI screenshots include audience sorting, connection examples, missing-subject review guidance and inline schedule errors. Response errors are displayed in the dialog instead of obscured by it. A timed-out response does not prove the server failed to schedule: inspect Campaigns before retrying. Server-side duplicate-schedule protection remains in place.

No schema changes or new dependencies are required. The follow-up documentation commit records these results without changing tested code. The installer artifact is built from the source commit above. Publisher signing, live-provider verification and clean consumer-PC/accessibility testing remain release work.
