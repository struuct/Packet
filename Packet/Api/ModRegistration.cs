using System;
using Packet.Behaviors;
using Packet.Channels;

namespace Packet.Api;

public sealed class ModRegistration
{
    const string PresenceChannelName = ".presence";
    const string InternalChannelPrefix = ".packet/";

    readonly string ModGuid;
    Presence? _presence;

    internal ModRegistration(string modGuid) => ModGuid = modGuid;

    public Channel<T> GetChannel<T>(string name) =>
        Runtime.Connection.GetChannel<T>(ModGuid, ValidatePublicChannelName(name));

    public void ReleaseChannel(string name) =>
        Runtime.Connection.ReleaseChannel($"{ModGuid}/{ValidatePublicChannelName(name)}");

    public Presence GetPresence()
    {
        if (_presence != null)
        {
            return _presence;
        }
        
        var channel = Runtime.Connection.GetChannel<PresencePayload>(ModGuid, ".presence");
        _presence = new Presence(channel);
        return _presence;
    }

    public void Unregister()
    {
        _presence?.Release();
        _presence = null;
        Runtime.Connection.UnregisterMod(ModGuid);
    }

    static string ValidatePublicChannelName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Channel name is required", nameof(name));
        }

        if (string.Equals(name, PresenceChannelName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("'.presence' is reserved");
        }

        return name.StartsWith(InternalChannelPrefix, StringComparison.Ordinal) ? throw new InvalidOperationException("Channels under '.packet/' are reserved for Packet internals") : name;
    }
}
