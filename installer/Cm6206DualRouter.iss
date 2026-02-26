; CM6206 Dual Router installer
; - Installs the app (self-contained publish output)
; - Optionally installs the CM6206 driver via pnputil (admin required)

#define MyAppName "CM6206 Dual-Input Bass Shaker"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

; NOTE: This .iss lives under installer/, so all repo-root paths must be prefixed with ..\\
#define PublishDir "..\\artifacts\\cm6206_dual_router_win-x64"
#define DriverDir "..\\cm6206_driver_payload\\WIN10\\Driver"
#define VirtualDriverDir "..\\virtual_audio_driver_payload\\WIN10\\Driver"

; Presence checks (avoid offering install tasks when only README is present)
#define DriverInf DriverDir + "\\CMUAC.inf"
#define VirtualDriverInf VirtualDriverDir + "\\CMVADR.inf"

#if DirExists(DriverDir) && FileExists(DriverInf)
  #define IncludeDriverPayload 1
#else
  #define IncludeDriverPayload 0
#endif

#if DirExists(VirtualDriverDir) && FileExists(VirtualDriverInf)
  #define IncludeVirtualDriverPayload 1
#else
  #define IncludeVirtualDriverPayload 0
#endif

#if IncludeDriverPayload
  #define IncludeAnyDriverPayload 1
#else
  #if IncludeVirtualDriverPayload
    #define IncludeAnyDriverPayload 1
  #else
    #define IncludeAnyDriverPayload 0
  #endif
#endif

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
#if IncludeDriverPayload
Name: "install_driver"; Description: "Install CM6206 USB 7.1 driver (recommended)"; Flags: checkedonce
#endif
#if IncludeVirtualDriverPayload
Name: "install_virtual_driver"; Description: "Install Virtual Game/Shaker playback endpoints (advanced)"; Flags: unchecked
#endif
Name: "desktop_icon"; Description: "Create a desktop icon"; Flags: unchecked

[Files]
; App payload (includes assets/generated that CI places under the publish folder)
Source: "{#PublishDir}\\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

; Driver payload (minimal subset needed for pnputil)
#if IncludeDriverPayload
Source: "{#DriverDir}\\*"; DestDir: "{tmp}\\cm6206_driver"; Flags: recursesubdirs createallsubdirs deleteafterinstall; Tasks: install_driver
#endif

; Virtual audio endpoints driver payload (minimal subset needed for pnputil)
#if IncludeVirtualDriverPayload
Source: "{#VirtualDriverDir}\\*"; DestDir: "{tmp}\\cm6206_virtual_audio_driver"; Flags: recursesubdirs createallsubdirs deleteafterinstall; Tasks: install_virtual_driver
#endif

[Run]
; Launch the app after install
Filename: "{app}\\Cm6206DualRouter.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Icons]
Name: "{group}\\{#MyAppName}"; Filename: "{app}\\Cm6206DualRouter.exe"
Name: "{userdesktop}\\{#MyAppName}"; Filename: "{app}\\Cm6206DualRouter.exe"; Tasks: desktop_icon

[Code]
#if IncludeDriverPayload
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
#endif

#if IncludeVirtualDriverPayload
procedure TryInstallVirtualAudioDriverWithPnPUtil();
var
  InfPath: string;
  ResultCode: Integer;
  Ok: Boolean;
begin
  if not WizardIsTaskSelected('install_virtual_driver') then
    exit;

  // Expected to be provided by the driver build pipeline.
  InfPath := ExpandConstant('{tmp}\\cm6206_virtual_audio_driver\\CMVADR.inf');
  if not FileExists(InfPath) then
  begin
    Log('Virtual audio driver INF not found: ' + InfPath);
    exit;
  end;

  Log('Installing virtual Game/Shaker endpoints driver via pnputil: ' + InfPath);
  Ok := Exec(ExpandConstant('{sys}\\pnputil.exe'),
    '/add-driver "' + InfPath + '" /install',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if Ok then
    Log(Format('pnputil exit code: %d (ignored)', [ResultCode]))
  else
    Log('pnputil failed to start (ignored)');
end;
#endif

#if IncludeAnyDriverPayload
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep <> ssPostInstall then
    exit;

  #if IncludeDriverPayload
  TryInstallDriverWithPnPUtil();
  #endif

  #if IncludeVirtualDriverPayload
  TryInstallVirtualAudioDriverWithPnPUtil();
  #endif
end;
#endif
