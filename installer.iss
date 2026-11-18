; Inno Setup script for the Modeled Threads add-in.
;
; Builds a single installer that puts the bundle where Inventor looks for it.
; Compile with: iscc installer.iss
;
; The install is per user and needs no administrator rights, which is also the
; simplest way past Windows' add-in prompts: Inventor only ever looks in the
; user's own profile unless the machine is set up for all users.

#define AppName "Modeled Threads"
#ifndef AppVersion
  ; The release passes -DAppVersion=<version>; a plain `iscc installer.iss`
  ; falls back to this.
  #define AppVersion "1.0.0"
#endif
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
LicenseFile=LICENSE
ChangesEnvironment=no
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bundle\ModeledThreads.bundle\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{app}\README.md"; Description: "Read the notes"; Flags: postinstall shellexec skipifsilent unchecked

[Code]
{ Inno Setup 6 has no process-list function, so ask tasklist and read what it
  says. The image name is not localised, so this works on any Windows. }
function InventorIsRunning(): Boolean;
var
  Output: string;
  Lines: TArrayOfString;
  I: Integer;
  Code: Integer;
begin
  Result := False;
  Output := ExpandConstant('{tmp}\inventor-running.txt');

  if not Exec(ExpandConstant('{cmd}'),
      '/c tasklist /FI "IMAGENAME eq Inventor.exe" /NH > "' + Output + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Exit;

  if LoadStringsFromFile(Output, Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
      if Pos('Inventor.exe', Lines[I]) > 0 then
        Result := True;

  DeleteFile(Output);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if InventorIsRunning() then
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
