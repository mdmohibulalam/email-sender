# Validation — Campaign Studio 0.2.1
Source: 1a2b45857126067710a8bc83bb634b393feef993 on feature/settings-help-navigation.
Workflow: https://github.com/mdmohibulalam/email-sender/actions/runs/39048579729
Date: 2026-10-17. No real email was sent.

- Local .NET build: zero warnings/errors; 129 functional checks passed.
- CI Linux: 135 functional/PostgreSQL checks passed.
- CI Chromium: 36 browser checks passed.
- API NuGet and npm audits: no known vulnerable dependencies reported by configured sources.
- Docker: build, startup, authentication, capture-only setting and non-root checks passed.
- UI screenshots reviewed: navigation/Settings dropdown, Light appearance palettes and Dark theme.

New functional coverage includes authenticated appearance storage, persistence across a new server port, invalid-input rejection, corrupt-file fallback, credential isolation, trusted release URLs, unpublished-release behavior, network errors, prerelease rejection, response-size bounds and stable-version comparison.

Browser coverage includes top-level Connections, footer version, icon menus, saved palettes, Dark/System themes, focus/arrow/Escape controls, shortcut navigation, searchable guide, real account status, cancelled disconnect, credential-free copied diagnostics, manual release links, retryable update failures and 360px dropdown boundaries. The existing campaign/audience/designer workflows also pass.

Windows CI: 129 functional checks passed; self-contained native and portable packages published and launched successfully. The Inno Setup installer compiled, installed, launched the installed authenticated native workspace and uninstalled successfully. Native dependency audit reported no known vulnerable packages. All three workflow jobs passed. Local browser launch is restricted by the execution environment; the successful Chromium checks and screenshots were produced by GitHub CI.

Appearance is workspace-wide and saved in appearance.json, independent of changing native loopback ports. It does not change email designs. License/account shows preview status and administrative-key access, not a new licensing/account service. Release checks do not install updates automatically. Publisher signing, external-provider tests and clean consumer-PC/accessibility verification remain release work.

The follow-up commit records these results and removes a trailing blank line; tested application behavior is unchanged. The installer artifact was built from the source commit above.
