using BepInEx.Logging;

namespace Packet.Logging;

internal static class PacketLog
{
    static readonly ManualLogSource Source = Logger.CreateLogSource("Packet");

    internal static void Info(string msg) => Source.LogInfo(msg);
    internal static void Warn(string msg) => Source.LogWarning(msg);
    internal static void Error(string msg) => Source.LogError(msg);
}
