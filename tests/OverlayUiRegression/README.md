# Compact popup placement regression

Run on Windows with the .NET 8 SDK:

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64
```

The fixture loads the application's styles and overlay into synthetic windows, with generated profile names and quota data. It never starts `OverlayController`, reads real account directories, makes network requests, or switches accounts.

It checks that the popup remains attached to a live visual, aligns with the overlay and stays within the screen working area after five consecutive background updates and three consecutive manual refresh clicks. The menu must remain open through each refresh and display the asynchronously updated quota values. Opening the profile manager must still close the menu. Checks run with production mouse capture and with focus-independent positioning, at overlay scales 1.0 and 1.4 near both the top and bottom-right of the screen. Geometry evidence is written to `popup-regression.json` beside the test executable.

The release preceding the anchor fix fails at the first background refresh with a detached popup anchor. The current fixture also rejects closing the menu on manual refresh. It performs 96 geometry checks in total. The fixture is separate from the platform-independent unit test solution because it requires an interactive Windows desktop.
