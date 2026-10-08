# Release Checklist

- Run `dotnet format CodexProfileOverlay.sln --verify-no-changes`.
- Run `dotnet restore CodexProfileOverlay.sln`.
- Build and test Release with `.\build.ps1`.
- Run `.\verify-repository-safety.ps1`.
- Publish with `.\publish.ps1`.
- Confirm the EXE, portable ZIP, and `SHA256SUMS.txt` exist under `artifacts`.
- Verify the EXE and ZIP against `SHA256SUMS.txt`.
- Launch the published executable.
- Verify tray menu, compact mode, expanded mode, settings, and profile refresh.
- Do not upload `auth.json`, backups, logs, `.codex-profiles`, or real local fixtures.
