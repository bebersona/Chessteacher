#define MyAppName "ChessTeacher"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "ChessTeacher Project"
#define MyAppExeName "ChessTeacher.exe"

[Setup]
AppId={{B8E6391D-861A-4A8D-BE14-3B67155ABAF0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ChessTeacher
DefaultGroupName=ChessTeacher
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=ChessTeacherSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
SetupIconFile=..\src\ChessTeacher.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE.txt
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ChessTeacher"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Documentation"; Filename: "{app}\README.md"
Name: "{autodesktop}\ChessTeacher"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

; There is deliberately no [Run] section. Installation never launches the app.
; User data under %LocalAppData%\ChessTeacher is intentionally preserved.
