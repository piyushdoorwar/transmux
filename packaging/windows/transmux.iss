; Transmux — Inno Setup installer script
; Compiled by scripts/build-windows.ps1 via:
;   ISCC.exe /DAppVersion=<ver> /DSourceDir=<abs-path> /DRepoRoot=<abs-path> transmux.iss

#ifndef AppVersion
  #define AppVersion "0.0.0.0"
#endif

; Absolute path to staged package dir (artifacts/pkg/transmux-windows/Transmux)
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\pkg\transmux-windows\Transmux"
#endif

; Absolute path to repository root
#ifndef RepoRoot
  #define RepoRoot "..\.."
#endif

[Setup]
AppId={{A3F1C2B4-7E6D-4A9C-8B3E-2D5F1A4C8E07}
AppName=Transmux
AppVersion={#AppVersion}
AppPublisher=Piyush Doorwar
AppPublisherURL=https://github.com/piyushdoorwar/transmux
AppSupportURL=https://github.com/piyushdoorwar/transmux/issues
AppUpdatesURL=https://github.com/piyushdoorwar/transmux/releases
DefaultDirName={autopf}\Transmux
DefaultGroupName=Transmux
AllowNoIcons=yes
LicenseFile={#RepoRoot}\LICENSE
OutputDir={#RepoRoot}\artifacts\packages
OutputBaseFilename=transmux_{#AppVersion}_win-x64_setup
SetupIconFile={#RepoRoot}\src\Transmux.App\Assets\Icons\transmux.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableDirPage=no
DisableReadyPage=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\Transmux.exe
UninstallDisplayName=Transmux
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy everything from the staged directory (Transmux.exe + DLLs + ffmpeg.exe + ffprobe.exe)
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Transmux"; Filename: "{app}\Transmux.exe"
Name: "{autodesktop}\Transmux";  Filename: "{app}\Transmux.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Transmux.exe"; Description: "{cm:LaunchProgram,Transmux}"; Flags: nowait postinstall skipifsilent
