using Newtonsoft.Json;

namespace Packet.Serialization;

internal sealed class PayloadCodec : IPayloadCodec
{
    internal static readonly PayloadCodec Default = new();

    static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

    public string Encode<T>(T value) => JsonConvert.SerializeObject(value, Settings);
    public T Decode<T>(string data) => JsonConvert.DeserializeObject<T>(data, Settings)!;
}
