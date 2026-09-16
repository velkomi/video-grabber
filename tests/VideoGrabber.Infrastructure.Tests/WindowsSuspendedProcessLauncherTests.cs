using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using VideoGrabber.Core.Processes;
using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

[CollectionDefinition("ProcessTreeSerial", DisableParallelization = true)]
public sealed class ProcessTreeSerialCollection { }

[Collection("ProcessTreeSerial")]
public sealed class WindowsSuspendedProcessLauncherTests
{
    [Fact]
    public async Task Launcher_preserves_quoted_arguments_working_directory_and_utf8_output()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "VG native launcher Юникод " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var script = Path.Combine(root, "echo args Юникод.ps1");
        await File.WriteAllTextAsync(script, "[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); "
            + "$args | ForEach-Object { [Console]::WriteLine('ARG:' + $_) }; "
            + "[Console]::WriteLine('PWD:' + (Get-Location).Path); [Console]::WriteLine('UNICODE:Привет мир')", Encoding.UTF8);
        try
        {
            using var child = WindowsSuspendedProcessLauncher.Start(new ProcessSpec("powershell.exe",
                ["-NoProfile", "-NonInteractive", "-File", script, "space value", "quote\"value", "Юникод"],
                WorkingDirectory: root));
            var stdout = child.StandardOutput.ReadToEndAsync();
            var stderr = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, child.ExitCode);
            var text = await stdout;
            Assert.Contains("ARG:space value", text);
            Assert.Contains("ARG:quote\"value", text);
            Assert.Contains("ARG:Юникод", text);
            Assert.Contains("PWD:" + root, text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("UNICODE:Привет мир", text);
            Assert.True(string.IsNullOrWhiteSpace(await stderr));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Launcher_assigns_job_before_resume_for_immediate_descendants()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "VG-native-job-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var childCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes("Start-Sleep -Seconds 30"));
        var script = Path.Combine(root, "spawn.ps1");
        await File.WriteAllTextAsync(script,
            "$p=Start-Process powershell.exe -ArgumentList '-NoProfile','-NonInteractive','-EncodedCommand','" + childCommand + "' -PassThru -WindowStyle Hidden; "
            + "[Console]::WriteLine('CHILD:' + $p.Id + '|' + $p.StartTime.ToUniversalTime().Ticks); [Console]::Out.Flush()", Encoding.UTF8);
        try
        {
            for (var iteration = 0; iteration < 20; iteration++)
            {
                using var launched = WindowsSuspendedProcessLauncher.Start(new ProcessSpec("powershell.exe",
                    ["-NoProfile", "-NonInteractive", "-File", script], WorkingDirectory: root));
                var outputTask = launched.StandardOutput.ReadToEndAsync();
                var errorTask = launched.StandardError.ReadToEndAsync();
                await launched.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(8));
                Assert.Equal(0, launched.ExitCode);
                var line = (await outputTask).Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
                    .Single(value => value.StartsWith("CHILD:", StringComparison.Ordinal));
                var parts = line["CHILD:".Length..].Split('|');
                var pid = int.Parse(parts[0]);
                var ticks = long.Parse(parts[1]);
                using var owned = Process.GetProcessById(pid);
                Assert.Equal(ticks, owned.StartTime.ToUniversalTime().Ticks);
                Assert.False(owned.HasExited);
                launched.TerminateOwnedTree();
                await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(owned.HasExited);
                Assert.True(string.IsNullOrWhiteSpace(await errorTask));
            }
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Missing_program_fails_during_suspended_launch()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Throws<Win32Exception>(() => WindowsSuspendedProcessLauncher.Start(
            new ProcessSpec("vg-definitely-missing-native-launcher.exe", [])));
    }
}
