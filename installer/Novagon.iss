; Script Inno Setup cho Novagon
; Tuân thủ cấu trúc Portable Stack tương tự Laragon, mặc định tại C:\Novagon

#ifndef MyAppName
#define MyAppName "Novagon"
#endif

#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#ifndef MyAppPublisher
#define MyAppPublisher "Novagon"
#endif

#ifndef MyAppURL
#define MyAppURL "https://github.com/acmavirus/Novagon"
#endif

#ifndef MyAppExeName
#define MyAppExeName "Novagon.exe"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename "Novagon-Setup-" + MyAppVersion
#endif

[Setup]
AppId={{8B8A6273-0DB9-4EF3-A152-BE9B76A0353F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName=C:\Novagon
DisableDirPage=no
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist\installer
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\src\Novagon.UI\Assets\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Khởi động cùng Windows (Run on Windows startup)"; GroupDescription: "Tùy chọn khởi động:"; Flags: unchecked

[Dirs]
Name: "{app}\logs"; Permissions: users-full
Name: "{app}\backups"; Permissions: users-full
Name: "{app}\www"; Permissions: users-full
Name: "{app}\etc\ssl"; Permissions: users-full
Name: "{app}\etc\nginx\sites-enabled"; Permissions: users-full

[Files]
Source: "..\dist\package\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\package\bin\*"; DestDir: "{app}\bin"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\package\etc\*"; DestDir: "{app}\etc"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\package\www\*"; DestDir: "{app}\www"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Novagon"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
