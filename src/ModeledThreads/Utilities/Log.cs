using System.Text;

namespace ModeledThreads.Utilities;

/// <summary>
/// A small file log next to the add-in, so a thread that goes wrong can be
/// traced after the fact.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _path;

    public static string LogFilePath => _path ??= Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ModeledThreads",
        "modeled-threads.log");

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message) => Write("ERROR", message);

    public static void Error(string message, Exception ex) =>
        Write("ERROR", $"{message} :: {DescribeException(ex)}");

    /// <summary>A readable description of an exception, with the Inventor error text.</summary>
    public static string DescribeException(Exception ex)
    {
        var sb = new StringBuilder();
        sb.Append(ex.GetType().Name).Append(": ").Append(ex.Message);

        if (ex is System.Runtime.InteropServices.COMException com)
        {
            sb.Append(" (HRESULT 0x").Append(com.HResult.ToString("X8")).Append(')');
        }

        if (ex.InnerException is not null)
        {
            sb.Append(" -> ").Append(DescribeException(ex.InnerException));
        }

        return sb.ToString();
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                string path = LogFilePath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(
                    path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // A log that cannot be written must never stop the add-in.
        }
    }
}
