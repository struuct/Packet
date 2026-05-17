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

            var hs = JsonConvert.DeserializeObject<HandshakeResponse>(await response.Content.ReadAsStringAsync());
            if (hs!.Version != Constants.BackendProtocolVersion)
                PacketLog.Warn($"backend version mismatch: expected {Constants.BackendProtocolVersion}, got {hs.Version} - update Packet");
            return (new HandshakeResult { SessionToken = hs.SessionToken, WsUrl = hs.WsUrl }, PacketError.None);
        }
        catch (Exception ex)
        {
            PacketLog.Error($"handshake error: {ex.Message}");
            return (null, PacketError.HandshakeFailed);
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
}

internal sealed class HandshakeResult
{
    internal string? SessionToken { get; set; }
    internal string? WsUrl        { get; set; }
}
