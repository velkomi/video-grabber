using VideoGrabber.Platform.Api.Auth;
using VideoGrabber.Platform.Api.Jobs;
using Xunit;

namespace VideoGrabber.Platform.Tests;

public sealed class BrowserDownloadTicketTests
{
    [Fact]
    public void Ticket_round_trip_is_account_and_job_scoped()
    {
        var options = new SessionJwtOptions(
            "videograbber-platform",
            "videograbber-api",
            Enumerable.Repeat((byte)0x5A, 64).ToArray());
        var service = new BrowserDownloadTicketService(options, TimeProvider.System);
        var account = Guid.NewGuid();
        var job = Guid.NewGuid();

        var ticket = service.Create(account, job);

        Assert.True(service.TryValidate(ticket, out var actualAccount, out var actualJob));
        Assert.Equal(account, actualAccount);
        Assert.Equal(job, actualJob);
    }

    [Fact]
    public void Ticket_rejects_tampering()
    {
        var options = new SessionJwtOptions(
            "videograbber-platform",
            "videograbber-api",
            Enumerable.Repeat((byte)0x33, 64).ToArray());
        var service = new BrowserDownloadTicketService(options, TimeProvider.System);
        var ticket = service.Create(Guid.NewGuid(), Guid.NewGuid());
        var chars = ticket.ToCharArray();
        chars[^2] = chars[^2] == 'A' ? 'B' : 'A';

        Assert.False(service.TryValidate(new string(chars), out _, out _));
    }
}
