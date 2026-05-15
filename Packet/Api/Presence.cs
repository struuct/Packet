using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Packet.Behaviors;
using Packet.Channels;
using Packet.Models;
using Photon.Realtime;

namespace Packet.Api;

public sealed class Presence
{
    readonly Channel<PresencePayload> Channel;
    readonly Dictionary<string, long> LastSeen = new();
    readonly List<Player> _players = new();
    readonly CancellationTokenSource Cts = new();

    public IReadOnlyList<Player> Players => _players;
    public event Action<Player>? OnPlayerJoined;
    public event Action<Player>? OnPlayerLeft;

    internal Presence(Channel<PresencePayload> channel)
    {
        Channel = channel;
        Channel.OnMessage += OnReceive;
        PacketRuntime.Client.OnStateChanged += OnStateChanged;
        BroadcastActive();
        _ = HeartbeatLoopAsync(Cts.Token);
    }

    void OnStateChanged(ConnectionState state)
    {
        if (state == ConnectionState.Connected)
            BroadcastActive();
        else if (state == ConnectionState.Disconnected)
            PacketRuntime.RunOnMainThread(ClearAll);
    }

    void OnReceive(Player sender, PresencePayload payload)
    {
        if (payload.Active)
        {
            LastSeen[sender.UserId] = DateTime.UtcNow.Ticks;
            if (!_players.Exists(p => p.UserId == sender.UserId))
            {
                _players.Add(sender);
                OnPlayerJoined?.Invoke(sender);
                BroadcastActive();
            }
        }
        else
        {
            var idx = _players.FindIndex(p => p.UserId == sender.UserId);
            if (idx >= 0)
            {
                var player = _players[idx];
                _players.RemoveAt(idx);
                LastSeen.Remove(sender.UserId);
                OnPlayerLeft?.Invoke(player);
            }
        }
    }

    void BroadcastActive() => Channel.Send(new PresencePayload { Active = true });

    async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (OperationCanceledException) { break; }
            PacketRuntime.RunOnMainThread(() => { BroadcastActive(); CheckTimeouts(); });
        }
    }

    void CheckTimeouts()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-75).Ticks;
        var timedOut = new List<string>();
        foreach (var kv in LastSeen)
            if (kv.Value < cutoff) timedOut.Add(kv.Key);
        foreach (var id in timedOut)
        {
            LastSeen.Remove(id);
            var idx = _players.FindIndex(p => p.UserId == id);
            if (idx < 0) continue;
            var player = _players[idx];
            _players.RemoveAt(idx);
            OnPlayerLeft?.Invoke(player);
        }
    }

    void ClearAll()
    {
        var snapshot = _players.ToArray();
        _players.Clear();
        LastSeen.Clear();
        foreach (var player in snapshot)
            OnPlayerLeft?.Invoke(player);
    }

    internal void Release()
    {
        Cts.Cancel();
        Channel.OnMessage -= OnReceive;
        PacketRuntime.Client.OnStateChanged -= OnStateChanged;
        try { Channel.Send(new PresencePayload { Active = false }); }
        catch
        {
            // ignored
        }

        ClearAll();
    }
}

internal sealed class PresencePayload
{
    public bool Active { get; set; }
}
