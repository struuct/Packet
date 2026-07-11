using System;
using System.Threading;
using System.Threading.Tasks;

namespace Packet.Net;

internal sealed class Policy
{
    static readonly object RandomGate = new();
    static readonly Random _rng = new();

    int _attempts;
    const int MaxAttempts = 5;
    const int BaseDelayMs = 1000;

    internal bool ShouldRetry => _attempts < MaxAttempts;

    internal async Task WaitAsync(CancellationToken ct)
    {
        int jitter;
        lock (RandomGate)
        {
            jitter = _rng.Next(0, 500);
        }

        var delay = Math.Min(BaseDelayMs * (1 << _attempts) + jitter, 30_000);
        await Task.Delay(delay, ct);
        _attempts++;
    }

    internal void Reset() => _attempts = 0;
}
