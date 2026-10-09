# Changelog

## 1.1.7-local - 2026-10-09

- Refresh tracking immediately on Windows foreground-change and minimize-end events, avoiding the normal wait for the 750 ms timer when switching instances using the taskbar or Alt+Tab.
- Coalesce queued events on the UI dispatcher, retain periodic recovery, and unregister native hooks before closing the helper.
- Add native event delivery, response-time and hook-disposal regression checks.

## 1.1.6-local - 2026-10-09

- Recover a natively hidden or minimized overlay even when WPF still reports it visible and its geometry has not changed.
- Re-establish overlay stacking above its current owner without activating it when moving between instances.
- Add eight repeated native-hide/owner-change regression cycles and verify that the controller's hide decision still prevents recovery.
- Extend the opt-in live probe to repeat window switching and check that the overlay is actually above its owner, instead of checking visibility alone.

## 1.1.5-local - 2026-10-09

- Follow the foreground Codex desktop window across independent instances and highlight its actual account using the instance's CODEX_HOME, including Cockpit instances on another drive.
- Selecting an account already open in Codex focuses its window and restores it if minimized. Other instances and their tasks keep running.
- Ask users to open an unavailable account in Cockpit when using independent instances; do not replace shared authorization in this mode.
- Restrict the legacy single-instance close flow to the selected desktop process and its descendants, protecting other instances and reused process IDs.
- Close quota popups when changing the overlay owner to avoid detached detail windows.
- Add account-identity and process-isolation tests and an opt-in local multi-instance verification probe that never writes credentials.

## Local UI fix - 2026-10-08

- Hide minimized settings and profile-manager windows instead of leaving small desktop captions, reuse their instances when reopened, and restore the native window state before focusing them.
- Prevent profile-button focus from horizontally scrolling the expanded account row during a pending or active drag, keeping the main account visible when pressing the third account.
- Show color and manual status settings as a permanent section on the quota settings page, without an expander or collapse arrow.
- Keep the compact menu open when clicking Refresh all quotas, and update quota results in place. Commands that open another window retain their closing behavior.
- Keep the compact account menu anchored to the persistent overlay shell when manual or automatic quota refresh replaces account buttons.
- Close the native popup before rebuilding and reopen it after layout and owner placement are updated, preserving an open menu without detached positioning.
- Add an isolated WPF regression fixture covering consecutive updates, manual refresh, scaling and screen-edge placement. It does not access accounts or run the application controller.

## 1.0.0 - 2026-09-20

- Fixed a critical storage bug caused by the former per-account state backup implementation recursively copying `sessions`, attachments, rollout JSONL files, and local databases into every `state-*` rollback directory.
- Replaced full-state rollback copies with minimal atomic authentication transactions containing only the previous authorization, active-profile metadata, and a sanitized manifest.
- Added 10 MB per-file and 25 MB per-switch backup guards, plus retention limits of five completed backups, seven days, and 100 MB total.
- Added safe legacy `state-*` inspection and confirmed cleanup that retains the two newest copies, along with Advanced settings storage statistics and completed-backup cleanup.
- Added regression tests proving shared sessions, rollouts, attachments, settings, and SQLite data are never copied or changed by switching and rollback.
- Added profile status metadata for manual tracking of profile limits and availability.
- Added validated per-profile manual status editing in Settings; metadata remains separate from credentials.
- Added an emoji-only overlay formatter and a deterministic recommendation algorithm for future real provider data.
- Added schema migration, generalized usage windows, UTC/local-time handling, stale-cache preservation, timeout/cancellation, and notification deduplication primitives.
- Replaced the fragile interactive `/status` capture with profile-isolated `codex app-server` requests to `account/rateLimits/read`.
- Kept chats, sessions, and local Codex databases shared across account profiles; only `auth.json` changes during a switch.
- Added a one-time migration that merges legacy per-profile session files and thread database rows into the shared Codex state.
- Wait for every Codex process to exit after forced termination before replacing authorization, preventing restart races and locked database errors.
- Fixed build, test, and publish scripts to prefer the repository-local .NET SDK when the system installation contains only a runtime.
- Added provider support testing, manual refresh, last successful refresh, last safe error, CLI version display, and full master-toggle behavior for automatic limit indicators.
- Added privacy regression coverage for ANSI/terminal parsing, low-window selection, malformed output, stale cache, unavailable providers, timeouts, and redaction of email/session identifiers.
- Fixed severe overlay overhead by reusing the attached window, skipping unchanged WPF placement work, containing tracking errors, and avoiding duplicate Codex launches.
- Expanded English/Russian localization and regression coverage for settings, thresholds, recommendations, stale data, and unavailable-provider behavior.
- Removed the obsolete interactive `/status` terminal compatibility path so usage data now has one deterministic, structured app-server implementation.
- Updated the bundled SQLite native runtime to a non-vulnerable release for safe shared-state migration.

## Previous Releases

Added tray lifecycle, single-instance activation, startup registration, compact/expanded/auto display modes, global hotkeys, settings, profile management, notifications, docs, CI, packaging scripts, and repository safety checks.
