; Simple Launcher - per-user Windows installer.
;
; This script is compiled by scripts/package-release.ps1 with -BuildInstaller.
; It requires Inno Setup 6.5 or later (CreateDownloadPage / built-in download
; support). The script installs the Avalonia payload produced for one RID as
; SimpleLauncher.exe, plus the shared content files and Updater.exe. The WPF
; project remains in the source tree as a reference but is not shipped.
;
; Packaging defines (all required):
;   AppVersion, Rid, PayloadDir, OutputDir, RepoDir,
;   ArchitecturesAllowed, ArchitecturesInstallIn64BitMode,
;   RuntimeArch, RuntimeUrl, RuntimeFileName, RuntimePageUrl
;
; The installer is deliberately per user. It never requests elevation for the
; app install; the .NET 11 runtime bootstrapper asks for elevation only
; when the runtime is missing and the user chooses the automatic install.

#ifndef AppVersion
  #error AppVersion is not defined. Build with scripts/package-release.ps1 -BuildInstaller.
#endif
#ifndef Rid
  #error Rid is not defined.
#endif
#ifndef PayloadDir
  #error PayloadDir is not defined.
#endif
#ifndef OutputDir
  #error OutputDir is not defined.
#endif
#ifndef RepoDir
  #error RepoDir is not defined.
#endif
#ifndef ArchitecturesAllowed
  #error ArchitecturesAllowed is not defined.
#endif
#ifndef ArchitecturesInstallIn64BitMode
  #error ArchitecturesInstallIn64BitMode is not defined.
#endif
#ifndef RuntimeArch
  #error RuntimeArch is not defined.
#endif
#ifndef RuntimeUrl
  #error RuntimeUrl is not defined.
#endif
#ifndef RuntimeFileName
  #error RuntimeFileName is not defined.
#endif
#ifndef RuntimePageUrl
  #error RuntimePageUrl is not defined.
#endif

[Setup]
AppId={{2016DA44-0694-42F7-92A3-4015CF9069A5}
AppName=Simple Launcher
AppVersion={#AppVersion}
AppVerName=Simple Launcher {#AppVersion}
AppPublisher=PureLogicCode
AppPublisherURL=https://www.purelogiccode.com/
AppSupportURL=https://github.com/purelogiccode/SimpleLauncher/issues
AppUpdatesURL=https://github.com/purelogiccode/SimpleLauncher/releases
AppComments=Open-source emulator frontend for Windows, Linux and macOS.
VersionInfoCompany=PureLogicCode
VersionInfoDescription=Simple Launcher installer
VersionInfoProductName=Simple Launcher
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}.0
VersionInfoCopyright=Copyright (C) PureLogicCode

; Per-user install: %LocalAppData%\SimpleLauncher. The app treats a writable
; install folder as its portable data folder, so settings.dat, system.xml and
; logs live here too. The uninstaller removes the program files it installed
; and leaves those user files in place.
DefaultDirName={localappdata}\SimpleLauncher
DefaultGroupName=Simple Launcher
DisableProgramGroupPage=yes
AllowNoIcons=yes
UsePreviousAppDir=yes

PrivilegesRequired=lowest
ArchitecturesAllowed={#ArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#ArchitecturesInstallIn64BitMode}
MinVersion=10.0.17763

OutputDir={#OutputDir}
OutputBaseFilename=SimpleLauncher-{#AppVersion}-{#Rid}-setup
SetupIconFile={#RepoDir}\SimpleLauncher.Avalonia\icon\icon.ico
UninstallDisplayIcon={app}\SimpleLauncher.exe
UninstallDisplayName=Simple Launcher
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes

; The Avalonia app keeps the historical single-instance mutex shared with legacy
; WPF installs. Setup asks the user to close a running instance before replacing
; files, and does not relaunch it afterwards (the built-in updater handles normal
; in-app updates).
AppMutex=SimpleLauncher_SingleInstanceMutex_A8E2B9C1-F5D7-4E0A-8B3C-6D1E9F0A7B4C
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Runtime data (settings.dat, system.xml, logs, the single-instance lock) is
; intentionally excluded so a dirty publish folder can never ship user data.
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "settings.dat,settings.xml,system.xml,favorites.dat,playhistory.dat,*.log,single-instance.lock,*.tmp"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Upgrades from the former side-by-side bundle: remove the old Avalonia binary and
; its shortcut so users cannot launch the stale build after installing the rename.
Type: files; Name: "{app}\SimpleLauncher.Avalonia.exe"
Type: files; Name: "{group}\Simple Launcher (Avalonia UI).lnk"
Type: files; Name: "{autodesktop}\Simple Launcher (Avalonia UI).lnk"

[Icons]
Name: "{group}\Simple Launcher"; Filename: "{app}\SimpleLauncher.exe"; WorkingDir: "{app}"; Comment: "Simple Launcher"
Name: "{group}\{cm:UninstallProgram,Simple Launcher}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Simple Launcher"; Filename: "{app}\SimpleLauncher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SimpleLauncher.exe"; Description: "{cm:LaunchProgram,Simple Launcher}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DownloadPage: TDownloadWizardPage;
  RuntimeNeedsRestart: Boolean;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    'Downloading the .NET 11 runtime',
    'Simple Launcher needs Microsoft''s .NET 11 runtime. Setup will download it now.',
    nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;

function HasDotNetRuntime11In(const Directory: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if not DirExists(Directory) then
    Exit;

  if not FindFirst(AddBackslash(Directory) + '11.*', FindRec) then
    Exit;

  try
    repeat
      if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
         (Copy(FindRec.Name, 1, 3) = '11.') then
      begin
        Result := True;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function DotNetRuntime11Location(): String;
var
  Root: String;
begin
  Result := '';

  Root := GetEnv('DOTNET_ROOT');
  if (Root <> '') and HasDotNetRuntime11In(AddBackslash(Root) + 'shared\Microsoft.NETCore.App') then
  begin
    Result := AddBackslash(Root) + 'shared\Microsoft.NETCore.App';
    Exit;
  end;

  if HasDotNetRuntime11In(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.NETCore.App')) then
  begin
    Result := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.NETCore.App');
    Exit;
  end;

  if HasDotNetRuntime11In(ExpandConstant('{commonpf}\dotnet\{#RuntimeArch}\shared\Microsoft.NETCore.App')) then
  begin
    Result := ExpandConstant('{commonpf}\dotnet\{#RuntimeArch}\shared\Microsoft.NETCore.App');
    Exit;
  end;

  if HasDotNetRuntime11In(ExpandConstant('{localappdata}\Microsoft\dotnet\shared\Microsoft.NETCore.App')) then
  begin
    Result := ExpandConstant('{localappdata}\Microsoft\dotnet\shared\Microsoft.NETCore.App');
    Exit;
  end;
end;

function DotNetRuntime11Installed(): Boolean;
begin
  Result := DotNetRuntime11Location() <> '';
end;

{ An offline copy placed next to Setup wins over a download. The official
  Microsoft installer is named dotnet-runtime-11.0.<patch>-win-<arch>.exe,
  so accept both the short and the patch-versioned name. }
function FindOfflineRuntimeInstaller(): String;
var
  FindRec: TFindRec;
begin
  Result := ExpandConstant('{src}\{#RuntimeFileName}');
  if FileExists(Result) then
    Exit;

  Result := '';
  if FindFirst(ExpandConstant('{src}\dotnet-runtime-11.0*-win-{#RuntimeArch}.exe'), FindRec) then
  try
    if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) = 0 then
      Result := ExpandConstant('{src}\') + FindRec.Name;
  finally
    FindClose(FindRec);
  end;
end;

function InstallDotNetRuntime(): String;
var
  RuntimePath: String;
  ResultCode: Integer;
  DownloadError: String;
begin
  Result := '';
  RuntimeNeedsRestart := False;
  DownloadError := '';

  RuntimePath := FindOfflineRuntimeInstaller();
  if RuntimePath = '' then
  begin
    RuntimePath := ExpandConstant('{tmp}\{#RuntimeFileName}');
    try
      if WizardSilent then
      begin
        DownloadTemporaryFile('{#RuntimeUrl}', '{#RuntimeFileName}', '', nil);
      end
      else
      begin
        DownloadPage.Clear;
        DownloadPage.Add('{#RuntimeUrl}', '{#RuntimeFileName}', '');
        DownloadPage.Show;
        try
          try
            DownloadPage.Download;
          except
            if DownloadPage.AbortedByUser then
              DownloadError := 'The .NET 11 runtime download was cancelled.'
            else
              DownloadError := 'Could not download the .NET 11 runtime: ' + GetExceptionMessage;
          end;
        finally
          DownloadPage.Hide;
        end;

        if DownloadError <> '' then
        begin
          Result := DownloadError + #13#10#13#10 +
            'Install the runtime manually from {#RuntimePageUrl} and run Setup again.';
          Exit;
        end;
      end;
    except
      Result := 'Could not download the .NET 11 runtime: ' + GetExceptionMessage + #13#10#13#10 +
        'Install the runtime manually from {#RuntimePageUrl} and run Setup again.';
      Exit;
    end;
  end;

  if not ShellExec('runas', RuntimePath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'The .NET 11 runtime installer could not be started. ' +
      'Approve the administrator prompt, or install the runtime manually from {#RuntimePageUrl}.';
    Exit;
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    Result := Format('The .NET 11 runtime installer failed (exit code %d). ' +
      'Install the runtime manually from {#RuntimePageUrl} and run Setup again.', [ResultCode]);
    Exit;
  end;

  if ResultCode = 3010 then
    RuntimeNeedsRestart := True;

  if not DotNetRuntime11Installed() then
  begin
    Result := 'The .NET 11 runtime is still not available after installation. ' +
      'Install it manually from {#RuntimePageUrl} and run Setup again.';
    Exit;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Location: String;
  Answer: Integer;
  ErrorCode: Integer;
begin
  Result := '';
  NeedsRestart := False;

  Location := DotNetRuntime11Location();
  if Location <> '' then
  begin
    Log('Simple Launcher: .NET 11 runtime found at ' + Location);
    Exit;
  end;

  Log('Simple Launcher: .NET 11 runtime was not found.');

  if not WizardSilent then
  begin
    Answer := MsgBox(
      'Simple Launcher needs the .NET 11 runtime.' + #13#10#13#10 +
      'Setup can download and install it now. Windows will ask for administrator approval for this step only.' + #13#10#13#10 +
      'Choose No to open the Microsoft download page and install it yourself.',
      mbConfirmation, MB_YESNOCANCEL);

    if Answer = IDCANCEL then
    begin
      Result := 'Setup was cancelled. The .NET 11 runtime is required to run Simple Launcher.';
      Exit;
    end;

    if Answer = IDNO then
    begin
      ShellExec('open', '{#RuntimePageUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      Result := 'Install the .NET 11 runtime from the page that just opened, then run Setup again.';
      Exit;
    end;
  end;

  Result := InstallDotNetRuntime();
  if Result = '' then
  begin
    Log('Simple Launcher: .NET 11 runtime installed successfully.');
    NeedsRestart := RuntimeNeedsRestart;
  end;
end;
