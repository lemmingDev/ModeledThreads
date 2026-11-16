# Reports the ribbon's display state and what the Modeled Threads panel holds.
# Read-only: it only reads properties.
#
# Run in session 1:
#   powershell -ExecutionPolicy Bypass -File probe-ribbon.ps1 <pid>

param([Parameter(Mandatory = $true)][int] $InventorPid)

$out = 'C:\Temp\probe-ribbon.txt'
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

Add-Type -ReferencedAssemblies $interop -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Inventor;

public static class RotFind
{
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    public static Inventor.Application Find(int pid)
    {
        IRunningObjectTable table;
        if (GetRunningObjectTable(0, out table) != 0) { return null; }

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

                if (name != null && name.EndsWith("-" + pid, StringComparison.Ordinal))
                {
                    object found;
                    table.GetObject(one[0], out found);

                    Inventor.Application app = found as Inventor.Application;
                    if (app != null) { return app; }

                    Document document = found as Document;
                    if (document != null)
                    {
                        Inventor.Application owner = document.Parent as Inventor.Application;
                        if (owner != null) { return owner; }
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
"@

$inv = [RotFind]::Find($InventorPid)
if ($null -eq $inv) {
    Note "no Inventor instance with pid $InventorPid"
    exit 1
}

$ui = $inv.UserInterfaceManager
Note ("RibbonState: " + $ui.RibbonState)
Note ("ShowPanelTitles: " + $ui.ShowPanelTitles)
Note ("RibbonAppearance: " + $ui.RibbonAppearance)
Note ("RibbonDockingState: " + $ui.RibbonDockingState)
Note ("LargeIcons: " + $ui.LargeIcons)
Note ("InterfaceStyle: " + $ui.InterfaceStyle)
Note ("active environment: " + $ui.ActiveEnvironment.InternalName)

foreach ($ribbon in $ui.Ribbons) {
    foreach ($tab in $ribbon.RibbonTabs) {
        if ($tab.InternalName -ne 'ModeledThreads.Tab') { continue }

        Note ("tab '{0}' on ribbon '{1}' active={2} visible={3}" -f
            $tab.DisplayName, $ribbon.InternalName, $tab.Active, $tab.Visible)

        foreach ($panel in $tab.RibbonPanels) {
            Note ("  panel '{0}' docked={1} visible={2}" -f
                $panel.DisplayName, $panel.Docked, $panel.Visible)

            foreach ($control in $panel.CommandControls) {
                $text = ''
                if ($null -ne $control.ControlDefinition) { $text = $control.ControlDefinition.DisplayName }
                Note ("    control '{0}' text='{1}' showText={2} largeIcon={3} visible={4}" -f
                    $control.InternalName, $text, $control.ShowText, $control.UseLargeIcon, $control.Visible)
            }
        }
    }
}
