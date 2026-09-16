param(
    [Parameter(Mandatory = $true)][string]$StatePath,
    [Parameter(Mandatory = $true)][string]$ReadyPath,
    [Parameter(Mandatory = $true)][string]$GoPath
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class AuditNativeProcess
{
    public const int STD_INPUT_HANDLE = -10;
    public const int STD_OUTPUT_HANDLE = -11;
    public const int STD_ERROR_HANDLE = -12;
    public const uint STARTF_USESTDHANDLES = 0x00000100;
    public const uint HANDLE_FLAG_INHERIT = 0x00000001;
    public const uint CREATE_NO_WINDOW = 0x08000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CreateProcessW(
        string lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    public static void ThrowLastWin32(string operation)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), operation);
    }
}
"@

$childScript = @"
`$ErrorActionPreference = 'Stop'
`$self = [Diagnostics.Process]::GetCurrentProcess()
[IO.File]::WriteAllText('$($ReadyPath.Replace("'", "''"))', (`$PID.ToString() + '|' + `$self.StartTime.ToUniversalTime().Ticks.ToString()))
`$deadline = [DateTime]::UtcNow.AddSeconds(10)
while (-not [IO.File]::Exists('$($GoPath.Replace("'", "''"))') -and [DateTime]::UtcNow -lt `$deadline) { Start-Sleep -Milliseconds 25 }
if ([IO.File]::Exists('$($GoPath.Replace("'", "''"))')) {
    [Console]::Out.WriteLine('AUDIT_CHILD_PIPE_HELD:' + `$PID)
    [Console]::Out.Flush()
}
Start-Sleep -Seconds 30
"@
$encodedChild = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($childScript))
$powershell = [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
$commandLine = [Text.StringBuilder]::new('"' + $powershell + '" -NoProfile -NonInteractive -EncodedCommand ' + $encodedChild)

$stdin = [AuditNativeProcess]::GetStdHandle([AuditNativeProcess]::STD_INPUT_HANDLE)
$stdout = [AuditNativeProcess]::GetStdHandle([AuditNativeProcess]::STD_OUTPUT_HANDLE)
$stderr = [AuditNativeProcess]::GetStdHandle([AuditNativeProcess]::STD_ERROR_HANDLE)
$invalid = [IntPtr](-1)
foreach ($handle in @($stdin, $stdout, $stderr)) {
    if ($handle -ne [IntPtr]::Zero -and $handle -ne $invalid) {
        if (-not [AuditNativeProcess]::SetHandleInformation($handle, [AuditNativeProcess]::HANDLE_FLAG_INHERIT, [AuditNativeProcess]::HANDLE_FLAG_INHERIT)) {
            [AuditNativeProcess]::ThrowLastWin32('SetHandleInformation(inherit)')
        }
    }
}

$si = [AuditNativeProcess+STARTUPINFO]::new()
$si.cb = [Runtime.InteropServices.Marshal]::SizeOf([type][AuditNativeProcess+STARTUPINFO])
$si.dwFlags = [AuditNativeProcess]::STARTF_USESTDHANDLES
$si.hStdInput = $stdin
$si.hStdOutput = $stdout
$si.hStdError = $stderr
$pi = [AuditNativeProcess+PROCESS_INFORMATION]::new()
try {
    if (-not [AuditNativeProcess]::CreateProcessW(
        $powershell, $commandLine, [IntPtr]::Zero, [IntPtr]::Zero, $true,
        [AuditNativeProcess]::CREATE_NO_WINDOW, [IntPtr]::Zero, (Get-Location).Path, [ref]$si, [ref]$pi)) {
        [AuditNativeProcess]::ThrowLastWin32('CreateProcessW')
    }
    $child = [Diagnostics.Process]::GetProcessById([int]$pi.dwProcessId)
    try {
        $childStartTicks = $child.StartTime.ToUniversalTime().Ticks
        [IO.File]::WriteAllText($StatePath, ($child.Id.ToString() + '|' + $childStartTicks.ToString()))
    }
    finally { $child.Dispose() }

    [Console]::Out.WriteLine('AUDIT_PARENT_PID:' + $PID)
    [Console]::Out.WriteLine('AUDIT_CHILD_PID_FROM_PARENT:' + $pi.dwProcessId)
    [Console]::Out.Flush()
}
finally {
    foreach ($handle in @($stdin, $stdout, $stderr)) {
        if ($handle -ne [IntPtr]::Zero -and $handle -ne $invalid) {
            [void][AuditNativeProcess]::SetHandleInformation($handle, [AuditNativeProcess]::HANDLE_FLAG_INHERIT, 0)
        }
    }
    if ($pi.hThread -ne [IntPtr]::Zero) { [void][AuditNativeProcess]::CloseHandle($pi.hThread) }
    if ($pi.hProcess -ne [IntPtr]::Zero) { [void][AuditNativeProcess]::CloseHandle($pi.hProcess) }
}
exit 0
