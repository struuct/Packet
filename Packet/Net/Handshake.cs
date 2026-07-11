using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Packet.Api;
using Packet.Logging;
using Packet.Models;

namespace Packet.Net;

internal sealed class Handshake
{
    static readonly HttpClient Http = new();

    internal async Task<(HandshakeResult? Result, PacketError Error)> RunAsync(string backendUrl, string userId, string roomCode)
    {
        var body = JsonConvert.SerializeObject(new HandshakeRequest
        {
            UserId = userId,
            RoomCode = roomCode,
            Mods = PacketApi.GetRegisteredMods()
        });

        try
        {
            var response = await Http.PostAsync(
                $"{backendUrl}/handshake",
                new StringContent(body, Encoding.UTF8, "application/json")
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = response.StatusCode == HttpStatusCode.ServiceUnavailable
                    ? PacketError.RoomFull
                    : PacketError.HandshakeFailed;
                var detail = await response.Content.ReadAsStringAsync();
                PacketLog.Error($"handshake rejected: HTTP {(int)response.StatusCode} - {detail}");
                return (null, error);
            }

            var content = await response.Content.ReadAsStringAsync();
            var hs = JsonConvert.DeserializeObject<HandshakeResponse>(content);
            if (hs == null || hs.SessionToken == null || hs.SessionToken.Length == 0 || hs.WsUrl == null || hs.WsUrl.Length == 0)
            {
                PacketLog.Error("handshake response was missing sessionToken or wsUrl");
                return (null, PacketError.HandshakeFailed);
            }

            if (!string.Equals(hs.Version, Constants.BackendProtocolVersion, StringComparison.Ordinal))
            {
                PacketLog.Warn($"backend version mismatch: expected {Constants.BackendProtocolVersion}, got {hs.Version} - update Packet");
            }

            LogHandshakeNamespaceResults(hs);
            return (new HandshakeResult(hs.SessionToken, hs.WsUrl), PacketError.None);
        }
        catch (Exception ex)
        {
            PacketLog.Error($"handshake error: {ex.Message}");
            return (null, PacketError.HandshakeFailed);
        }
    }

    static void LogHandshakeNamespaceResults(HandshakeResponse response)
    {
        foreach (var rejected in response.RejectedMods ?? Array.Empty<RejectedMod>())
        {
            PacketLog.Warn($"backend rejected mod namespace '{rejected.Guid}' ({rejected.Reason})");
        }

        if (response.AcceptedMods == null) return;

        var registered = PacketApi.GetRegisteredMods();
        foreach (var guid in registered)
        {
            if (Array.IndexOf(response.AcceptedMods, guid) < 0)
            {
                PacketLog.Warn($"backend did not accept registered namespace '{guid}'");
            }
        }
    }
}

internal sealed class HandshakeRequest
{
    [JsonProperty("userId")]   public string?   UserId   { get; set; }
    [JsonProperty("roomCode")] public string?   RoomCode { get; set; }
    [JsonProperty("mods")]     public string[]? Mods     { get; set; }
}

internal sealed class HandshakeResponse
{
    [JsonProperty("sessionToken")] public string? SessionToken { get; set; }
    [JsonProperty("wsUrl")]        public string? WsUrl        { get; set; }
    [JsonProperty("version")]      public string? Version      { get; set; }
    [JsonProperty("acceptedMods")] public string[]? AcceptedMods { get; set; }
    [JsonProperty("rejectedMods")] public RejectedMod[]? RejectedMods { get; set; }
}

internal sealed class RejectedMod
{
    [JsonProperty("guid")] public string Guid { get; set; } = string.Empty;
    [JsonProperty("reason")] public string Reason { get; set; } = string.Empty;
}

internal sealed class HandshakeResult
{
    internal HandshakeResult(string sessionToken, string wsUrl)
    {
        SessionToken = sessionToken;
        WsUrl = wsUrl;
    }

    internal string SessionToken { get; }
    internal string WsUrl { get; }
}
