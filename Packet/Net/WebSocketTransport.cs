using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Packet.Logging;
using Packet.Models;

namespace Packet.Net;

internal sealed class WebSocketTransport
{
    ClientWebSocket? _socket;
    CancellationTokenSource? _cts;

    internal event Action<string>? OnMessage;
    internal event Action<PacketError>? OnError;
    internal event Action? OnConnected;
    internal event Action? OnDisconnected;

    internal ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    internal async Task ConnectAsync(string url, string bearerToken)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _socket = new ClientWebSocket();
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
        _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        State = ConnectionState.Connecting;
        try
        {
            await _socket.ConnectAsync(new Uri(url), _cts.Token);
            State = ConnectionState.Connected;
            OnConnected?.Invoke();
            _ = ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            State = ConnectionState.Disconnected;
            PacketLog.Error($"ws connect failed: {ex.Message}");
            OnError?.Invoke(PacketError.ConnectionLost);
        }
    }

    internal async Task SendAsync(string json)
    {
        if (State != ConnectionState.Connected) return;
        var bytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        try { await _socket?.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token); }
        catch (Exception ex) { PacketLog.Warn($"ws send failed: {ex.Message}"); }
    }

    internal async Task CloseAsync()
    {
        try
        {
            if (_socket?.State == WebSocketState.Open)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        catch { }
        finally
        {
            _cts?.Cancel();
            State = ConnectionState.Disconnected;
        }
    }

    async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var message = new StringBuilder();
        var closeError = PacketError.None;

        try
        {
            while (!ct.IsCancellationRequested && _socket is { State: WebSocketState.Open })
            {
                message.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeError = MapCloseCode(_socket.CloseStatus);
                        goto done;
                    }
                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                OnMessage?.Invoke(message.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            PacketLog.Error($"ws recieve error: {ex.Message}");
            closeError = PacketError.ConnectionLost;
        }

        done:
        State = ConnectionState.Disconnected;
        if (closeError != PacketError.None) OnError?.Invoke(closeError);
        OnDisconnected?.Invoke();
    }

    static PacketError MapCloseCode(WebSocketCloseStatus? status) => (int?)status switch
    {
        WsCloseCode.PolicyViolation => PacketError.AuthFailed,
        WsCloseCode.RateLimit       => PacketError.Throttled,
        WsCloseCode.PayloadTooLarge => PacketError.PayloadTooLarge,
        WsCloseCode.InvalidFrame    => PacketError.InvalidFrame,
        null                        => PacketError.None,
        _                           => PacketError.ConnectionLost
    };
}
