using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Packet.Extensions;
using Packet.Logging;
using Packet.Models;

namespace Packet.Net;

internal sealed class Transport
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
        ResetConnection();

        var socket = new ClientWebSocket();
        var cts = new CancellationTokenSource();
        socket.Options.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        _socket = socket;
        _cts = cts;

        State = ConnectionState.Connecting;
        try
        {
            await socket.ConnectAsync(new Uri(url), cts.Token);
            State = ConnectionState.Connected;
            OnConnected?.Invoke();
            ReceiveLoopAsync(socket, cts.Token).Forget("WebSocket receive loop");
        }
        catch (Exception ex)
        {
            State = ConnectionState.Disconnected;
            PacketLog.Error($"ws connect failed: {ex.Message}");
            ResetConnection();
            OnError?.Invoke(PacketError.ConnectionLost);
        }
    }

    internal async Task SendAsync(string json)
    {
        var socket = _socket;
        var cts = _cts;
        if (State != ConnectionState.Connected || socket == null || cts == null) return;
        var bytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        try { await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token); }
        catch (Exception ex) { PacketLog.Warn($"ws send failed: {ex.Message}"); }
    }

    internal async Task CloseAsync()
    {
        var socket = _socket;
        try
        {
            if (socket?.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        catch
        {
            // ignored
        }
        finally
        {
            ResetConnection();
            State = ConnectionState.Disconnected;
        }
    }

    async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var message = new StringBuilder();
        var closeError = PacketError.None;
        var closed = false;

        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                message.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeError = MapCloseCode(result.CloseStatus ?? socket.CloseStatus);
                        closed = true;
                        break;
                    }

                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                if (closed)
                {
                    break;
                }

                OnMessage?.Invoke(message.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            PacketLog.Error($"ws receive error: {ex.Message}");
            closeError = PacketError.ConnectionLost;
        }

        State = ConnectionState.Disconnected;
        if (closeError != PacketError.None) OnError?.Invoke(closeError);
        OnDisconnected?.Invoke();
    }

    void ResetConnection()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _socket?.Dispose();
        _socket = null;
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
