@echo off
:: Thin wrapper so make-installer.ps1 can be run by double-clicking in
:: Explorer. Windows' default file association for .ps1 is "Edit" (opens
:: Notepad), not "Run" - a deliberate security default, not something
:: specific to this script. .cmd files don't have that restriction.
:: Mirrors scripts/make-dmg.sh's +x bit on the macOS side.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make-installer.ps1" %*
pause
