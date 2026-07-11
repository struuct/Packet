using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Packet.Api;
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
            var message = JObject.Parse(raw);
            if (TryHandleError(message))
            {
                return;
            }

            var envelope = message.ToObject<Envelope>();
            if (envelope?.Channel == null) return;
            Runtime.RunOnMainThread(() => Registry.TryDispatch(envelope));
        }
        catch (Exception ex) { PacketLog.Warn($"dispatch error: {ex.Message}"); }
    }

    static bool TryHandleError(JObject message)
    {
        var type = message.Value<string>("type");
        if (!string.Equals(type, "error", StringComparison.Ordinal))
        {
            return false;
        }

        var code = message.Value<string>("code");
        var channel = message.Value<string>("channel");
        var detail = message.Value<string>("message");
        var error = code switch
        {
            "namespace_forbidden" => PacketError.NamespaceForbidden,
            "namespace_mismatch" => PacketError.NamespaceMismatch,
            _ => PacketError.None
        };

        if (error == PacketError.None)
        {
            return true;
        }

        PacketLog.Warn($"packet namespace error ({code}) on '{channel}': {detail}");
        PacketApi.RaiseError(error);
        return true;
    }
}
