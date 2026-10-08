param(
    [string]$Configuration = "Release",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$localDotnet = Join-Path $repo ".dotnet\dotnet.exe"
if (Test-Path -LiteralPath $localDotnet) {
    $dotnet = [pscustomobject]@{ Source = $localDotnet }
} else {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
}

if (-not $dotnet) {
    throw "dotnet was not found. Install .NET 8 SDK or run the local SDK bootstrap used by this repository."
}

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $repo "artifacts\publish"
}

if (Test-Path -LiteralPath $Output) {
    Remove-Item -LiteralPath $Output -Recurse -Force
}

& $dotnet.Source publish (Join-Path $repo "src\CodexProfileOverlay\CodexProfileOverlay.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Platform=x64 `
    -o $Output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Include the attribution, license and illustrated usage guide in portable builds.
foreach ($name in @("README.md", "README_EN.md", "README_UPSTREAM.md", "README_RU.md", "CHANGELOG_ZH.md", "LICENSE")) {
    $document = Join-Path $repo $name
    if (Test-Path -LiteralPath $document -PathType Leaf) {
        Copy-Item -LiteralPath $document -Destination $Output -Force
    }
}
Copy-Item -LiteralPath (Join-Path $repo "docs") -Destination $Output -Recurse -Force

$zip = Join-Path $repo "artifacts\CodexProfileOverlay-win-x64-portable.zip"
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -Path (Join-Path $Output "*") -DestinationPath $zip -Force

$exe = Join-Path $Output "CodexProfileOverlay.exe"
$checksums = Join-Path $repo "artifacts\SHA256SUMS.txt"
$checksumLines = @(
    "{0}  {1}" -f (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash, (Split-Path -Leaf $exe)
    "{0}  {1}" -f (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash, (Split-Path -Leaf $zip)
)
Set-Content -LiteralPath $checksums -Value $checksumLines -Encoding ascii

Write-Host "Published to $Output"
Write-Host "Portable zip: $zip"
Write-Host "Checksums: $checksums"
