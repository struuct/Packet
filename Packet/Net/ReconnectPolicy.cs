using System;
using System.Threading;
using System.Threading.Tasks;

namespace Packet.Net;

internal sealed class ReconnectPolicy
{
    static readonly Random _rng = new();

    int _attempts;
    const int MaxAttempts = 5;
    const int BaseDelayMs = 1000;

    internal bool ShouldRetry => _attempts < MaxAttempts;

    internal async Task WaitAsync(CancellationToken ct)
    {
        var delay = Math.Min(BaseDelayMs * (1 << _attempts) + _rng.Next(0, 500), 30_000);
        await Task.Delay(delay, ct);
        _attempts++;
    }

    internal void Reset() => _attempts = 0;
}
