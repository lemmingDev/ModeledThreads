# Part 1 of the dialog check: selects a cylindrical face and presses Create Thread.
# Execute() does not return until the dialog closes, so this script blocks and is
# started in the background; ui-read-dialog.ps1 is what closes the dialog.
#
# Run in session 1:
#   powershell -ExecutionPolicy Bypass -File ui-open-dialog.ps1 <pid> <part.ipt>

param(
    [Parameter(Mandatory = $true)][int] $InventorPid,
    [Parameter(Mandatory = $true)][string] $PartPath
)

$out = 'C:\Temp\ui-open.txt'
"starting" | Out-File $out -Encoding utf8
function Note([string] $text) { "$(Get-Date -Format HH:mm:ss) $text" | Out-File $out -Append -Encoding utf8 }

$interop = 'C:\Temp\doctor\Autodesk.Inventor.Interop.dll'
[AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $event)
    $simple = (New-Object Reflection.AssemblyName($event.Name)).Name
    if ($simple -eq 'Autodesk.Inventor.Interop') {
        return [Reflection.Assembly]::LoadFrom($interop)
    }

    return $null
})

# The first registered Inventor may be the user's own, so find the one whose
# moniker ends in the pid we were given.
Add-Type -ReferencedAssemblies $interop -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Inventor;

public static class RotByPid
{
    public static string Seen = "";

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    public static Inventor.Application Find(int pid)
    {
        IRunningObjectTable table;        if (GetRunningObjectTable(0, out table) != 0)
        {
            return null;
        }

        IEnumMoniker monikers;
        table.EnumRunning(out monikers);

        var one = new IMoniker[1];
        IntPtr fetched = IntPtr.Zero;

        while (monikers.Next(1, one, fetched) == 0)
        {
            try
            {
                IBindCtx context;
                CreateBindCtx(0, out context);

                string name;
                one[0].GetDisplayName(context, null, out name);
                Seen += name + ";";

                if (name != null && name.EndsWith("-" + pid, StringComparison.Ordinal))
                {
                    object found;
                    table.GetObject(one[0], out found);

                    Inventor.Application app = found as Inventor.Application;
                    if (app != null)
                    {
                        return app;
                    }

                    // An instance with a document open may be registered through
                    // the document rather than the application.
                    Document document = found as Document;
                    if (document != null)
                    {
                        Inventor.Application owner = document.Parent as Inventor.Application;
                        if (owner != null)
                        {
                            return owner;
                        }
                    }
                }
            }
            catch
            {
                // A moniker that is not an Inventor is not interesting.
            }
        }

        return null;
    }
}
"@

$inv = [RotByPid]::Find($InventorPid)
if ($null -eq $inv) {
    Note "no Inventor instance with pid $InventorPid"
    Note ("monikers seen: " + [RotByPid]::Seen)
    exit 1
}

Note "attached to $($inv.Caption)"

$doc = $null
foreach ($open in $inv.Documents) {
    if ($open.FullFileName -eq $PartPath) { $doc = $open }
}

if ($null -eq $doc) {
    Note "opening $PartPath"
    $doc = $inv.Documents.Open($PartPath, $true)
}

$doc.Activate()
Start-Sleep -Seconds 1

if ($doc.FullFileName -ne $PartPath) {
    Note "refusing to touch $($doc.FullFileName)"
    exit 1
}

# The first cylindrical face is enough: any of them makes the dialog pick a size.
$face = $null
foreach ($body in $doc.ComponentDefinition.SurfaceBodies) {
    foreach ($candidate in $body.Faces) {
        if ($candidate.SurfaceType -ne 5891) { continue }   # kCylinderSurface
        $face = $candidate
        break
    }

    if ($null -ne $face) { break }
}

if ($null -eq $face) {
    Note "no cylindrical face in $PartPath"
    exit 1
}

Note ("face: r {0} mm" -f ($face.Geometry.Radius * 10))

$doc.SelectSet.Clear()
$doc.SelectSet.Select($face)

Note "pressing Create Thread; this returns when the dialog closes"
$inv.CommandManager.ControlDefinitions.Item('ModeledThreads.Create').Execute()
Note "the dialog has closed"

# The dialog was cancelled, so nothing was cut: close the part without saving.
$doc.Close($true)
Note "closed the part without saving"
