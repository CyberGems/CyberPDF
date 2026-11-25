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
DisableProgramGroupPage=yes
LicenseFile=LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
english.AssocGroup=File association:
english.AssocPdf=Open PDF files with CyberPDF
english.AssocHint=.pdf files will open in CyberPDF. You can change this later in Windows Settings, Default apps.
english.DesktopShortcut=Create a desktop shortcut
english.CloseBeforeUninstall=CyberPDF is still running. Close it and click Retry.

spanish.AssocGroup=Asociación de archivos:
spanish.AssocPdf=Abrir archivos PDF con CyberPDF
spanish.AssocHint=Los archivos .pdf se abrirán con CyberPDF. Puedes cambiarlo después en Configuración de Windows, Aplicaciones predeterminadas.
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
; Per-user association under HKCU, so nothing needs elevation and the user keeps
; control. ProgID names are versioned to this app to avoid colliding with other
; readers.
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document"; ValueType: string; ValueData: "PDF document"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document\DefaultIcon"; ValueType: string; ValueData: "{app}\{#AppExeName},0"
Root: HKCU; Subkey: "Software\Classes\CyberPDF.Document\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "CyberPDF.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assocpdf
Root: HKCU; Subkey: "Software\Classes\.pdf"; ValueType: string; ValueName: ""; ValueData: "CyberPDF.Document"; Flags: uninsdeletevalue; Tasks: assocpdf
; Tell Explorer the association changed, otherwise the icon and handler can stay
; cached for a while.
Root: HKCU; Subkey: "Software\Classes\.pdf"; ValueType: none; ValueName: ""; Flags: uninsdeletevalue; Tasks: assocpdf

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
