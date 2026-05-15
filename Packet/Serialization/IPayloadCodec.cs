namespace Packet.Serialization;

internal interface IPayloadCodec
{
    string Encode<T>(T value);
    T Decode<T>(string data);
}
