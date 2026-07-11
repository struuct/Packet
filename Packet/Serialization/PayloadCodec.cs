using Newtonsoft.Json;

namespace Packet.Serialization;

internal sealed class PayloadCodec : IPayloadCodec
{
    internal static readonly PayloadCodec Default = new();

    static readonly JsonSerializerSettings Settings = new() { NullValueHandling = NullValueHandling.Ignore };

    public string Encode<T>(T value) => JsonConvert.SerializeObject(value, Settings);
    public T Decode<T>(string data)
    {
        var value = JsonConvert.DeserializeObject<T>(data, Settings);
        if (value == null)
        {
            throw new JsonSerializationException($"Could not deserialize payload as {typeof(T).Name}.");
        }

        return value;
    }
}
