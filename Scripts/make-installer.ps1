<#
.SYNOPSIS
  Builds a distributable Wallpache Setup.exe and places it in dist/.

.DESCRIPTION
  Publishes the Windows app self-contained for win-x64 (so a user does not
  need the .NET runtime installed separately), then compiles
  Scripts/wallpache-installer.iss with Inno Setup's ISCC into a single
  installer executable.

  Mirrors Scripts/make-dmg.sh on the macOS side: same dist/ output location,
  same "build once, package once" shape.

.PARAMETER Version
  Overrides the version baked into the installer's filename and metadata.
  Defaults to the <Version> already set in Wallpache.App.csproj.

.EXAMPLE
  Scripts\make-installer.ps1
.EXAMPLE
  Scripts\make-installer.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$Project = Join-Path $RepoRoot "apps\windows\src\Wallpache.App\Wallpache.App.csproj"
$PublishDir = Join-Path $RepoRoot "apps\windows\build\publish"
$DistDir = Join-Path $RepoRoot "dist"
$IssScript = Join-Path $PSScriptRoot "wallpache-installer.iss"

if (-not $Version) {
    $csprojContent = Get-Content $Project -Raw
    if ($csprojContent -notmatch '<Version>([^<]+)</Version>') {
        throw "Could not find <Version> in $Project; pass -Version explicitly."
    }
    $Version = $Matches[1]
}

Write-Host "==> Publishing Wallpache $Version (win-x64, self-contained)"
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "==> Locating Inno Setup compiler"
$Iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($Iscc) {
    $IsccPath = $Iscc.Source
} else {
    $Candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $IsccPath = $Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $IsccPath) {
    throw "ISCC.exe (Inno Setup) not found. Install it with: winget install --id JRSoftware.InnoSetup -e"
}
Write-Host "    $IsccPath"

New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

Write-Host "==> Compiling the installer"
& $IsccPath "/DSourceDir=$PublishDir" "/DAppVersion=$Version" $IssScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$OutputExe = Join-Path $DistDir "WallpacheSetup-$Version.exe"
Write-Host ""
Write-Host "Done. Installer at: $OutputExe"
Write-Host ""
Write-Host "NOTE: this build is unsigned. Windows SmartScreen will warn on first"
Write-Host "run for an unrecognised publisher until it is code-signed - the same"
Write-Host "caveat the macOS build documents for an unnotarized .app."
