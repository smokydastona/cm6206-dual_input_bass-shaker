; CM6206 Dual Router installer
; - Installs the app (self-contained publish output)
; - Optionally installs the CM6206 driver via pnputil (admin required)

#define MyAppName "CM6206 Dual-Input Bass Shaker"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

; NOTE: This .iss lives under installer/, so all repo-root paths must be prefixed with ..\\
#define PublishDir "..\\artifacts\\cm6206_dual_router_win-x64"

[Setup]
AppId={{8C3B0A18-5D4A-4C89-8C25-37C67A2ACB48}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=smokydastona
DefaultDirName={autopf}\\Cm6206DualRouter
DefaultGroupName=Cm6206DualRouter
DisableProgramGroupPage=yes
OutputDir=..\\artifacts\\installer
OutputBaseFilename=Cm6206DualRouterSetup_{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "install_driver"; Description: "Install CM6206 USB 7.1 driver (recommended)"; Flags: checkedonce
Name: "desktop_icon"; Description: "Create a desktop icon"; Flags: unchecked

[Files]
; App payload (includes assets/generated that CI places under the publish folder)
Source: "{#PublishDir}\\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

; Driver payload (minimal subset needed for pnputil)
Source: "..\\cm6206_extracted\\[CMedia CM6206] Windows USB 7.1 Audio Adapter\\WIN10\\SoftwareDriver\\Driver\\*"; DestDir: "{tmp}\\cm6206_driver"; Flags: recursesubdirs createallsubdirs deleteafterinstall; Tasks: install_driver

[Run]
; Launch the app after install
Filename: "{app}\\Cm6206DualRouter.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Icons]
Name: "{group}\\{#MyAppName}"; Filename: "{app}\\Cm6206DualRouter.exe"
Name: "{userdesktop}\\{#MyAppName}"; Filename: "{app}\\Cm6206DualRouter.exe"; Tasks: desktop_icon

[Code]
procedure TryInstallDriverWithPnPUtil();
var
  InfPath: string;
  ResultCode: Integer;
  Ok: Boolean;
begin
  if not WizardIsTaskSelected('install_driver') then
    exit;

  InfPath := ExpandConstant('{tmp}\\cm6206_driver\\CMUAC.inf');
  if not FileExists(InfPath) then
  begin
    Log('CM6206 driver INF not found: ' + InfPath);
    exit;
  end;

  Log('Installing CM6206 driver via pnputil: ' + InfPath);
  Ok := Exec(ExpandConstant('{sys}\\pnputil.exe'),
    '/add-driver "' + InfPath + '" /install',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if Ok then
    Log(Format('pnputil exit code: %d (ignored)', [ResultCode]))
  else
    Log('pnputil failed to start (ignored)');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    TryInstallDriverWithPnPUtil();
end;
