using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VideoGrabber.Core.Processes;

namespace VideoGrabber.Infrastructure.Processes;

internal static class WindowsSuspendedProcessLauncher
{
    public static INativeChildProcessHandle Start(ProcessSpec spec)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        ArgumentNullException.ThrowIfNull(spec);

        nint stdoutRead = 0, stdoutWrite = 0, stderrRead = 0, stderrWrite = 0, stdin = 0;
        nint attributeList = 0, handleList = 0;
        PROCESS_INFORMATION processInfo = default;
        WindowsProcessJob? job = null;
        var processCreated = false;
        try
        {
            CreateInheritedPipe(out stdoutRead, out stdoutWrite);
            CreateInheritedPipe(out stderrRead, out stderrWrite);
            stdin = OpenInheritedNullInput();
            attributeList = CreateHandleAttributeList([stdin, stdoutWrite, stderrWrite], out handleList);

            var startup = new STARTUPINFOEX();
            startup.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
            startup.StartupInfo.hStdInput = stdin;
            startup.StartupInfo.hStdOutput = stdoutWrite;
            startup.StartupInfo.hStdError = stderrWrite;
            startup.lpAttributeList = attributeList;

            var commandLine = new StringBuilder(BuildCommandLine(spec));
            var workingDirectory = spec.WorkingDirectory ?? Environment.CurrentDirectory;
            var flags = CREATE_SUSPENDED | CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT;
            if (!CreateProcessW(null, commandLine, 0, 0, true, flags, 0, workingDirectory, ref startup, out processInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            processCreated = true;

            Close(ref stdoutWrite);
            Close(ref stderrWrite);
            Close(ref stdin);

            job = WindowsProcessJob.Create();
            job.Assign(processInfo.hProcess);
            if (ResumeThread(processInfo.hThread) == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Close(ref processInfo.hThread);

            var result = new NativeChildProcessHandle(processInfo.dwProcessId, processInfo.hProcess,
                job, stdoutRead, stderrRead);
            processInfo.hProcess = 0;
            stdoutRead = 0;
            stderrRead = 0;
            job = null;
            return result;
        }
        catch
        {
            if (job is not null)
            {
                try { job.Terminate(); } catch { }
                job.Dispose();
            }
            else if (processCreated && processInfo.hProcess != 0)
            {
                try { TerminateProcess(processInfo.hProcess, 1); } catch { }
            }
            throw;
        }
        finally
        {
            Close(ref processInfo.hThread);
            Close(ref processInfo.hProcess);
            Close(ref stdoutRead);
            Close(ref stdoutWrite);
            Close(ref stderrRead);
            Close(ref stderrWrite);
            Close(ref stdin);
            if (attributeList != 0) DeleteProcThreadAttributeList(attributeList);
            if (handleList != 0) Marshal.FreeHGlobal(handleList);
            if (attributeList != 0) Marshal.FreeHGlobal(attributeList);
        }
    }

    private static void CreateInheritedPipe(out nint read, out nint write)
    {
        var security = InheritableSecurityAttributes();
        if (!CreatePipe(out read, out write, ref security, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!SetHandleInformation(read, HANDLE_FLAG_INHERIT, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static nint OpenInheritedNullInput()
    {
        var security = InheritableSecurityAttributes();
        var handle = CreateFileW("NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
            ref security, OPEN_EXISTING, 0, 0);
        if (handle == INVALID_HANDLE_VALUE) throw new Win32Exception(Marshal.GetLastWin32Error());
        return handle;
    }

    private static nint CreateHandleAttributeList(nint[] handles, out nint handleList)
    {
        nuint size = 0;
        _ = InitializeProcThreadAttributeList(0, 1, 0, ref size);
        var list = Marshal.AllocHGlobal(checked((int)size));
        if (!InitializeProcThreadAttributeList(list, 1, 0, ref size))
        {
            var error = Marshal.GetLastWin32Error();
            Marshal.FreeHGlobal(list);
            throw new Win32Exception(error);
        }
        handleList = Marshal.AllocHGlobal(IntPtr.Size * handles.Length);
        for (var i = 0; i < handles.Length; i++) Marshal.WriteIntPtr(handleList, i * IntPtr.Size, handles[i]);
        if (!UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                handleList, (nuint)(IntPtr.Size * handles.Length), 0, 0))
        {
            var error = Marshal.GetLastWin32Error();
            DeleteProcThreadAttributeList(list);
            Marshal.FreeHGlobal(list);
            Marshal.FreeHGlobal(handleList);
            handleList = 0;
            throw new Win32Exception(error);
        }
        return list;
    }

    private static string BuildCommandLine(ProcessSpec spec)
    {
        var parts = new string[spec.Arguments.Count + 1];
        parts[0] = QuoteWindowsArgument(spec.FileName);
        for (var i = 0; i < spec.Arguments.Count; i++) parts[i + 1] = QuoteWindowsArgument(spec.Arguments[i]);
        return string.Join(' ', parts);
    }

    internal static string QuoteWindowsArgument(string value)
    {
        if (value.Length == 0) return "\"\"";
        if (value.IndexOfAny([' ', '\t', '\"']) < 0) return value;
        var builder = new StringBuilder(value.Length + 2).Append('\"');
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            if (character == '\"')
            {
                builder.Append('\\', slashes * 2 + 1).Append('\"');
                slashes = 0;
                continue;
            }
            builder.Append('\\', slashes).Append(character);
            slashes = 0;
        }
        builder.Append('\\', slashes * 2).Append('\"');
        return builder.ToString();
    }

    private static SECURITY_ATTRIBUTES InheritableSecurityAttributes() => new()
    {
        nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), bInheritHandle = true
    };

    private static void Close(ref nint handle)
    {
        if (handle is 0 or -1) { handle = 0; return; }
        CloseHandle(handle);
        handle = 0;
    }

    private const uint CREATE_SUSPENDED = 0x00000004;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint STARTF_USESTDHANDLES = 0x00000100;
    private const uint HANDLE_FLAG_INHERIT = 0x00000001;
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const nuint PROC_THREAD_ATTRIBUTE_HANDLE_LIST = 0x00020002;
    private static readonly nint INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public nint lpSecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)] public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public nint lpReserved, lpDesktop, lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        public uint dwFillAttribute, dwFlags;
        public ushort wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public nint lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out nint readPipe, out nint writePipe,
        ref SECURITY_ATTRIBUTES pipeAttributes, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        ref SECURITY_ATTRIBUTES securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(nint attributeList, int attributeCount,
        uint flags, ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(nint attributeList, uint flags, nuint attribute,
        nint value, nuint size, nint previousValue, nint returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(nint attributeList);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string? applicationName, StringBuilder commandLine,
        nint processAttributes, nint threadAttributes, bool inheritHandles, uint creationFlags,
        nint environment, string currentDirectory, ref STARTUPINFOEX startupInfo, out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(nint threadHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(nint processHandle, uint exitCode);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}

internal sealed class NativeChildProcessHandle : INativeChildProcessHandle
{
    private nint _processHandle;
    private WindowsProcessJob? _job;
    private int _disposed;
    private int _suspended;

    public NativeChildProcessHandle(int id, nint processHandle, WindowsProcessJob job,
        nint stdoutRead, nint stderrRead)
    {
        Id = id;
        _processHandle = processHandle;
        _job = job;
        StandardOutput = CreateReader(stdoutRead);
        StandardError = CreateReader(stderrRead);
    }

    public int Id { get; }
    public StreamReader StandardOutput { get; }
    public StreamReader StandardError { get; }

    public bool HasExited
    {
        get
        {
            ThrowIfDisposed();
            if (!GetExitCodeProcess(_processHandle, out var exitCode))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return exitCode != STILL_ACTIVE;
        }
    }

    public int ExitCode
    {
        get
        {
            ThrowIfDisposed();
            if (!GetExitCodeProcess(_processHandle, out var exitCode))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (exitCode == STILL_ACTIVE) throw new InvalidOperationException("Process has not exited.");
            return unchecked((int)exitCode);
        }
    }

    public async Task WaitForExitAsync(CancellationToken token)
    {
        while (!HasExited)
            await Task.Delay(20, token).ConfigureAwait(false);
    }

    public void Suspend()
    {
        ThrowIfDisposed();
        if (Interlocked.Exchange(ref _suspended, 1) != 0) return;
        var status = NtSuspendProcess(_processHandle);
        if (status < 0)
        {
            Interlocked.Exchange(ref _suspended, 0);
            throw new Win32Exception(status);
        }
    }

    public void Resume()
    {
        ThrowIfDisposed();
        if (Interlocked.Exchange(ref _suspended, 0) == 0) return;
        var status = NtResumeProcess(_processHandle);
        if (status < 0)
        {
            Interlocked.Exchange(ref _suspended, 1);
            throw new Win32Exception(status);
        }
    }

    public void TerminateOwnedTree()
    {
        ThrowIfDisposed();
        _job?.Terminate();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _job?.Dispose(); }
        finally
        {
            _job = null;
            StandardOutput.Dispose();
            StandardError.Dispose();
            var handle = Interlocked.Exchange(ref _processHandle, 0);
            if (handle != 0) CloseHandle(handle);
        }
    }

    private static StreamReader CreateReader(nint readHandle)
    {
        var safe = new SafeFileHandle(readHandle, ownsHandle: true);
        try
        {
            var stream = new FileStream(safe, FileAccess.Read, 4096, isAsync: false);
            return new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096, leaveOpen: false);
        }
        catch
        {
            safe.Dispose();
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
    }

    private const uint STILL_ACTIVE = 259;

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(nint processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(nint processHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(nint processHandle, out uint exitCode);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
