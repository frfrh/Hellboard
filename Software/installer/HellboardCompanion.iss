; SPDX-License-Identifier: GPL-3.0-or-later
;
; Installer of Hellboard Companion (Inno Setup 7, https://jrsoftware.org/isinfo.php).
; Built by publish.ps1:  ISCC.exe /DAppVersion=<version> /DSourceDir=<package folder> /DOutputDir=<folder> HellboardCompanion.iss
;
; The user chooses "Install for me only" (no admin rights, %LOCALAPPDATA%\Programs) or "Install for all users"
; (Program Files, admin rights). Settings, logs and own stratagems in %APPDATA% / %LOCALAPPDATA% are kept
; on uninstall, so a reinstall or update keeps them.

#ifndef AppVersion
  #error "Pass the version: ISCC /DAppVersion=0.4.0 ..."
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\HellboardCompanion-" + AppVersion + "-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\publish"
#endif

#define AppName "Hellboard Companion"
#define AppExe "HellboardCompanion.exe"

[Setup]
; Never change the AppId: updates and the uninstaller find the installation by it.
AppId={{1DEFBA03-95C5-47F6-92C0-3C0726484560}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Hellboard project
AppPublisherURL=https://github.com/frfrh/Hellboard
AppSupportURL=https://github.com/frfrh/Hellboard/issues
AppUpdatesURL=https://github.com/frfrh/Hellboard/releases
; "Install for me only" or "for all users": the installer asks, {autopf} follows the choice.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 version 2004 or newer, like the Companion itself
MinVersion=10.0.19041
SetupIconFile=..\src\HellboardCompanion.App\Assets\hellboard.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=HellboardCompanion-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Closes a running Companion before files are replaced (single-instance mutex of the app).
AppMutex=Local\HellboardCompanion.SingleInstance
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; The Companion writes its autostart entry itself (Settings -> "Start with Windows"); remove it on uninstall,
; so it does not point to a deleted file. ValueType none: the installer does not create it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#AppName}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
