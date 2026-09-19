using VideoGrabber.Infrastructure.Processes;

namespace VideoGrabber.Infrastructure.Tests;

[Collection("ProcessPauseRegistrySerial")]
public sealed class ProcessPauseRegistryTests
{
    [Fact]
    public void Pause_and_resume_reach_registered_processes()
    {
        var fake = new FakeHandle();
        ProcessPauseRegistry.ResumeAll();
        try
        {
            ProcessPauseRegistry.Register(fake);
            ProcessPauseRegistry.PauseAll();
            Assert.True(ProcessPauseRegistry.IsPaused);
            Assert.Equal(1, fake.SuspendCalls);

            ProcessPauseRegistry.ResumeAll();
            Assert.False(ProcessPauseRegistry.IsPaused);
            Assert.Equal(1, fake.ResumeCalls);
        }
        finally
        {
            ProcessPauseRegistry.Unregister(fake);
            ProcessPauseRegistry.ResumeAll();
        }
    }

    [Fact]
    public void Process_registered_while_paused_is_immediately_suspended()
    {
        var fake = new FakeHandle();
        ProcessPauseRegistry.PauseAll();
        try
        {
            ProcessPauseRegistry.Register(fake);
            Assert.Equal(1, fake.SuspendCalls);
        }
        finally
        {
            ProcessPauseRegistry.Unregister(fake);
            ProcessPauseRegistry.ResumeAll();
        }
    }

    private sealed class FakeHandle : INativeChildProcessHandle
    {
        public int SuspendCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public int Id { get; } = Random.Shared.Next(100000, 999999);
        public StreamReader StandardOutput => throw new NotSupportedException();
        public StreamReader StandardError => throw new NotSupportedException();
        public bool HasExited => false;
        public int ExitCode => 0;
        public Task WaitForExitAsync(CancellationToken token) => Task.CompletedTask;
        public void Suspend() => SuspendCalls++;
        public void Resume() => ResumeCalls++;
        public void TerminateOwnedTree() { }
        public void Dispose() { }
    }
}

[CollectionDefinition("ProcessPauseRegistrySerial", DisableParallelization = true)]
public sealed class ProcessPauseRegistrySerialCollection
{
}