; WinUpdateChecker installer script (Inno Setup 6).
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

#define MyAppName        "WinUpdateChecker"
#define MyAppPublisher   "WinUpdateChecker contributors"
#define MyAppURL         "https://github.com/adrian3092/win-update-checker"
#define MyAppExeName     "WinUpdateChecker.exe"

[Setup]
; Same AppId as v1 so installing v2 upgrades an existing v1 install in place.
AppId={{60cbe8cd-e316-4bc8-9a92-96305ec2c7d2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=WinUpdateChecker-Setup-{#MyAppVersion}-{#MyAppArch}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
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
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
