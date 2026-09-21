; Installer of DTM Toolbox for BLE. build\build.ps1 compiles it and passes the version:
;   ISCC /DAppVersion=1.0.0 installer\DtmToolbox.iss
; The .NET Framework 4.8 offline installer is expected in installer\redist (build\fetch-redist.ps1).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "DTM Toolbox for BLE"
#define AppExe "DtmToolbox.exe"
#define AppPublisher "Felipe Dantas da Silva"
#define AppUrl "https://github.com/felipedts/dtm-toolbox-for-ble"
#define DotNetInstaller "ndp48-x86-x64-allos-enu.exe"
; "Release" value of .NET Framework 4.6.2, the oldest version the application runs on.
#define RequiredDotNetRelease 394802

[Setup]
AppId={{8C6E2B3E-5B1D-4C2F-9F0B-6A1E2D7C4A55}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
; All users by default. "Install for me only" needs no administrator, and cannot install .NET.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
MinVersion=6.1sp1
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=DtmToolbox-{#AppVersion}-setup
SetupIconFile=..\src\DtmToolbox\Resources\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
; Not solid: the 116 MB .NET installer is only unpacked on a machine that needs it.
SolidCompression=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\src\DtmToolbox\bin\Release\net462\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\docs\quick-start.md"; DestDir: "{app}"; DestName: "Quick start.txt"; Flags: ignoreversion
Source: "redist\{#DotNetInstaller}"; Flags: dontcopy

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function DotNetRelease: Cardinal;
var
  Release: Cardinal;
begin
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
    Result := Release
  else
    Result := 0;
end;

function NeedsDotNet: Boolean;
begin
  Result := DotNetRelease < {#RequiredDotNetRelease};
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if NeedsDotNet and not IsAdminInstallMode then
  begin
    MsgBox('This computer does not have .NET Framework 4.6.2 or later, which {#AppName} needs.' + #13#10#13#10 +
      'Setup can install it, but only with administrator rights. Run Setup again and choose "Install for all users".',
      mbError, MB_OK);
    Result := False;
  end;
end;

// Runs the .NET Framework 4.8 installer packed with Setup, only on a machine that needs it.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if not NeedsDotNet then
    Exit;

  ExtractTemporaryFile('{#DotNetInstaller}');
  if not Exec(ExpandConstant('{tmp}\{#DotNetInstaller}'), '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    Result := 'The .NET Framework 4.8 installer could not be started: ' + SysErrorMessage(ResultCode)
  else if (ResultCode = 3010) or (ResultCode = 1641) then
    NeedsRestart := True
  else if ResultCode <> 0 then
    Result := 'The .NET Framework 4.8 installer ended with code ' + IntToStr(ResultCode) + '.' + #13#10#13#10 +
      'On Windows 7 SP1 it needs the updates KB4474419 and KB4490628 (SHA-2 code signing support) installed first.';
end;
