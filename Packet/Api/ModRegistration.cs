using Packet.Behaviors;
using Packet.Channels;

namespace Packet.Api;

public sealed class ModRegistration
{
    readonly string ModGuid;
    Presence? _presence;

    internal ModRegistration(string modGuid) => ModGuid = modGuid;

    public Channel<T> GetChannel<T>(string name) =>
        PacketRuntime.Client.GetChannel<T>(ModGuid, name);

    public void ReleaseChannel(string name) =>
        PacketRuntime.Client.ReleaseChannel($"{ModGuid}/{name}");

    public Presence GetPresence()
    {
        if (_presence != null) return _presence;
        var channel = PacketRuntime.Client.GetChannel<PresencePayload>(ModGuid, ".presence");
        _presence = new Presence(channel);
        return _presence;
    }

    public void Unregister()
    {
        _presence?.Release();
        _presence = null;
        PacketRuntime.Client.UnregisterMod(ModGuid);
    }
}
