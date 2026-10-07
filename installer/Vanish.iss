; inno setup script -> dist\OblivionSetup.exe
; iscc installer\Vanish.iss   (run build.ps1 first, it creates dist\app)
; other version: iscc /DMyAppVersion=3.0.1 installer\Vanish.iss

#ifndef MyAppVersion
  #define MyAppVersion "3.1.0"
#endif
#define MyAppName "Oblivion"
#define MyAppPublisher "ANIL GÜL"
#define MyAppURL "https://github.com/anilg12/Oblivion-Uninstaller"
#define MyAppExeName "Oblivion.exe"

[Setup]
; same AppId as 1.x/2.x so it upgrades in place
AppId={{B7E1A3D2-1C4E-4F5A-9D2B-3E6F7A8B9C01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=© 2026 ANIL GÜL
VersionInfoVersion={#MyAppVersion}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} Setup
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=OblivionSetup
SetupIconFile=..\src\Vanish\Assets\Oblivion.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
WizardStyle=modern
WizardImageFile=art\wizard-large100.bmp,art\wizard-large150.bmp,art\wizard-large200.bmp
WizardSmallImageFile=art\wizard-small100.bmp,art\wizard-small150.bmp,art\wizard-small200.bmp
WizardImageStretch=no
; refresh the icon cache after install, otherwise shortcuts can keep an old cached icon
ChangesAssociations=yes
; needs admin to read HKLM and run uninstallers
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; self-contained ReadyToRun build (no single-file extraction, starts faster)
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser shellexec
