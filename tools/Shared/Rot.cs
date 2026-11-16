using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Inventor;

/// <summary>The running object table, for reaching one particular Inventor.</summary>
internal static class Rot
{
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    public static Inventor.Application? FindApplication(int pid)
    {
        if (GetRunningObjectTable(0, out IRunningObjectTable table) != 0)
        {
            return null;
        }

        table.EnumRunning(out IEnumMoniker enumMoniker);

        var monikers = new IMoniker[1];
        IntPtr fetched = IntPtr.Zero;

        while (enumMoniker.Next(1, monikers, fetched) == 0)
        {
            IMoniker moniker = monikers[0];

            try
            {
                CreateBindCtx(0, out IBindCtx context);
                moniker.GetDisplayName(context, null, out string name);

                if (name is null || !name.EndsWith("-" + pid, StringComparison.Ordinal))
                {
                    continue;
                }

                table.GetObject(moniker, out object held);

                if (held is Inventor.Application application)
                {
                    return application;
                }

                if (held is Document document && document.Parent is Inventor.Application owner)
                {
                    return owner;
                }

                Console.WriteLine($"  pid {pid}: the object is {held.GetType().FullName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  pid {pid}: {ex.Message}");
            }
            finally
            {
                Marshal.ReleaseComObject(moniker);
            }
        }

        return null;
    }
}
