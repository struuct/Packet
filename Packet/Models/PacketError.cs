namespace Packet.Models;

public enum PacketError
{
    None,
    HandshakeFailed,
    RoomFull,
    AuthFailed,
    Throttled,
    PayloadTooLarge,
    InvalidFrame,
    ConnectionLost
}

internal static class WsCloseCode
{
    internal const int PolicyViolation = 1008;
    internal const int RateLimit       = 4429;
    internal const int PayloadTooLarge = 4413;
    internal const int InvalidFrame    = 4400;
}
