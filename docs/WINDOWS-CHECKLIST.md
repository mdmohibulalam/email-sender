# Windows release checks — 0.2
## Automated workflow
Build and verify runs the functional suite on Windows, publishes the self-contained native WinUI shell and portable API, launches the portable API on an isolated ephemeral port, checks authenticated settings, launches the native shell in smoke-test mode, and verifies that the authenticated WebView renders the workspace. Only after these checks does it compile the unsigned Inno Setup installer, install it into a temporary location, launch the installed native workspace, and uninstall it.

A successful runner build is evidence for that Windows runner, not certification on every Windows PC. WebView2 Runtime is a prerequisite; an offline runtime is not bundled. The installer currently does not install that prerequisite automatically.

## Manual checks still required
- Install/uninstall as a standard user on a clean Windows 10 and Windows 11 PC; retain existing workspace data.
- Test missing WebView2 Runtime and provide clear installation guidance.
- Verify publisher signing, installer reputation and distribution integrity.
- Exercise keyboard navigation, focus, screen reader, high-contrast mode and 125–200% display scaling.
- Test startup minimized, restart, sleep/resume and missed schedules.
- Test DPAPI secrets and documented restore under the same Windows account.
- Connect an authorized SMTP/POP3 provider and test confirmation/unsubscribe from an external device.
- Test the actual MailStyler executable/export workflow when configured.
- Validate received MIME in Outlook, Gmail and Apple Mail.

Native and portable previews use separate data folders. The old 0.1 ZIP does not prove the current native build works.
