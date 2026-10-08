# Architecture

Codex Swap Account has two assemblies:

- `CodexProfileOverlay.Core`: token-safe services and models that can be unit-tested without WPF.
- `CodexProfileOverlay`: WPF shell, tray lifecycle, window attachment, hotkeys, settings/profile windows, and Codex process launching.

The switcher keeps `%USERPROFILE%\.codex` as one shared Codex state directory for every profile. During a switch it saves the current `auth.json` into `%USERPROFILE%\.codex-profiles\<profile>`, atomically installs the selected profile's `auth.json`, and leaves chats, sessions, projects, and local databases in place. A one-time migration merges legacy per-profile session files and thread rows back into the shared store. Non-secret UI metadata lives under `%LOCALAPPDATA%\CodexProfileOverlay`.

## Authentication transaction and backups

`AuthSwitchService` validates the current and target authorization files before mutation. `BackupMaintenanceService` then creates an active transaction directory containing exactly:

- `previous-auth.json`;
- `previous-active-profile.txt`;
- `manifest.json` with state, UTC timestamps, byte size, and SHA-256 hashes.

No recursive workspace-copy API exists in the switch path. The service rejects any input or backup file over 10 MB and rejects a transaction estimated or measured above 25 MB. After the rollback exists, the current authorization is atomically saved to its profile, the target authorization atomically replaces the shared file, and `ActiveProfileStore` atomically updates metadata. A failed replacement or Codex relaunch restores only authorization and active-profile metadata; shared chats, sessions, settings, databases, projects, and attachments are never restored or replaced.

Completed backups use the `completed-*` namespace. Cleanup runs at startup and after each switch: at most five completed backups, seven-day maximum age, and 100 MB combined storage. A manifest marked `active` is never removed. Only recognized non-active `txn-*` directories older than 24 hours qualify as abandoned temporary transactions.

Legacy `state-*` directories came from the removed per-account workspace implementation. The Advanced settings page can calculate their count, size, time range, and reclaimable size without reading credential contents. Cleanup requires confirmation, retains the two newest legacy directories, ignores unknown entries, and can target only children of `%LOCALAPPDATA%\CodexProfileOverlay\backups`; `.codex` and `.codex-profiles` are outside its target set.

Normal Codex launches do not set `CODEX_HOME`. The add-profile login flow sets `CODEX_HOME` only for that isolated `codex login` process.

## Profile Status and Usage Limits

The status feature allows users to track which Codex profiles have usable limits:

- `ProfileStatusMetadata`: Stored per-profile user metadata (manual emoji, labels, notes, reset times). Saved to `profile-status.json`, never mixed with credentials.
- `UsageSnapshot`: Automatic usage data from a provider. `UsageLimitWindow` preserves provider window names, optional durations, remaining percentages, and reset timestamps instead of assuming fixed 5-hour/7-day windows.
- `IUsageProvider`: Abstraction for retrieving usage data. Returns `Supported` or `Unavailable` capability. `CodexCliStatusUsageProvider` reads normalized account limits from a profile-isolated Codex app-server process.
- `ProfileStatusService`: Validates manual metadata, serializes provider calls with cancellation/timeouts, preserves successful cache entries on failure, and calculates recommendations.
- `UsageIntelligence` and `ProfileIndicatorFormatter`: Pure, tested capacity and emoji formatting rules used by the WPF shell.

The status document is schema-versioned and normalized on load. Timestamps are persisted as UTC and formatted in local time. Legacy short/long snapshot fields remain readable for backward compatibility. Manual metadata and automatic snapshots are independent, so refreshes cannot overwrite user notes.

### Automatic provider

The supported Codex CLI app-server schema exposes `account/rateLimits/read`. The provider starts `codex app-server --stdio` with `CODEX_HOME` set to one saved profile, performs protocol initialization, requests the structured limit snapshot, and terminates the child process. This avoids terminal emulation and remains independent of `/status` screen layout.

The runtime stores only normalized limit windows, remaining percentages, reset timestamps, and capture metadata. Raw app-server messages and credentials are never persisted. Refresh calls are serialized, cancellable, timeout-bound, and isolated per profile.

Limit indicators appear as small emoji next to profile names in the overlay:
- ⭐: Recommended profile (only when ≥2 profiles have fresh comparable data)
- 🟢: High remaining capacity (≥ green threshold, default 60%)
- 🟡: Medium remaining capacity (≥ yellow threshold, default 25%)
- 🔴: Low or exhausted (below yellow threshold)
- No emoji: Unknown, unavailable, or stale data

The recommendation algorithm only considers fresh, known, non-exhausted snapshots. It maximizes the lowest remaining percentage across all available windows, then the average remaining percentage, then the nearest reset. It returns no recommendation with fewer than two candidates or when the leading candidates are indistinguishable.

### Overlay performance

The controller reuses a validated Codex window handle and performs a full top-level window scan only when attachment is lost. Placement is recalculated on a background-priority UI tick, but WPF layout and `SetWindowPos` run only after geometry actually changes. Launch-on-start first checks for an existing Codex window and never starts a duplicate instance. Tick failures are contained and logged rather than terminating the tray process.
