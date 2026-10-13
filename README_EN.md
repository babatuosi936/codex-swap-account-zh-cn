<div align="center">

# Codex Account Manager · Codex Swap Account

**A Windows Codex account manager, account switcher, and quota monitor for multiple ChatGPT Plus accounts.**

View five-hour and weekly remaining quotas for multiple accounts in a draggable overlay beside the Codex desktop app. Switch accounts and continue using your local chats and workspaces, with automatic quota refresh, Chinese and English interfaces, and optional hotkeys.

**English** · [简体中文](README.md)

[Public release EXE](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest/download/CodexProfileOverlay.exe) · [Public v1.2.0 release](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/tag/v1.2.0) · [How to use](#quick-start)

For everyday use, download the EXE and run it directly. No extraction or separate .NET installation is required.

</div>

**Local trial: 1.3.2-local.** Separate account windows have independent sign-ins and shared local conversation history, without Cockpit Tools. Selecting a compatible open independent account focuses it immediately; other accounts offer a choice, with a separate window selected by default. Advanced settings have been audited, with import/export and reset wiring repaired; see the [Advanced controls reference](docs/ADVANCED_SETTINGS.md). GitHub public downloads above still provide v1.2.0. On Windows Store Codex 26.1007.2314.0, two signed-out synthetic account homes have launched two simultaneously visible official windows with distinct processes, homes, and desktop caches. Real signed-in identities and history display still need verification. See the [trial guide and design boundaries](docs/ISOLATED_MULTI_ACCOUNT.md).

![Expanded overlay with sample accounts and remaining allowance](docs/images/zh-CN/expanded-mode.png)

## What it does

If you sign in to Codex with several ChatGPT Plus accounts, this tool brings their names, active status, and remaining quotas into one panel beside the Codex window. Once an account has been signed in and saved, select it from the overlay or system tray to start a switch.

### Is there a Codex alternative to a VS Code account manager for switching Plus accounts and checking multiple accounts' quotas?

This project provides account management, account switching, and quota monitoring for the **Codex desktop app on Windows**. It is a standalone community tool: download and run the EXE. It offers a workflow similar to a VS Code account manager without requiring a VS Code extension or a native Codex plugin.

Select a saved, signed-in account from the overlay. The panel periodically refreshes the available five-hour and weekly remaining percentages; quota updates may be delayed.

**1.3.0-local enables separate account windows by default.** Selecting an account focuses an already-open window with a matching configuration, or starts that account's own window while the existing default window continues running. Sign-ins and desktop caches remain separate; the new windows use the same existing local history. Independent histories, incorrect directory links, and incompatible old account windows are rejected with the original data preserved.

Continue each conversation in one window. Instant updates in other windows, automatic follower behavior, and repair of existing VS Code writer-lock directories are not promised. **Settings → General → Prefer a separate account window** only chooses the default action; both actions remain available.

| Feature | What you can do |
| --- | --- |
| Account management and switching | Add, name, and manage accounts. Select through the overlay, tray menu, or a custom hotkey. The local trial opens or focuses separate account windows by default and highlights the active account. |
| Usage panel | See available five-hour and weekly remaining percentages for multiple accounts, with automatic updates and a manual refresh-all action. |
| Shared local history | Account windows use the same conversation files, attachments, and databases without copying or merging them. Continue each conversation in one window. |
| Two overlay layouts | Expanded mode sizes itself to the account and quota text. Compact mode uses a single account button and menu. Automatic mode switches to compact when the full account row will not fit, including the selected UI scale. |
| Position and appearance | Drag the overlay, remember its position, choose a preset beside the menu or along the bottom, and adjust scaling and light or dark themes. Windows DPI scaling is supported. |
| Mouse or hotkeys | Use the mouse for everyday actions, or configure optional hotkeys. Leave individual hotkeys unset if you prefer. Hotkey edits take effect only after clicking **Save**. |
| Languages and system tray | Use English, Simplified Chinese, Russian, or the system language. The tray provides access to accounts, settings, and overlay visibility. |
| Switch current account | Choose this action to use the existing authorization backup, switch and failure-recovery flow; Advanced controls its shutdown behavior. |

Separate account windows preserve default authorization and existing windows. Legacy single-instance switching closes and restarts the selected Codex process, so finish its work first. Usage values come from server queries and may take time to update.

## Quick start

Basic requirements: **Windows 10/11 x64 and the Codex desktop app**. A working Codex CLI is also required for adding accounts and automatic usage queries. The account-window steps below apply to local 1.3.0-local, initially targeting Windows Store Codex 26.1007.2314.0. Two signed-out synthetic windows have been verified; real-account end-to-end verification is pending.

1. Use the local 1.3.0-local executable or portable package delivered in `artifacts/`. For the public release, use [Public release EXE](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest/download/CodexProfileOverlay.exe), which currently provides v1.2.0.
2. Put the EXE in a permanent folder and double-click it to run. It includes its runtime, so you do not need to install .NET separately.
3. Open Codex. The account overlay appears once the tool detects its window. You can also show or hide the overlay from the system tray.
4. Choose **English** under **Settings → Language**, or use the system language setting.
5. Use **Add profile** to sign in to your accounts. Once saved, they appear in the overlay with any available usage information.
6. Keep **Settings → General → Prefer a separate account window** enabled. Selecting an already-open compatible independent account focuses it immediately; otherwise choose between opening a new window or switching the current account, with the new window selected by default.

If preparation reports independent history or an incompatible existing window, preserve the directories. Save your work and close the old account window before reviewing the trial guide. The tool does not automatically migrate, clear, or merge these histories.

Renaming the profile directory or removing an account is blocked while its process is running. Compatibility checks for existing windows are read-only and do not prepare their directories again.

If you do not need hotkeys, open **Settings → Hotkeys**, clear the individual shortcuts, and click **Save**. To change the overlay position, drag it or choose a preset under **Settings → Appearance**.

**The public release ZIP is optional.** To keep the illustrated guides with the public release, download the [public program and documentation ZIP](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest/download/CodexProfileOverlay-win-x64-portable.zip), extract it, and run the EXE inside. It contains the same public executable as the standalone download. These links do not yet provide 1.3.0-local.

The [release page](https://github.com/babatuosi936/codex-swap-account-zh-cn/releases/latest) provides release notes and `SHA256SUMS.txt`. The checksum file helps verify your download; it is not required to run the program. Windows may show a SmartScreen prompt for an unsigned executable; check the source and published hashes before deciding whether to run it.

This is a public repository. You can view the source code and screenshots and download releases without signing in to GitHub.

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

Separate account windows use the same existing local Codex history and attachments while keeping sign-ins independent. No raw history is copied or merged. If a profile already contains independent history, preparation stops and preserves it. This concerns computer-local data, not merging cloud chats from different ChatGPT accounts.

**Can several windows continue the same conversation at once?**

Continue each conversation in one window. The native synthetic test confirms that a second process cannot resume a thread while the first holds its writer lock; it can resume after the first exits. Real desktop viewing, refresh, and handoff behavior still need verification. Shared files do not imply real-time synchronization.

## Improvements over the original project

The sections above describe the complete experience in this version. The following table covers additions and changes made in this derivative. Account switching, shared local sessions, authorization backup and rollback, and tray integration originate from the original project.

| Area | Improvements in this version |
| --- | --- |
| Separate account windows (local trial) | Independently implements account directories, shared history links and SQLite environment, and separate desktop cache startup. No Cockpit Tools dependency; independent histories and incorrect links remain untouched and are rejected. |
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
- Separate account windows retain each profile's authorization and share existing history/databases through directory links and child-process settings, without history import, copying or merging. Choosing Switch current account uses the original authorization-switch and legacy-migration behavior.
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

For implementation details, see the [separate-account-window design](docs/ISOLATED_MULTI_ACCOUNT.md), [architecture documentation](docs/ARCHITECTURE.md), and [UI check guide](tests/OverlayUiRegression/README.md).

## Validation scope

Local 1.3.0-local uses independently written C# code and initially targets Windows Store Codex 26.1007.2314.0. Validation passed 192 core tests, 22 launcher-policy checks, and 205 WPF checks. Native CLI 0.162.0-alpha.17.2 verified shared databases and thread writer locks using two synthetic homes. An actual desktop probe launched two simultaneously visible signed-out official windows, verified distinct processes, homes, caches, all three environment paths and package identity, and confirmed that previously existing window processes remained alive.

The desktop probe used no real credentials, histories, or model inference. Signed-in identities, real attachment display, cross-window refresh, and desktop conversation handoff still need verification. Closing the synthetic windows left background processes, so cleanup stopped only those two verified synthetic processes; graceful complete desktop exit is not proven. These results do not establish compatibility with every Codex version or monitor configuration.

When reporting an issue, include your Codex version, display scaling, overlay mode, and reproduction steps. Hide real email addresses and account details in screenshots.

## Origin, acknowledgments, and license

**This project is based on [ZOONGG/codex-swap-account](https://github.com/ZOONGG/codex-swap-account).** Account management, switching transactions, authorization rollback, shared local sessions, tray integration, and the base overlay implementation come from ZOONGG and the original project's contributors. This repository builds on that work with Simplified Chinese support, Windows compatibility fixes, and interaction improvements. Thank you to the original author and contributors.

The original [MIT license and copyright notice](LICENSE) are retained. The English documentation from the imported source snapshot is preserved in [README_UPSTREAM.md](README_UPSTREAM.md), and Russian documentation is available in [README_RU.md](README_RU.md). See the original project's [v1.0.0](https://github.com/ZOONGG/codex-swap-account/tree/v1.0.0) for reference. This repository's first commit preserves the imported source snapshot; subsequent commits record the changes made here.

This is an independently maintained community project, with no affiliation or official partnership with OpenAI.
