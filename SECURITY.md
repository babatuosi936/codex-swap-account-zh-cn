# Security Policy

Codex Swap Account is local-only and must never upload, display, parse, or log `auth.json`.

## Credential Rules

- Never commit `auth.json`, `.codex-profiles`, backups, logs, or local app data.
- Tests must use temporary directories and dummy `auth.json` contents only.
- Profile metadata in `%LOCALAPPDATA%\CodexProfileOverlay\profiles.json` may contain display names, order, initials, and local accent choices only.
- Manual status metadata and cached normalized usage snapshots belong only in `%LOCALAPPDATA%\CodexProfileOverlay\profile-status.json`, never beside profile credentials.
- The automatic usage provider may use only a profile-isolated local `codex app-server` process and the structured `account/rateLimits/read` protocol response.
- Usage providers must discard raw terminal output immediately after parsing recognized limit rows. Undocumented endpoints, browser scraping/cookies, raw responses, and credential parsing are prohibited.
- Cached usage snapshots may contain only limit labels, remaining percentages, reset timestamps, capture timestamp, Codex CLI version, and the sanitized source identifier `codex-cli-status`.
- Provider logs may contain only an internal profile identifier, timestamps, capability/result category, sanitized error category, and snapshot age. They must never contain account email, session ID, model, permissions, directory, project path, or auth contents.
- Logs must stay token-safe and must not include raw exception stacks for normal user flows.
- Normal switch backups may contain only the previous authorization, active-profile metadata, and a hash/timestamp transaction manifest. They must never contain sessions, rollout files, attachments, memories, databases, logs, caches, settings, browser state, or project data.
- Backup creation rejects an individual file over 10 MB and a switch backup over 25 MB before changing account state. Completed retention is limited to five backups, seven days, and 100 MB total; active transactions are never deleted.
- Legacy cleanup is restricted to recognized `state-*` children of `%LOCALAPPDATA%\CodexProfileOverlay\backups`, requires explicit confirmation, and retains the two newest. It must never target `%USERPROFILE%\.codex` or `%USERPROFILE%\.codex-profiles`.
- Run `.\verify-repository-safety.ps1` before staging or publishing changes.

## Reporting

Please report credential exposure privately. Do not attach real `auth.json` files, screenshots containing tokens, browser cookies, or account credentials.
