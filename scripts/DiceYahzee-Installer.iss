; Windows installer for DiceYahzee.
; Build: ISCC.exe /DAppVersion=1.5.0 /DSourceDir=<build folder> /DOutputDir=<dir> DiceYahzee-Installer.iss
; Installs per user so the in-game updater can replace files without administrator rights.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{6E0C5B9A-4F1D-4C55-9C2E-D1CE5A11E0B5}
AppName=DiceYahzee
AppVersion={#AppVersion}
AppPublisher=sakri9616-coder
AppPublisherURL=https://github.com/sakri9616-coder/DiceYahzee
DefaultDirName={localappdata}\Programs\DiceYahzee
DefaultGroupName=DiceYahzee
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=DiceYahzee-Setup-{#AppVersion}
UninstallDisplayIcon={app}\DiceYahzee.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "build-report.json,*_BurstDebugInformation_DoNotShip\*,*_BackUpThisFolder_ButDontShipItWithYourGame\*"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\DiceYahzee"; Filename: "{app}\DiceYahzee.exe"
Name: "{group}\{cm:UninstallProgram,DiceYahzee}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\DiceYahzee"; Filename: "{app}\DiceYahzee.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DiceYahzee.exe"; Description: "{cm:LaunchProgram,DiceYahzee}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
