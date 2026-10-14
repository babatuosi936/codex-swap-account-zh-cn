# Changelog

## 1.3.13-local - 2026-10-14

- Dragging the bar below the title row restores the full expanded account row and resets horizontal scrolling to the beginning. Auto mode also leaves its compact button and displays all accounts there.
- Returning to the top fits the protected title-row space again. Width changes only on a region transition; movement within either region stays at a fixed width. Keep window boundaries and retain scrolling if the full content exceeds the owner itself.
- Pass four region-transition scenarios, including three overlay scales, scrolled content, repeated gestures and Auto mode; preserve six fixed-width title-row drag checks. Public release remains 1.3.1.

## 1.3.12-local - 2026-10-14

- A wider undocked account bar can be dragged back to the protected title row. If its existing width cannot fit there, fit the row once on entry and retain that new width for subsequent movement and release.
- Keep horizontal dragging at a fixed width, X=340 menu protection, caption-button protection, fixed endpoints and wheel scrolling. Do not expand the bar merely because it is dragged down again.
- Pass three return-to-top scenarios at scales 0.8/1/1.4, including a wider starting bar, release/save and repeated down/back gestures; retain the six fixed-width drag scenarios. Public release remains 1.3.1.

## 1.3.11-local - 2026-10-14

- Dragging Overview or other whole-bar surfaces moves the overlay at its existing width. Clamp the position at menu/caption boundaries instead of enlarging or shrinking the row; reject movement into a region that cannot fit the full dragged width.
- Retain that width after release, settings save and timestamp-only quota refresh. Resume automatic sizing when owner dimensions, measured account content, display mode or overlay scale change. Floating-ball dragging retains its own size.
- Validate fixed-width movement and release at scales 0.8/1/1.4 in Expanded and Auto modes, including moving between the title row and ordinary positions, and owner-resize adaptation. Public release remains 1.3.1.

## 1.3.10-local - 2026-10-14

- Keep the top account bar at or to the right of X=340 relative to the Codex client. Shrink its width between that menu boundary and the caption buttons instead of shifting left or forcing it down. The boundary does not scale with overlay content.
- Keep Overview and the final menu button fixed at the two ends. Scroll only the middle account viewport horizontally with the normal mouse wheel, using pixel offsets and no visible scrollbar. Preserve its scroll position through quota refresh.
- Retain content-sized width on wider windows, independent floating-ball placement, and the saved preset/offsets. Temporarily hide when even the fixed controls cannot fit safely.
- Add seven core boundary cases and six native UI scenarios covering 0.8/1/1.4 scales, menu/custom placement, wheel direction, fixed endpoints and refresh persistence. Update caption protection checks to require shrinking in place. Public release remains 1.3.1.

## 1.3.9-local - 2026-10-14

- Closing the last window of a verified account desktop now stops that desktop and its remembered Codex workers. Observe all open account windows, allow normal exit first, then terminate residual identity-checked workers after the configured graceful timeout.
- Retain minimized windows, windows on other virtual desktops, and other windows in the same desktop process. Cancel cleanup if a window reopens. Keep other accounts, VS Code, unrelated CLI workers, and the overlay alive.
- Match process ID plus start time, remember workers before their parent exits, and skip cleanup if window/process identity cannot be verified. This applies while the account manager is running; it does not sweep previously unseen background processes or cancel server-side work.
- Validation: 269 core tests, six native simulated window-lifetime checks, and the existing independent-account action fixture passed. No real account windows were closed during validation. The public release remains 1.3.1.

## 1.3.8-local - 2026-10-14

- Size the expanded account bar to its measured content instead of reserving a fixed 620/560-DIP minimum. Removing accounts or shortening quota summaries immediately shrinks the bar, while adding accounts or receiving longer summaries expands it.
- Keep the menu button next to the final account, retaining scaled content measurement, narrow-window scrolling and caption-button protection.
- Add six content-width scenarios covering 0/1/2/3 accounts, quota visibility, scales 0.8/1/1.4, and shorter/longer quota refreshes in the same window. The public release remains 1.3.1.

## 1.3.7-local - 2026-10-14

- Keep the account bar and floating ball clear of the owner's minimize, maximize and close buttons. Read native caption bounds where available, with a protected title-row area for custom desktop frames.
- When the expanded After menu row does not fit, move it below the caption buttons temporarily. Auto mode measures the space between the menu anchor and caption controls before selecting a compact row. Restore the saved menu position when the owner grows.
- Apply the same protection during dragging, quota refresh, window resizing and floating-ball restore without overwriting saved offsets or the position preset. Hide the surface if a tiny owner has no safe placement.
- Validation: 259 core tests and 309 synthetic WPF checks passed, including six caption protection scenarios at scales 0.8, 1 and 1.4. The public release remains 1.3.1.

## 1.3.1 - 2026-10-13

This public release includes the functionality and fixes tested in local builds through 1.3.6-local. Its public version is 1.3.1.

- Open accounts in separate Codex windows with separate sign-ins and desktop caches while sharing existing local history. Cockpit Tools is no longer required for multi-account windows.
- Focus already-open account windows directly, including the original desktop, in both directions. Accounts without an open window offer opening a new independent window or switching the current account; opening an independent window is the default.
- Display reported Plus/Pro/Free plan badges and all returned quota periods, including Free accounts' 30-day quotas. Preserve the actual percentage and reset time.
- Minimize the overlay to a draggable floating ball and restore its previous full-size position. Enable Windows startup for new settings, and preserve dragged placement across smaller Codex windows.
- Align Advanced import/export/reset with the current settings service; position defaults/reset use After menu at (340, 0). Place account action dialogs below the overlay.
- Preserve account/history isolation checks and saved-account identity protection. Shared storage does not guarantee immediate message refresh between desktop windows; continue each conversation in one window.
- Release validation: 249 core tests, 303 synthetic WPF checks and 22 isolated desktop launch-policy checks passed.
- Documentation update: rebuild Chinese and English homepages around current workflows, regenerate synthetic screenshots from current controls, and move developer instructions to `docs/DEVELOPMENT.md`.

## 1.3.6-local - 2026-10-13

- Clicking an account now goes directly to its already-open original desktop as well as an independent desktop. Fix asymmetric navigation where 475 to 181 showed an action dialog while 181 to 475 focused immediately.
- Reuse the same verified account-window lookup for account clicks, opening a window and current-account switching. Keep existing history/environment validation, minimized-window restoration and the choice for accounts without an open window.
- Verify both selection directions, reversed window order, minimized and unknown accounts; pass independent-account UI checks. A read-only local probe confirmed both current desktops resolve to saved accounts and shared history, with one original and one independent desktop.

## 1.3.5-local - 2026-10-13

- Show every returned quota window in account details and overview cards. Free accounts with a 30-day limit no longer incorrectly show quota unavailable.
- Label the actual 30-day period consistently in headers, details and settings; retain its returned percentage and reset time without inventing five-hour or weekly limits.
- Pass 37 quota/plan core cases using in-process xUnit assertions, 32 tooltip UI checks and 24 overview checks, including Free 30-day and other custom periods.

## 1.3.4-local - 2026-10-13

- Restore the After menu position preset on position reset and for new settings, retaining offsets (340, 0). Preserve independent scale and animation preferences.
- Pass 32 relevant core tests and 23 Advanced UI checks covering the reset button, selected preset label and persistence.

## 1.3.3-local - 2026-10-13

- Anchor the account action dialog below the full account bar, horizontally centered with an 8-DIP gap, instead of centering over its small owner window.
- Use physical screen rectangles and dialog DPI, recheck placement after loading, clamp horizontal screen edges, and prefer above the bar when there is insufficient space below.
- Retain the default separate-window action, direct focus of an existing independent account window, and cancellation behavior.
- Unify initial, reset and invalid-value position defaults at Custom (340, 0), with initial scale 1. Position reset retains separate scale/animation preferences.
- Show account plan badges after names in the header, compact menu and overview. Read Plus/Pro/Free and other supported plans from the current quota response's planType; never infer Free from missing data.
- Pass 246 core tests and 284 synthetic WPF checks, including the production dialog Show path, plan-only refresh, multiple scales and long account names.

## 1.3.2-local - 2026-10-13

- Audit Advanced controls against the current multi-account implementation. Runtime source contains no original-author repository, updater or download endpoints.
- Route settings import/export through the configured settings service. Validate imports and apply them immediately; export effective settings instead of copying a file from a hardcoded application directory.
- Retain the settings object shared by all windows on reset/import and use the normal application path for language, theme, shortcuts, startup, visibility and quota thresholds. Clear obsolete shortcut drafts only for complete settings replacements.
- Scope shutdown controls to single-window current-account switching. Distinguish sign-in rollback/legacy state backups from old application-build archives. Mark bundled upstream Russian documentation as historical.
- Pass 213 core tests and 224 synthetic WPF checks. Actual system startup and global-shortcut registration paths were source-reviewed; no user account reset, import or backup cleanup was performed.

## 1.3.1-local - 2026-10-13

- When selecting an account, focus a verified independent account window immediately if it is already open. Otherwise, show a choice between opening a new separate window and switching the current account. A separate window is the default choice.
- Rename the General setting to **Prefer a separate account window**. It now controls the default choice while retaining both actions.

## 1.3.0-local - 2026-10-13

- Independently implement separate account windows with shared existing local history, enabled by default. Focus a compatible existing window or launch the selected saved account without replacing default authorization or restarting the default window. No Cockpit Tools dependency or source copied from Cockpit Tools is included.
- Keep `CODEX_HOME` account-specific, set child-process `CODEX_SQLITE_HOME` to the canonical default directory, link sessions, archived sessions, attachments, and native thread-writer locks, and isolate Electron user data per account.
- Inspect all locations before preparation. Preserve and refuse nonempty private history/attachment directories, links to other targets, escaped or linked account roots, unknown account identities, and incompatible existing windows. Do not copy, import, or merge histories or account credentials in this flow.
- Keep existing-window configuration checks read-only, and block profile-directory renaming or account removal while a verified process is using it.
- Add **Open accounts in separate windows** to General settings. Disabling it retains the explicit legacy single-instance authorization-switch behavior.
- Validate preparation with focused tests and native CLI 0.162.0-alpha.17.2 using two synthetic account homes sharing six SQLite databases. A second resume is rejected while the first native process owns the shared writer lock and succeeds after it exits.
- Pass 192 core tests, 22 launcher-policy checks, and 205 WPF checks. On Windows Store Codex 26.1007.2314.0, launch two simultaneously visible signed-out official windows using synthetic homes, verify distinct processes/homes/caches, all three environment paths and package identity, and confirm existing window processes stay alive. No real credentials, history, or model inference were used.
- Real signed-in identity, attachment display, cross-window refresh and desktop ownership handoff remain pending. Synthetic window cleanup required stopping only the two verified test processes after closing their windows left background processes; graceful complete desktop exit is not proven. Automatic follower behavior and repair of old VS Code writer-lock directories are not claimed.
- Deliver locally only. Public GitHub download links still point to public v1.2.0; 1.3.0-local has not been published remotely.

## 1.2.4-local - 2026-10-11

- Preserve the requested custom overlay position when a smaller Codex window temporarily clamps the bar to its edges. Resizing/restoring or switching back from a smaller Cockpit Tools instance restores the chosen position instead of retaining the clamped top/left coordinates.
- Add regressions for repeated resizing, layout refresh while clamped, and switching between differently sized owners. Floating-ball and normal positions remain independent.

## 1.2.3-local - 2026-10-11

- Enable Windows sign-in startup by default for new settings. Existing explicit settings remain respected, and users can still disable startup in General settings.

## 1.2.2-local - 2026-10-10

- Add Minimize between Add Profile and Settings. Keep a visible 32-DIP floating ball that can be dragged and clicked to restore. Ball placement is separate from normal overlay placement, so restoration returns to the original account-bar position; quota and settings updates retain the minimized state.

## 1.2.1-local - 2026-10-10

- Verify account identity before writing shared authorization back into the previous saved profile. A stale active-profile marker after an external login or instance switch must not overwrite another account's credentials. Failed-switch rollback restores shared authorization only, leaving saved profiles intact.
- Add matching, mismatched and unknown identity regression coverage, including failed target replacement.

## 1.2.0 - 2026-10-10

- Support independent Codex desktop instances launched by Cockpit Tools, including instance directories on another drive. Follow the foreground window and highlight its actual account; focus an already-open account without restarting other instances. Unopened accounts must first be launched in Cockpit Tools. Chat history synchronization remains the responsibility of the instance manager.
- Restore the overlay reliably across taskbar and Alt+Tab switches, and respond to native foreground events to reduce switching delay.
- Add an all-account overview when two or more accounts exist, centered below the overlay, with at most three cards per row. Support hover preview and click-to-toggle opening and closing.
- Show colored remaining-quota progress bars, reset countdowns, credit balances, available reset opportunities and expiry times. Display unknown values honestly; reset opportunities are queried only, never redeemed.
- Move individual and all-account refresh actions into quota panels and preserve open panels while updating results.
- Long-press account tabs to reorder them with browser-style sliding animation and saved order; drag other header regions to move the whole overlay.
- Move account-overlay visibility into General settings, align Reset Hotkeys and Save on one row, and improve auxiliary-window restoration.
- Verify account identity before using shared credentials for quota queries, preventing another instance's quotas from appearing under the wrong account. Explicit UTF-8 protocol encoding fixes Chinese account paths causing refresh timeouts and stale data.

## 1.1.20-local - 2026-10-09

- Read and write app-server quota messages explicitly as UTF-8. Prevent Windows legacy console encodings from corrupting initialization JSON for Chinese account paths and leaving quota permanently stale after refresh timeouts.

## 1.1.19-local - 2026-10-09

- Verify account identity before querying shared credentials for the active profile. Query saved credentials when identities differ or cannot be verified, preventing quota from another instance appearing under the wrong account.

## 1.1.18-local - 2026-10-09

- Place Reset Hotkeys and Save on the same row, aligned to the left and right of the settings card.

## 1.1.17-local - 2026-10-09

- Move account-overlay visibility from floating and tray menus to the General settings toggle, with immediate application and persisted state.
- Reveal the overlay when the settings toggle is enabled after a previous manual hide.

## 1.1.16-local - 2026-10-09

- Animate account sorting like browser tabs: the grabbed card follows the pointer above its neighbors, adjacent cards slide into preview slots, and cards settle smoothly after release or cancellation.
- Use stable layout positions for insertion hit testing to avoid animation-induced oscillation; retain drag-region separation and the animation preference.

## 1.1.15-local - 2026-10-09

- Reserve account tabs exclusively for account sorting; pressing or moving on a tab never starts overlay positioning, including during the hold delay.
- Retain whole-overlay dragging on Overview, collapse/menu controls, and the header background.
- Suppress accidental account switches when a tab is moved and released before the hold delay completes.

## 1.1.14-local - 2026-10-09

- Hold an account tab for 450 ms, then drag horizontally to reorder saved accounts. Show an insertion cue and scroll at the account viewport edges.
- Save through the existing profile-order store, suppress account switching after reordering, and retain ordinary clicks and quick overlay dragging.
- Cancel unfinished sorting when the overlay hides, changes owner, or closes; defer quota layout updates during sorting.

## 1.1.13-local - 2026-10-09

- Move quota refresh from the floating menus into the quota panels: refresh one account in its detail panel, or all saved accounts from Overview.
- Show refreshing state and block repeat submissions, update open cards in place, and defer the account-bar rebuild until the panel closes.
- Manual refresh of all accounts does not depend on automatic-polling preferences.

## 1.1.12-local - 2026-10-09

- Toggle the clicked Overview panel closed on a second click, including when popup outside-click capture dismisses it before the button Click event.

## 1.1.11-local - 2026-10-09

- Clicking Overview opens the account panel immediately and keeps it visible after the pointer leaves. Clicking outside dismisses it.
- Retain hover previews and close the panel when the floating window hides or changes owner.

## 1.1.10-local - 2026-10-09

- Arrange overview account cards horizontally with vertical details inside each card, wrapping into further rows on smaller monitors or with more accounts.
- Center the overview beneath the entire floating account bar, retaining hover traversal and bounded vertical scrolling.
- Size the panel using the anchor monitor's working area and DPI rather than the primary monitor.

## 1.1.9-local - 2026-10-09

- Add colored remaining-quota progress tracks to single-account details and the all-account overview, keeping reset countdowns aligned right and omitting absent quota windows.
- Parse decimal credit balance and unlimited credits from the existing app-server response.
- Query available reset credits and nearest expiry using the same read-only ChatGPT endpoint used by Codex Accounts Manager. No reset/redemption operation is performed.
- Display unavailable supplementary values as unknown, preserve zero as a real value, and keep quota results usable when the optional reset-credit query fails.

## 1.1.8-local - 2026-10-09

- Add a leading Overview entry to expanded and compact layouts only when at least two saved accounts exist.
- Hover to view every account's quota details, reset countdowns and last update, with current-account highlighting and scrolling for larger lists.
- Reuse the existing quota card format, omit unavailable five-hour windows, and preserve the mouse transition into the overview panel.

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

