; Inno Setup script for Wallpache (Windows).
;
; Not run directly - scripts\make-installer.ps1 publishes the app first and
; passes SourceDir/AppVersion in via /D defines, then invokes ISCC on this
; file. To iterate on the script alone against an existing publish output:
;   iscc /DSourceDir="apps\windows\build\publish" /DAppVersion="1.0.0" scripts\wallpache-installer.iss
;
#ifndef SourceDir
  #define SourceDir "..\apps\windows\build\publish"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
; Must never change between releases - it is how Windows recognises an
; upgrade instead of installing a second, parallel copy. {{ escapes the
; literal opening brace Inno's constant syntax would otherwise expect.
AppId={{A025E8B5-85CE-4A06-ACF9-1B6495E28757}
AppName=Wallpache
AppVersion={#AppVersion}
AppVerName=Wallpache {#AppVersion}
AppPublisher=Wallpache
DefaultDirName={localappdata}\Programs\Wallpache
DefaultGroupName=Wallpache
DisableProgramGroupPage=yes
; Per-user install, no admin prompt: matches the app's own per-user Run-key
; startup registration, which already assumes no elevation is available.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=WallpacheSetup-{#AppVersion}
SetupIconFile=..\apps\windows\src\Wallpache.App\Assets\wallpache.ico
UninstallDisplayIcon={app}\Wallpache.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Matches Program.cs's SingleInstanceMutexName exactly, so Setup detects a
; running copy and asks the user to close it before installing over it.
AppMutex=Local\Wallpache.SingleInstance

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Wallpache"; Filename: "{app}\Wallpache.exe"
Name: "{group}\Uninstall Wallpache"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Wallpache"; Filename: "{app}\Wallpache.exe"; Tasks: desktopicon

[Registry]
; The app itself writes this Run-key value when the user enables "launch at
; sign-in" (System/StartupService.cs); this just makes sure uninstalling
; also removes it, so a deleted app is never left trying to start itself.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Wallpache"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Wallpache.exe"; Description: "Launch Wallpache"; Flags: nowait postinstall skipifsilent
