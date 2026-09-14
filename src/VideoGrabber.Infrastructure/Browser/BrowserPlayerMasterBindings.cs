using System.Collections.Concurrent;

namespace VideoGrabber.Infrastructure.Browser;

public sealed class BrowserPlayerMasterBindings
{
    private readonly ConcurrentDictionary<string, Uri> _players = new(StringComparer.Ordinal);

    public void Remember(Uri master, Uri player)
        => _players[master.AbsoluteUri] = player;

    public bool TryResolve(Uri master, out Uri? player)
        => _players.TryGetValue(master.AbsoluteUri, out player);

    public void Clear() => _players.Clear();
}
