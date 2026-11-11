; Patch Pal installer script (Inno Setup 6).
; CI builds one installer per architecture:
;   ISCC.exe /DMyAppVersion=2.0.0 /DMyAppArch=x64   /DMySourceDir=..\dist\publish-x64   installer\setup.iss
;   ISCC.exe /DMyAppVersion=2.0.0 /DMyAppArch=arm64 /DMySourceDir=..\dist\publish-arm64 installer\setup.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif
#ifndef MyAppArch
  #define MyAppArch "x64"
#endif
#ifndef MySourceDir
  #define MySourceDir "..\dist\publish-x64"
#endif

#define MyAppName        "Patch Pal"
#define MyAppDirName     "PatchPal"
#define MyAppPublisher   "Patch Pal contributors"
#define MyAppURL         "https://github.com/adrian3092/patch-pal"
#define MyAppExeName     "PatchPal.exe"

[Setup]
; Same AppId as v1 (which installed as "WinUpdateChecker") so installing
; Patch Pal upgrades an existing v1 install in place.
AppId={{60cbe8cd-e316-4bc8-9a92-96305ec2c7d2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppDirName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=PatchPal-Setup-{#MyAppVersion}-{#MyAppArch}
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\src\PatchPal.App\PatchPal.ico
WizardStyle=modern
; Always all users, into Program Files: Patch Pal runs as admin, so its files must sit in a folder
; only an admin can change. A per-user copy left by 2.1.0 or earlier is removed by the code below.
PrivilegesRequired=admin
#if MyAppArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MySourceDir}\*_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md";    DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";      DestDir: "{app}"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Purge v1 (PowerShell) files when upgrading an existing install.
Type: files; Name: "{app}\UpdateChecker.ps1"
Type: files; Name: "{app}\Run.bat"
Type: files; Name: "{app}\Run-Console.bat"

[Icons]
Name: "{autoprograms}\{#MyAppName}";            Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autoprograms}\Uninstall {#MyAppName}";  Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";             Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Patch Pal asks for administrator rights, which only ShellExecute can prompt for: a plain
; CreateProcess of it fails with error 740 ("The requested operation requires elevation").
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent shellexec

[Code]
const
  // Where 2.1.0 and earlier registered a per-user ("just for me") install. Same AppId as above.
  OldPerUserKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{60cbe8cd-e316-4bc8-9a92-96305ec2c7d2}_is1';
  // The old uninstaller hands off to a copy of itself in %TEMP% and exits, so wait for its
  // entry to go: up to 120 x 250 ms.
  UninstallPolls = 120;
  UninstallPollMs = 250;

var
  OldCopyLeftBehind: Boolean;

// True only when the account that started Setup is shown to run at Medium or Low integrity
// (S-1-16-8192 / S-1-16-4096). Any failure counts as elevated, so nothing runs as admin.
function OriginalUserIsNotElevated(): Boolean;
var
  ResultCode: Integer;
begin
  Result := ExecAsOriginalUser(ExpandConstant('{cmd}'),
      '/c "' + ExpandConstant('{sys}\whoami.exe') + ' /groups | ' +
      ExpandConstant('{sys}\findstr.exe') + ' /c:S-1-16-8192 /c:S-1-16-4096 >nul"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
end;

function WaitForOldEntryToGo(): Boolean;
var
  I: Integer;
begin
  for I := 1 to UninstallPolls do
  begin
    if not RegKeyExists(HKCU, OldPerUserKey) then
      Break;
    Sleep(UninstallPollMs);
  end;
  Result := not RegKeyExists(HKCU, OldPerUserKey);
end;

// The old copy sits in a folder the user's normal account can change, so its uninstaller may
// have been swapped out. Run it only with the user's normal rights, never as admin; if that
// isn't possible, leave the copy and say so at the end. Never blocks the install.
procedure RemoveOldPerUserCopy();
var
  Uninstaller: String;
  ResultCode: Integer;
begin
  if not RegQueryStringValue(HKCU, OldPerUserKey, 'UninstallString', Uninstaller) then
    Exit;
  Uninstaller := RemoveQuotes(Uninstaller);

  if not FileExists(Uninstaller) then
  begin
    // The uninstaller is already gone; drop the stale entry so it isn't taken as this install.
    RegDeleteKeyIncludingSubkeys(HKCU, OldPerUserKey);
    Exit;
  end;

  if not OriginalUserIsNotElevated() then
  begin
    Log('Old per-user copy left in place: could not run its uninstaller without admin rights.');
    OldCopyLeftBehind := True;
    Exit;
  end;

  if not ExecAsOriginalUser(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '',
      SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    OldCopyLeftBehind := True
  else
    OldCopyLeftBehind := not WaitForOldEntryToGo();

  if OldCopyLeftBehind then
    Log('Old per-user copy could not be removed: ' + Uninstaller);
end;

function InitializeSetup(): Boolean;
begin
  RemoveOldPerUserCopy();
  Result := True;
end;

// Patch Pal runs as admin, so its folder must never be one the user's normal account can change.
function IsInsideUserProfile(Dir: String): Boolean;
begin
  Result := Pos(Uppercase(AddBackslash(ExpandConstant('{%USERPROFILE}'))),
                Uppercase(AddBackslash(ExpandFileName(Dir)))) = 1;
end;

// Whatever an old per-user entry or /DIR= says, start from Program Files.
procedure InitializeWizard();
begin
  if IsInsideUserProfile(WizardForm.DirEdit.Text) then
    WizardForm.DirEdit.Text := ExpandConstant('{autopf}\{#MyAppDirName}');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and IsInsideUserProfile(WizardForm.DirEdit.Text) then
  begin
    SuppressibleMsgBox('Patch Pal runs as administrator, so it can''t be installed inside your ' +
      'user folder. Choose a folder such as Program Files.', mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssDone) and OldCopyLeftBehind then
    SuppressibleMsgBox('Patch Pal is now installed for all users. An older copy installed only ' +
      'for your account is still there; remove it from Settings > Apps > Installed apps. ' +
      'Your settings and history are kept either way.', mbInformation, MB_OK, IDOK);
end;
