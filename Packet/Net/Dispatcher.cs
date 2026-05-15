using System;
using Newtonsoft.Json;
using Packet.Behaviors;
using Packet.Channels;
using Packet.Logging;
using Packet.Models;

namespace Packet.Net;

internal sealed class Dispatcher
{
    readonly ChannelRegistry Registry;

    internal Dispatcher(ChannelRegistry registry) => Registry = registry;

    internal void Dispatch(string raw)
    {
        try
        {
            var envelope = JsonConvert.DeserializeObject<Envelope>(raw);
            if (envelope?.Channel == null) return;
            PacketRuntime.RunOnMainThread(() => Registry.TryDispatch(envelope));
        }
        catch (Exception ex) { PacketLog.Warn($"dispatch error: {ex.Message}"); }
    }
}
