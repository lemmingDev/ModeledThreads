; Inno Setup script for the Modeled Threads add-in.
;
; Builds a single installer that puts the bundle where Inventor looks for it.
; Compile with: iscc installer.iss
;
; The install is per user and needs no administrator rights, which is also the
; simplest way past Windows' add-in prompts: Inventor only ever looks in the
; user's own profile unless the machine is set up for all users.

#define AppName "Modeled Threads"
#define AppVersion "1.0.0"
#define AppPublisher "Modeled Threads"

[Setup]
AppId={{3F5B1C2A-6D47-4A91-9D2E-8B7C5F1A4E30}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\ModeledThreads.bundle
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputBaseFilename=ModeledThreads-Setup
OutputDir=dist
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayName={#AppName}
ChangesEnvironment=no
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bundle\ModeledThreads.bundle\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{app}\README.md"; Description: "Read the notes"; Flags: postinstall shellexec skipifsilent unchecked

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if GetProcessIDs('Inventor.exe', ProcIds) <> 0 then
  begin
    MsgBox('Close Autodesk Inventor before installing.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Bundle: string;
begin
  { Inventor sees the bundle folder itself, so nothing of ours may be left in it
    once the user uninstalls. }
  if CurUninstallStep = usPostUninstall then
  begin
    Bundle := ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\ModeledThreads.bundle');
    if DirExists(Bundle) then
      RemoveDir(Bundle, True);
  end;
end;
