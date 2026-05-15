using System;
using System.Collections.Generic;
using Packet.Models;
using Packet.Serialization;
using Photon.Pun;
using Photon.Realtime;

namespace Packet.Channels;

public abstract class Channel(string id)
{
    protected string Id { get; } = id;

    Action<string, string, string?>? _send;
    Queue<(string encoded, string? target)>? _queue;
    const int QueueCap = 16;

    internal void SetTransport(Action<string, string, string?> send)
    {
        _send = send;
        if (_queue == null) return;
        while (_queue.Count > 0)
        {
            var (encoded, target) = _queue.Dequeue();
            send(Id, encoded, target);
        }
        _queue = null;
    }

    internal void ClearTransport() => _send = null;

    internal void Release()
    {
        _send = null;
        _queue = null;
    }

    protected bool TrySend(string encoded, string? target = null)
    {
        if (_send != null) { _send(Id, encoded, target); return true; }
        _queue ??= new Queue<(string, string?)>();
        if (_queue.Count >= QueueCap) _queue.Dequeue();
        _queue.Enqueue((encoded, target));
        return false;
    }

    internal abstract void Dispatch(Envelope envelope);
}

public sealed class Channel<T> : Channel
{
    readonly IPayloadCodec Codec;

    public event Action<Player, T>? OnMessage;

    internal Channel(string id, IPayloadCodec codec) : base(id) => Codec = codec;

    public bool Send(T payload) => TrySend(Codec.Encode(payload));
    public bool SendTo(Player player, T payload) => TrySend(Codec.Encode(payload), player.UserId);
    public bool SendTo(string userId, T payload) => TrySend(Codec.Encode(payload), userId);

    internal override void Dispatch(Envelope envelope)
    {
        try
        {
            var sender = Array.Find(PhotonNetwork.PlayerList, p => p.UserId == envelope.SenderId);
            if (sender == null) return;
            OnMessage?.Invoke(sender, Codec.Decode<T>(envelope.Payload));
        }
        catch { }
    }
}

internal sealed class ChannelRegistry
{
    readonly Dictionary<string, Channel> Channels = new();

    internal Channel<T> GetOrCreate<T>(string fullId, IPayloadCodec codec)
    {
        if (Channels.TryGetValue(fullId, out var existing))
            return (Channel<T>)existing;

        var channel = new Channel<T>(fullId, codec);
        Channels[fullId] = channel;
        return channel;
    }

    internal void SetTransport(Action<string, string, string?> send)
    {
        foreach (var ch in Channels.Values)
            ch.SetTransport(send);
    }

    internal void ClearTransport()
    {
        foreach (var ch in Channels.Values)
            ch.ClearTransport();
    }

    internal bool TryDispatch(Envelope envelope)
    {
        if (!Channels.TryGetValue(envelope.Channel!, out var ch)) return false;
        ch.Dispatch(envelope);
        return true;
    }

    internal IEnumerable<string> ChannelIds() => Channels.Keys;

    internal void Remove(string fullId)
    {
        if (!Channels.TryGetValue(fullId, out var ch)) return;
        ch.Release();
        Channels.Remove(fullId);
    }
}
