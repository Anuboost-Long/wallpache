<#
.SYNOPSIS
  Builds a distributable Wallpache Setup.exe and places it in dist/.

.DESCRIPTION
  Publishes the Windows app via its FolderProfile (self-contained win-x64, so
  a user does not need the .NET runtime installed separately - see
  Properties/PublishProfiles/FolderProfile.pubxml for the actual settings,
  the one place they're defined), then compiles
  scripts/wallpache-installer.iss with Inno Setup's ISCC into a single
  installer executable.

  Mirrors scripts/make-dmg.sh on the macOS side: same dist/ output location,
  same "build once, package once" shape.

.PARAMETER Version
  Overrides the version baked into the installer's filename and metadata.
  Defaults to the <Version> already set in Wallpache.App.csproj.

.PARAMETER PublishDir
  Where the self-contained build lives (or should be written). Defaults to
  apps\windows\build\publish.

.PARAMETER SkipPublish
  Skips the `dotnet publish` step and compiles the installer directly from an
  existing PublishDir. Used by Wallpache.App.csproj's BuildInstaller target,
  which runs this after MSBuild's own Publish step has already produced that
  output - so the app is never published twice.

.EXAMPLE
  scripts\make-installer.ps1
.EXAMPLE
  scripts\make-installer.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$PublishDir,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$Project = Join-Path $RepoRoot "apps\windows\src\Wallpache.App\Wallpache.App.csproj"
if (-not $PublishDir) {
    $PublishDir = Join-Path $RepoRoot "apps\windows\build\publish"
}
$DistDir = Join-Path $RepoRoot "dist"
$IssScript = Join-Path $PSScriptRoot "wallpache-installer.iss"

if (-not $Version) {
    $csprojContent = Get-Content $Project -Raw
    if ($csprojContent -notmatch '<Version>([^<]+)</Version>') {
        throw "Could not find <Version> in $Project; pass -Version explicitly."
    }
    $Version = $Matches[1]
}

if ($SkipPublish) {
    Write-Host "==> Skipping publish, using existing build at $PublishDir"
    if (-not (Test-Path $PublishDir)) {
        throw "PublishDir $PublishDir does not exist; omit -SkipPublish to publish first."
    }
} else {
    Write-Host "==> Publishing Wallpache $Version (via FolderProfile)"
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }

    # SkipInstallerBuild=true because this script does the ISCC compile
    # itself below - without it, Wallpache.App.csproj's BuildInstaller
    # target would also fire (it hooks the same Publish target VS's
    # Publish button uses) and compile the installer a second time.
    dotnet publish $Project `
        -c Release `
        -p:PublishProfile=FolderProfile `
        -p:Version=$Version `
        -p:SkipInstallerBuild=true `
        -o $PublishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
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
