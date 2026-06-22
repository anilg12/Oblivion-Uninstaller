; Inno Setup script for Oblivion — produces dist\OblivionSetup.exe
; Build with:  iscc installer\Vanish.iss   (run build.ps1 first to create dist\Oblivion.exe)

#define MyAppName "Oblivion"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ANIL GUL"
#define MyAppExeName "Oblivion.exe"

[Setup]
AppId={{B7E1A3D2-1C4E-4F5A-9D2B-3E6F7A8B9C01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=OblivionSetup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Oblivion needs admin to read HKLM and run uninstallers
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The portable single-file build produced by build.ps1
Source: "..\dist\Oblivion.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser
