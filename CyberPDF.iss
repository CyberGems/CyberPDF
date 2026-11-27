; Inno Setup script for CyberPDF.
; The version comes from CI via /DAppVersion from the git tag; never hardcode it.
; Build locally with:
;   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=0.1.0 CyberPDF.iss

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "CyberPDF"
#define AppPublisher "CyberGems"
#define AppURL "https://cybergems.org"
#define AppDescription "A fast, lightweight PDF reader for Windows."
#define AppExeName "CyberPDF.exe"
#define AppId "{{7B2F4A61-9C3E-4D58-8F1A-2E6D5C4B9A70}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
AppComments={#AppDescription}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=CyberPDF-Setup-{#AppVersion}
SetupIconFile=src\CyberPDF.UI\Assets\CyberPDF.ico
UninstallDisplayIcon={app}\{#AppExeName},0
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Per-user install: no administrator prompt for a document reader.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
DisableProgramGroupPage=yes
LicenseFile=LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
english.AssocGroup=Default reader and file association:
english.AssocPdf=Set CyberPDF as the default PDF reader
english.AssocHint=.pdf files will be registered to CyberPDF. If another reader is currently pinned, Windows Default Apps opens at the end so you can confirm in one click.
english.DesktopShortcut=Create a desktop shortcut
english.CloseBeforeUninstall=CyberPDF is still running. Close it and click Retry.

spanish.AssocGroup=Lector predeterminado y asociación de archivos:
spanish.AssocPdf=Establecer CyberPDF como lector de PDF predeterminado
spanish.AssocHint=Los archivos .pdf se asociarán a CyberPDF. Si Windows tiene fijado otro lector, se abrirá Aplicaciones predeterminadas al finalizar para confirmarlo en un clic.
spanish.DesktopShortcut=Crear un acceso directo en el escritorio
spanish.CloseBeforeUninstall=CyberPDF sigue abierto. Ciérralo y haz clic en Reintentar.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "assocpdf"; Description: "{cm:AssocPdf}"; GroupDescription: "{cm:AssocGroup}"

[Files]
; The publish folder carries the self-contained runtime plus pdfium.dll and its
; licenses. The installer compresses it, so the downloaded size is far smaller
; than the ~117 MB on disk.
Source: "publish-win64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Per-user registration under HKCU: no administrator prompt required and the
; user retains control in Windows Default Apps.
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document"; ValueType: string; ValueData: "PDF document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document"; ValueType: string; ValueName: "FriendlyTypeName"; ValueData: "PDF document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document\Application"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document\Application"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{#AppDescription}"
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\Applications\{#AppExeName}\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "CyberPDF.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assocpdf
Root: HKCU; Subkey: "Software\Classes\.pdf"; ValueType: string; ValueName: ""; ValueData: "CyberPDF.Document"; Flags: uninsdeletevalue; Tasks: assocpdf
Root: HKCU; Subkey: "Software\CyberGems\CyberPDF\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\CyberGems\CyberPDF\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{#AppDescription}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\CyberGems\CyberPDF\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pdf"; ValueData: "CyberPDF.Document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "Software\CyberGems\CyberPDF\Capabilities"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\licenses"

[Code]
// Remove the file association only if it still points at this install, so
// uninstalling never steals the .pdf handler from another reader the user chose.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current: String;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  if RegQueryStringValue(HKCU, 'Software\Classes\.pdf', '', Current) and
     (CompareText(Current, 'CyberPDF.Document') = 0) then
  begin
    RegDeleteValue(HKCU, 'Software\Classes\.pdf', '');
    Log('Removed the .pdf association because it pointed at CyberPDF.');
  end;

  // Make sure no running copy holds the files being removed.
  while CheckForMutexes('CyberPDF_SingleInstance') do
  begin
    if UninstallSilent then
      Abort;
    if MsgBox(CustomMessage('CloseBeforeUninstall'), mbInformation, MB_RETRYCANCEL) <> IDRETRY then
      Abort;
  end;
end;

// Show the association note up front, so choosing the task is informed.
procedure InitializeWizard;
begin
  WizardForm.TasksList.Hint := CustomMessage('AssocHint');
end;

// If Windows already locks .pdf to another reader via UserChoice, open Windows
// Default Apps directly after install so the user can confirm in one click.
procedure CurStepChanged(CurStep: TSetupStep);
var
  CurrentProgId: String;
  ErrorCode: Integer;
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('assocpdf') and (not WizardSilent) then
  begin
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice', 'ProgId', CurrentProgId) then
    begin
      if (CurrentProgId <> '') and (CompareText(CurrentProgId, 'CyberPDF.Document') <> 0) then
      begin
        if not ShellExec('open', 'ms-settings:defaultapps?registeredAppUser=CyberPDF', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode) then
          ShellExec('open', 'ms-settings:defaultapps', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      end;
    end;
  end;
end;
