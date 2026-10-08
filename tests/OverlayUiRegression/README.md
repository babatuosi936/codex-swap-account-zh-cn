# Compact popup placement regression

Run on Windows with the .NET 8 SDK:

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64
```

The fixture loads the application's styles and overlay into synthetic windows, with generated profile names and quota data. It never starts `OverlayController`, reads real account directories, makes network requests, or switches accounts.

It checks that the popup remains attached to a live visual, aligns with the overlay and stays within the screen working area after five consecutive background updates and three consecutive manual refresh clicks. The menu must remain open through each refresh and display the asynchronously updated quota values. Opening the profile manager must still close the menu. Checks run with production mouse capture and with focus-independent positioning, at overlay scales 1.0 and 1.4 near both the top and bottom-right of the screen. Geometry evidence is written to `popup-regression.json` beside the test executable.

The release preceding the anchor fix fails at the first background refresh with a detached popup anchor. The current fixture also rejects closing the menu on manual refresh. It performs 96 popup checks and four expanded-row checks in total. The expanded-row scenarios reproduce a third-account focus request before and during dragging, require the main account to remain visible without a horizontal offset change, and verify that normal navigation can still scroll after the gesture. The fixture is separate from the platform-independent unit test solution because it requires an interactive Windows desktop.

The auxiliary-window scenarios add 14 checks using the real settings and profile-manager window types with fake dependencies and a native owner. They cover three minimize/hide/restore cycles, including the native title-bar minimize command, require managed and native visibility to agree, verify the same window and controls are reused, and close a window while its minimize callback is queued. Run just these scenarios with `-- --auxiliary-only` appended to the command above.

Header layout adds 12 checks at scales 0.8, 1.0 and 1.4, in both compact and expanded modes, with and without quota lines. The floating header must match the full Codex title bar's 44 logical pixels at default scale and preserve vertically unclipped account names and quota text. Run these checks alone with `-- --header-only`. A synthetic Chinese expanded-header preview is saved as `header-layout-preview.png` beside the executable.

Hotkey editing adds 14 checks using fake accounts and a local settings store. Left-click Clear, right-click clearing, Backspace and Reset must only change a draft. Bulk clearing is absent, and clearing visible rows must preserve other profile shortcuts. The recording field shows Backspace and Esc help. Saving another setting must not commit that draft; only the hotkey Save button persists it. Empty shortcuts register nothing, a failed save preserves live settings, and closing and reopening discards unsaved edits while retaining saved empty shortcuts. Run these scenarios with `-- --hotkeys-only`; `hotkey-settings-preview.png` shows the recording hint and mouse-clear controls.

Run with `-- --docs docs/images/zh-CN` to render the Chinese README images from actual controls using synthetic account names and quota values. This documentation mode does not run the controller, read real authorizations or capture the desktop. It generates eight images for expanded and compact overlays, their menus, appearance settings, hotkey editing and quota settings.
