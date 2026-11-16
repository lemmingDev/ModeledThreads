using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace SessionRunner;

/// <summary>
/// Runs a command in the interactive user's session.
///
/// A service, or a process started by `prlctl exec`, lives in session 0 and
/// nothing it starts can be seen or clicked on a user's desktop. Windows'
/// Task Scheduler is the usual way round that; when the scheduler itself is
/// stuck (launch requests queue and never start), this takes the user's own
/// token from the console session and starts the command with it instead.
/// </summary>
internal static class Program
{
    private const uint TOKEN_ALL_ACCESS = 0xF01FF;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NEW_CONSOLE = 0x00000010;
    private const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        int impersonationLevel,
        TOKEN_TYPE tokenType,
        out IntPtr newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr token,
        string? applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr token);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out long luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr token,
        bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState,
        int bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;
        public long Luid;
        public int Attributes;
    }

    private const int SE_PRIVILEGE_ENABLED = 2;
    private const string SE_TCB_NAME = "SeTcbPrivilege";

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: SessionRunner [--wait] <command line>");
            return 2;
        }

        bool wait = args[0] == "--wait";

        // The command line is handed to CreateProcess whole, so a path with a
        // space in it has to keep its quotes.
        string commandLine = string.Join(
            " ",
            (wait ? args.Skip(1) : args).Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        uint sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            Console.WriteLine("There is no console session to start the command in.");
            return 1;
        }

        EnablePrivilege(SE_TCB_NAME);

        if (!WTSQueryUserToken(sessionId, out IntPtr userToken))
        {
            Fail($"The console session's token could not be read (session {sessionId})");
        }

        if (!DuplicateTokenEx(
                userToken,
                TOKEN_ALL_ACCESS,
                IntPtr.Zero,
                SecurityImpersonation,
                (TOKEN_TYPE)TokenPrimary,
                out IntPtr primaryToken))
        {
            Fail("The console session's token could not be duplicated");
        }

        IntPtr environment = IntPtr.Zero;
        bool hasEnvironment = CreateEnvironmentBlock(out environment, primaryToken, false);

        var startup = new STARTUPINFO();
        startup.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        startup.lpDesktop = null;

        // `prlctl exec` puts what it starts in a job that is killed when the
        // command returns, and a child left in that job would die at once. The
        // breakaway flag takes the new process out of it; a job that forbids
        // breakaway is rare, and then the plain call is tried instead.
        bool started = CreateProcessAsUser(
            primaryToken,
            null,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            CREATE_UNICODE_ENVIRONMENT | CREATE_BREAKAWAY_FROM_JOB,
            hasEnvironment ? environment : IntPtr.Zero,
            null,
            ref startup,
            out PROCESS_INFORMATION process);

        if (!started)
        {
            started = CreateProcessAsUser(
                primaryToken,
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CREATE_UNICODE_ENVIRONMENT,
                hasEnvironment ? environment : IntPtr.Zero,
                null,
                ref startup,
                out process);
        }

        if (!started)
        {
            Fail($"The command could not be started in session {sessionId}");
        }

        if (hasEnvironment)
        {
            DestroyEnvironmentBlock(environment);
        }

        Console.WriteLine($"Started in session {sessionId}: pid {process.dwProcessId} {commandLine}");

        // --wait keeps this process alive until the command finishes, which is
        // also what keeps the command from being swept away with it.
        if (wait)
        {
            WaitForSingleObject(process.hProcess, 0xFFFFFFFF);
            GetExitCodeProcess(process.hProcess, out uint code);
            Console.WriteLine($"Exit code 0x{code:X8} ({code}).");
            return code == 0 ? 0 : 1;
        }

        return 0;
    }

    private static void EnablePrivilege(string name)
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out IntPtr token))
        {
            return;
        }

        if (!LookupPrivilegeValue(null, name, out long luid))
        {
            return;
        }

        var privileges = new TOKEN_PRIVILEGES
        {
            PrivilegeCount = 1,
            Luid = luid,
            Attributes = SE_PRIVILEGE_ENABLED
        };

        AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static void Fail(string message)
    {
        Console.WriteLine($"{message}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        Environment.Exit(1);
    }
}
