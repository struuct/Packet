using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Packet.Api;
using Packet.Channels;
using Packet.Extensions;
using Packet.Logging;
using Packet.Models;
using Packet.Serialization;

namespace Packet.Net;

internal sealed class Client
{
    readonly ChannelRegistry _registry = new();
    readonly Handshake _handshake = new();
    readonly Policy _reconnect = new();

    Transport? _transport;
    Dispatcher? _dispatcher;
    CancellationTokenSource? _cts;

    string? _backendUrl;
    string? _userId;
    string? _roomCode;

    PacketError _lastError;
    int _reconnectActive;
    volatile bool _stopping;

    internal ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    internal event Action<ConnectionState>? OnStateChanged;

    internal Channel<T> GetChannel<T>(string modGuid, string name)
        => _registry.GetOrCreate<T>($"{modGuid}/{name}", PayloadCodec.Default);

    internal void ReleaseChannel(string fullId) => _registry.Remove(fullId);

    internal void UnregisterMod(string modGuid)
    {
        var prefix = $"{modGuid}/";
        var toRemove = _registry.ChannelIds().Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var id in toRemove) _registry.Remove(id);
    }

    internal void Ping()
    {
        if (_transport?.State == ConnectionState.Connected)
            _ = _transport.SendAsync("{\"type\":\"ping\"}");
    }

    internal async Task JoinRoomAsync(string backendUrl, string userId, string roomCode)
    {
        if (string.IsNullOrWhiteSpace(backendUrl))
        {
            throw new ArgumentException("Backend URL is required.", nameof(backendUrl));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(roomCode))
        {
            throw new ArgumentException("Room code is required.", nameof(roomCode));
        }

        _stopping = false;
        _backendUrl = backendUrl;
        _userId = userId;
        _roomCode = roomCode;
        _reconnect.Reset();
        await ConnectAsync();
    }

    internal async Task LeaveRoomAsync()
    {
        _stopping = true;
        _cts?.Cancel();
        if (_transport != null) await _transport.CloseAsync();
        _registry.ClearTransport();
        SetState(ConnectionState.Disconnected);
    }

    async Task ConnectAsync()
    {
        SetState(ConnectionState.Connecting);

        if (_backendUrl == null || _userId == null || _roomCode == null)
        {
            throw new InvalidOperationException("Packet client connection state is incomplete.");
        }

        var (result, hsError) = await _handshake.RunAsync(_backendUrl, _userId, _roomCode);
        if (_stopping) return;
        if (result == null)
        {
            SetState(ConnectionState.Disconnected);
            if (_reconnectActive == 0 || hsError == PacketError.RoomFull)
            {
                PacketApi.RaiseError(hsError);
                _cts?.Cancel();
            }
            return;
        }

        _cts = new CancellationTokenSource();
        _transport = new Transport();
        _dispatcher = new Dispatcher(_registry);

        _transport.OnMessage += _dispatcher.Dispatch;
        _transport.OnConnected += OnSocketConnected;
        _transport.OnDisconnected += HandleSocketDisconnected;
        _transport.OnError += e => { _lastError = e; PacketLog.Error($"transport error: {e}"); };

        await _transport.ConnectAsync(result.WsUrl, result.SessionToken);
    }

    void OnSocketConnected()
    {
        _reconnect.Reset();
        _registry.SetTransport((channelId, payload, target) =>
            _ = _transport!.SendAsync(JsonConvert.SerializeObject(new Envelope { Channel = channelId, Payload = payload, Target = target }))
        );
        SetState(ConnectionState.Connected);
        PacketLog.Info($"connected in room {_roomCode}");
    }

    async Task OnSocketDisconnectedAsync()
    {
        if (_stopping) { SetState(ConnectionState.Disconnected); return; }
        var err = _lastError;
        _lastError = PacketError.None;

        _registry.ClearTransport();

        if (!IsRetryable(err))
        {
            PacketApi.RaiseError(err);
            SetState(ConnectionState.Disconnected);
            return;
        }

        if (Interlocked.CompareExchange(ref _reconnectActive, 1, 0) != 0) return;

        try
        {
            SetState(ConnectionState.Reconnecting);
            PacketLog.Warn("disconnected; attempting to reconnect");

            while (_reconnect.ShouldRetry)
            {
                try { await _reconnect.WaitAsync(_cts!.Token); }
                catch (OperationCanceledException) { return; }

                await ConnectAsync();
                if (_transport?.State == ConnectionState.Connected) return;
            }

            PacketApi.RaiseError(PacketError.ConnectionLost);
            SetState(ConnectionState.Disconnected);
            PacketLog.Error("max reconnect attempts reached");
        }
        finally { _reconnectActive = 0; }
    }

    static bool IsRetryable(PacketError e) =>
        e is PacketError.None or PacketError.ConnectionLost or PacketError.Throttled;

    void HandleSocketDisconnected() => OnSocketDisconnectedAsync().Forget("Packet reconnect loop");

    void SetState(ConnectionState state)
    {
        State = state;
        OnStateChanged?.Invoke(state);
        PacketApi.RaiseStateChanged(state);
    }
}
