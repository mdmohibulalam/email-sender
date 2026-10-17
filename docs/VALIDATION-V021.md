# Validation — 0.2.1 navigation and preferences
Local .NET build passed with zero warnings/errors. All 129 local functional checks passed, including 14 new checks covering authenticated appearance settings, persistence across server restart on a new port, corrupt-file fallback and bounded/trusted update checks. No real email was sent.

The expanded Chromium workflow covers top-level Connections, footer version, icon menus, saved palettes, Dark/System themes, keyboard focus/Escape, guide search, account status, disconnect cancellation, credential-free diagnostics, manual release links, retryable update failures and narrow-window dropdown boundaries. CI additionally runs PostgreSQL, Docker and native/portable Windows installation checks. Final results are recorded after the workflow completes.

Appearance settings are stored per workspace, not per customer account. License/account shows actual preview access; it does not implement a licensing service. Update checks use published stable GitHub releases and do not install updates automatically.
