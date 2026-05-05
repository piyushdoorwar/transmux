; Lumyn Media Player — Inno Setup installer script
; Compiled by scripts/build-windows.ps1 via:
;   ISCC.exe /DAppVersion=<ver> /DSourceDir=<abs-path> /DRepoRoot=<abs-path> lumyn.iss

#ifndef AppVersion
  #define AppVersion "0.0.0.0"
#endif

; Absolute path to the staged package directory (artifacts/pkg/lumyn-windows/Lumyn)
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\pkg\lumyn-windows\Lumyn"
#endif

; Absolute path to the repository root
#ifndef RepoRoot
  #define RepoRoot "..\.."
#endif

[Setup]
; Unique AppId — do not change once the installer is publicly distributed
AppId={{B7E2C4A1-3F5D-4E8B-9C2A-1D6F8E3B7A04}
AppName=Lumyn
AppVersion={#AppVersion}
AppPublisher=Piyush Doorwar
AppPublisherURL=https://github.com/piyushdoorwar/lumyn-media-player
AppSupportURL=https://github.com/piyushdoorwar/lumyn-media-player/issues
AppUpdatesURL=https://github.com/piyushdoorwar/lumyn-media-player/releases
DefaultDirName={autopf}\Lumyn
DefaultGroupName=Lumyn
AllowNoIcons=yes
LicenseFile={#RepoRoot}\LICENSE
OutputDir={#RepoRoot}\artifacts\packages
OutputBaseFilename=lumyn_{#AppVersion}_win-x64_setup
SetupIconFile={#RepoRoot}\src\Lumyn.App\Assets\Icons\lumyn.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableDirPage=no
DisableReadyPage=no
; x64 only
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 minimum (.NET 10 requirement)
MinVersion=10.0.17763
; Show app icon in Add/Remove Programs
UninstallDisplayIcon={app}\Lumyn.exe
UninstallDisplayName=Lumyn
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy everything from the staged directory (Lumyn.exe + all DLLs + mpv)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Lumyn"; Filename: "{app}\Lumyn.exe"
Name: "{autodesktop}\Lumyn"; Filename: "{app}\Lumyn.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Lumyn.exe"; Description: "{cm:LaunchProgram,Lumyn}"; Flags: nowait postinstall skipifsilent

[Registry]
; ── ProgId ──────────────────────────────────────────────────────────────────
Root: HKCU; Subkey: "Software\Classes\Lumyn.MediaFile"; ValueType: string; ValueName: ""; ValueData: "Media file"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Lumyn.MediaFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Lumyn.exe,0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Lumyn.MediaFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Lumyn.exe"" ""%1"""; Flags: uninsdeletekey

; ── Video extensions ─────────────────────────────────────────────────────────
Root: HKCU; Subkey: "Software\Classes\.mp4"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.m4v"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mkv"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mk3d"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.webm"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.avi"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mov"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.wmv"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mpg"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mpeg"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.flv"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.3gp"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ogv"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ogm"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ts"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mts"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.m2ts"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.divx"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue

; ── Audio extensions ─────────────────────────────────────────────────────────
Root: HKCU; Subkey: "Software\Classes\.mp3"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.flac"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.ogg"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.oga"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.wav"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.m4a"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.m4b"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.aac"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.wma"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.opus"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.mka"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.aiff"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\.aif"; ValueType: string; ValueName: ""; ValueData: "Lumyn.MediaFile"; Flags: uninsdeletevalue
