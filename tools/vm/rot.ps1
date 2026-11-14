# Lists every moniker in the running object table and what object it holds:
# an Inventor application or one of its documents. The per-process names carry
# the process id, so the instance with the user's part can be told apart from a
# second, empty one.
$log = 'C:\Temp\rot.txt'
"starting" | Out-File $log -Encoding utf8

try {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class Rot
{
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    public static string[] Describe()
    {
        IRunningObjectTable table;
        int hr = GetRunningObjectTable(0, out table);
        if (hr != 0) { throw new COMException("GetRunningObjectTable failed", hr); }

        IEnumMoniker enumMoniker;
        table.EnumRunning(out enumMoniker);

        var lines = new System.Collections.Generic.List<string>();
        var monikers = new IMoniker[1];
        IntPtr fetched = IntPtr.Zero;

        while (enumMoniker.Next(1, monikers, fetched) == 0)
        {
            IMoniker moniker = monikers[0];
            string name = null;

            try
            {
                IBindCtx context;
                CreateBindCtx(0, out context);
                moniker.GetDisplayName(context, null, out name);
            }
            catch { }

            string description = "(no object)";
            try
            {
                object held;
                table.GetObject(moniker, out held);
                description = held.GetType().FullName;
                try
                {
                    object display = held.GetType().InvokeMember(
                        "DisplayName", System.Reflection.BindingFlags.GetProperty, null, held, null);
                    description += "  '" + display + "'";
                }
                catch { }
            }
            catch (Exception ex) { description = "(failed: " + ex.Message + ")"; }

            lines.Add(name + "  ->  " + description);
            Marshal.ReleaseComObject(moniker);
        }

        Marshal.ReleaseComObject(enumMoniker);
        Marshal.ReleaseComObject(table);
        return lines.ToArray();
    }
}
'@

    [Rot]::Describe() | Out-File $log -Append -Encoding utf8
}
catch {
    ("failed: " + $_.Exception.ToString()) | Out-File $log -Append -Encoding utf8
}

"done" | Out-File $log -Append -Encoding utf8
exit 0
