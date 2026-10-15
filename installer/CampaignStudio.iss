#define AppVersion "0.2.0"
[Setup]
AppId={{763F7AEC-A320-48A3-A0B0-E41D5C39BC73}
AppName=Campaign Studio
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\CampaignStudio
DefaultGroupName=Campaign Studio
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=CampaignStudio-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\CampaignStudio.Desktop.exe
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start minimized when I sign in"; Flags: unchecked
[Files]
Source: "..\artifacts\windows-native\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Campaign Studio"; Filename: "{app}\CampaignStudio.Desktop.exe"
Name: "{userdesktop}\Campaign Studio"; Filename: "{app}\CampaignStudio.Desktop.exe"; Tasks: desktopicon
Name: "{userstartup}\Campaign Studio"; Filename: "{app}\CampaignStudio.Desktop.exe"; Parameters: "--minimized"; Tasks: startup
[Run]
Filename: "{app}\CampaignStudio.Desktop.exe"; Description: "Open Campaign Studio"; Flags: nowait postinstall skipifsilent
