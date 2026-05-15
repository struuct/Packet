using Newtonsoft.Json;

namespace Packet.Models;

internal sealed class Envelope
{
    [JsonProperty("c")] public string? Channel { get; set; }
    [JsonProperty("p")] public string? Payload { get; set; }
    [JsonProperty("s")] public string? SenderId { get; set; }
    [JsonProperty("t", NullValueHandling = NullValueHandling.Ignore)] public string? Target { get; set; }
}