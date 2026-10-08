# Compact popup placement regression

Run on Windows with the .NET 8 SDK:

```powershell
dotnet run --project tests/OverlayUiRegression/OverlayUiRegression.csproj -c Release -p:Platform=x64
```

The fixture loads the application's styles and overlay into synthetic windows, with generated profile names and quota data. It never starts `OverlayController`, reads real account directories, makes network requests, or switches accounts.

It checks that the popup remains attached to a live visual, aligns with the overlay and stays within the screen working area after five consecutive background updates and a manual refresh/reopen. Checks run at overlay scales 1.0 and 1.4 near both the top and bottom-right of the screen. Geometry evidence is written to `popup-regression.json` beside the test executable.

The preceding local release fails at the first background refresh with a detached popup anchor. The fixed release passes all 32 geometry checks. The fixture is separate from the platform-independent unit test solution because it requires an interactive Windows desktop.
