<div align="center">

# Codex Swap Account

**A Windows account switcher and usage panel for the Codex desktop app.**

See the remaining usage allowance across your accounts, switch from an overlay beside Codex, and continue using your local chats and workspaces.

**English** · [简体中文](README.md)

[Download — extract and run](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest) · [Quick start](#quick-start) · [Screenshots](#screenshots) · [Improvements over the original project](#improvements-over-the-original-project)

</div>

![Expanded overlay with sample accounts and remaining allowance](docs/images/zh-CN/expanded-mode.png)

## What it does

If you use several Codex accounts, this tool brings their names, active status, and remaining allowance into one panel beside the Codex window. Once an account has been signed in and saved, select it from the overlay or system tray to start a switch.

| Feature | What you can do |
| --- | --- |
| Account management and switching | Add, name, and manage accounts. Switch through the overlay, tray menu, or a custom hotkey. The active account is highlighted. |
| Usage panel | See available five-hour and weekly remaining percentages for multiple accounts, with automatic updates and a manual refresh-all action. |
| Shared local work | Switching changes account authorization. Your local Codex chats, sessions, and workspaces remain shared across accounts. |
| Two overlay layouts | Expanded mode shows multiple accounts directly. Compact mode uses a single account button and menu. Automatic mode chooses a layout based on the window width. |
| Position and appearance | Drag the overlay, remember its position, choose a preset beside the menu or along the bottom, and adjust scaling and light or dark themes. Windows DPI scaling is supported. |
| Mouse or hotkeys | Use the mouse for everyday actions, or configure optional hotkeys. Leave individual hotkeys unset if you prefer. Hotkey edits take effect only after clicking **Save**. |
| Languages and system tray | Use English, Simplified Chinese, Russian, or the system language. The tray provides access to accounts, settings, and overlay visibility. |
| Authorization backup and recovery | Authorization is backed up before a switch. If switching fails, the tool attempts to restore the previous authorization and reports the result. |

Switching closes and restarts Codex, so finish your current work before switching. Usage values come from server queries and may take time to update.

## Quick start

Requirements: **Windows 10/11 x64 and the Codex desktop app**. A working Codex CLI is also required for adding accounts and automatic usage queries.

1. Open the [download page](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest) and download `CodexProfileOverlay-win-x64-portable.zip`.
2. Extract it to a permanent folder and run `CodexProfileOverlay.exe`. The portable package includes its runtime, so you do not need to install .NET separately.
3. Open Codex. The account overlay appears once the tool detects its window. You can also show or hide the overlay from the system tray.
4. Choose **English** under **Settings → Language**, or use the system language setting.
5. Use **Add profile** to sign in to your accounts. Once saved, they appear in the overlay with any available usage information.
6. Select the account you want to use, confirm the switch, and wait for Codex to reopen.

If you do not need hotkeys, open **Settings → Hotkeys**, clear the individual shortcuts, and click **Save**. To change the overlay position, drag it or choose a preset under **Settings → Appearance**.

The release also includes a standalone, self-contained EXE and `SHA256SUMS.txt` for verification. Windows may show a SmartScreen prompt for an unsigned executable; check the source and published hashes before deciding whether to run it.

This repository is currently private. Access to its code, images, and release downloads requires the appropriate GitHub permissions.

## Screenshots

These images are rendered from the application's actual WPF controls. **Account names and usage values are sample data**, with no real email addresses, credentials, or chat content. The screenshots show the Simplified Chinese interface; the application also supports English.

### Expanded and compact modes

Expanded mode puts several accounts and their remaining allowance directly on screen. Compact mode fits smaller windows.

![Expanded mode](docs/images/zh-CN/expanded-mode.png)

![Compact mode](docs/images/zh-CN/compact-mode.png)

The compact account menu stays open when you refresh all accounts, so you can keep reviewing the updated values.

<img src="docs/images/zh-CN/profile-menu.png" alt="Compact account menu with sample usage values" width="560">

### Rounded action menu

Access account creation, usage refresh, account management, settings, and overlay visibility from one menu.

<img src="docs/images/zh-CN/action-menu.png" alt="Rounded action menu" width="280">

### Position, layout, and scaling

Drag the overlay to a suitable position or choose a preset in settings. Overlay scaling affects only the account overlay.

![Appearance settings](docs/images/zh-CN/appearance.png)

### Optional hotkeys

Click **Clear** or right-click a hotkey field to leave it unset. While recording a shortcut, the field shows **Backspace to clear · Esc to cancel**. Editing, clearing, and resetting shortcuts are staged until you click **Save**. Closing without saving keeps the previous shortcuts.

![Hotkey settings with individual clearing and explicit saving](docs/images/zh-CN/hotkeys.png)

![Backspace and Escape hints inside the recording field](docs/images/zh-CN/hotkey-recording.png)

### Remaining allowance and automatic updates

The panel shows remaining percentages when they are available. Unknown or unavailable values are not treated as zero.

![Usage settings with sample remaining percentages](docs/images/zh-CN/quota-settings.png)

Usage is updated by polling, rather than a live server push. A percentage does not indicate a fixed number of messages you can still send.

## Frequently asked questions

**Is this a plugin installed inside Codex?**

It is a separate Windows desktop tool that works alongside Codex through an overlay and the system tray. Extract and run it; you do not install it in a VS Code extension directory.

**How often does usage update?**

With automatic querying enabled, the default interval is 30 seconds for the active account and 60 seconds for other accounts. You can change these intervals in settings or manually refresh all accounts. Server updates may be delayed, and a failed query does not mean that an account has zero allowance.

**Are hotkeys required?**

No. Clear any shortcut to leave it unset and use the mouse instead. Hotkey edits, clears, and resets take effect only after you click **Save**. Closing the window without saving keeps the previous values.

**Will I keep my chats after switching?**

Local Codex chats, sessions, and workspaces remain shared; the tool switches account authorization. This concerns data stored on your computer, not merging cloud chat histories from different ChatGPT accounts.

## Improvements over the original project

The sections above describe the complete experience in this version. The following table covers additions and changes made in this derivative. Account switching, shared local sessions, authorization backup and rollback, and tray integration originate from the original project.

| Area | Improvements in this version |
| --- | --- |
| Simplified Chinese | Adds Chinese settings, menus, account labels, usage information, and messages alongside English and Russian. |
| Usage display and refresh | Extends status indicators with visible remaining percentages. Default query intervals change from 15 minutes for the active account and 60 minutes for others to 30 and 60 seconds respectively, with adjustable settings. |
| Overlay and positioning | Adds dragging and position persistence, uses a default height of 44 device-independent pixels, and replaces three top presets with bottom-left, bottom-center, and bottom-right positions with margins. |
| Hotkey saving | Replaces immediate application with staged edits and explicit saving. Adds individual clearing, right-click clearing, and an in-field Backspace hint; removes the clear-all button. |
| Windows switching compatibility | Adjusts process detection and launch behavior for the locally verified Codex desktop environment, narrows the process shutdown scope, and keeps the helper independent of Codex restarts. |
| Refresh and drag behavior | Fixes compact-menu displacement and unwanted closing during refresh, and prevents holding the third account while dragging from scrolling the main account out of view. |
| Dialogs and appearance | Keeps confirmation dialogs within the visible area, hides and reuses minimized auxiliary windows, rounds action menus and highlighted items, and keeps color and manual-status settings expanded. |
| Documentation and downloads | Adds illustrated usage guides, sample screenshots, and an upgrade record. Portable packages include documentation and the original project's license. |

See the [upgrade record (Chinese)](CHANGELOG_ZH.md) for the full list of changes.

## Data and configuration

- Account authorization and switching backups remain on your computer. Do not publish `auth.json`, account directories, logs, or backups.
- The usual settings directory is `%LOCALAPPDATA%\CodexProfileOverlay`. An existing Codex packaged-app data directory is reused when detected.
- Switching targets authorization data, rather than copying your entire user directory or workspace. Shared local-session behavior follows the original design.
- Usage queries communicate with the service. Network access, sign-in state, server responses, and Codex CLI compatibility affect availability.
- This repository contains source code, documentation, and sample screenshots. Generated runtime data and credentials are excluded.

## Build and validation

Development requires the .NET 8 SDK. Interactive UI checks require a Windows desktop environment.

```powershell
dotnet build CodexProfileOverlay.sln -c Release -p:Platform=x64
dotnet test CodexProfileOverlay.sln -c Release -p:Platform=x64
.\publish.ps1 -Configuration Release
```

Portable packages are written to `artifacts/`. Windows CI builds the project, runs core tests, scans repository files, and generates a downloadable artifact.

Run focused hotkey-saving and overlay-layout checks with:

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --hotkeys-only
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --header-only
```

Regenerate the sample screenshots without reading real authorization data:

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64 -- --docs docs/images/zh-CN
```

For implementation details, see the [architecture documentation](docs/ARCHITECTURE.md) and [UI check guide](tests/OverlayUiRegression/README.md).

## Validation scope

This version has been built and checked locally on Windows with .NET 8, including core tests and focused WPF interaction checks. Actual account switching and usage queries depend on your Codex installation and sign-in state. Isolated UI checks are not an end-to-end test of switching real accounts. Compatibility with every Codex version or monitor configuration is not claimed.

When reporting an issue, include your Codex version, display scaling, overlay mode, and reproduction steps. Hide real email addresses and account details in screenshots.

## Origin, acknowledgments, and license

**This project is based on [ZOONGG/codex-swap-account](https://github.com/ZOONGG/codex-swap-account).** Account management, switching transactions, authorization rollback, shared local sessions, tray integration, and the base overlay implementation come from ZOONGG and the original project's contributors. This repository builds on that work with Simplified Chinese support, Windows compatibility fixes, and interaction improvements. Thank you to the original author and contributors.

The original [MIT license and copyright notice](LICENSE) are retained. The English documentation from the imported source snapshot is preserved in [README_UPSTREAM.md](README_UPSTREAM.md), and Russian documentation is available in [README_RU.md](README_RU.md). See the original project's [v1.0.0](https://github.com/ZOONGG/codex-swap-account/tree/v1.0.0) for reference. This repository's first commit preserves the imported source snapshot; subsequent commits record the changes made here.

This is an independently maintained community project, with no affiliation or official partnership with OpenAI.
