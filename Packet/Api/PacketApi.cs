using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Packet.Behaviors;
using Packet.Models;

namespace Packet.Api;

public static class PacketApi
{
    static readonly object ModsGate = new();
    static readonly HashSet<string> Mods = new(StringComparer.Ordinal);

    public static ModRegistration RegisterMod(BaseUnityPlugin plugin)
    {
        if (plugin == null)
        {
            throw new ArgumentNullException(nameof(plugin));
        }

        var guid = NormalizeGuid(plugin.Info.Metadata.GUID);
        if (string.IsNullOrWhiteSpace(guid))
        {
            throw new InvalidOperationException("A plugin GUID is required to register a mod");
        }

        if (string.Equals(guid, Constants.Guid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{Constants.Guid}' is reserved");
        }

        lock (ModsGate)
        {
            Mods.Add(guid);
        }

        return new(guid);
    }

    internal static string[] GetRegisteredMods()
    {
        lock (ModsGate)
        {
            return Mods.ToArray();
        }
    }

    static string NormalizeGuid(string guid) => guid.Trim().ToLowerInvariant();

    public static bool Connected => Runtime.Connection.State == ConnectionState.Connected;

    public static event Action<ConnectionState>? OnStateChanged;
    public static event Action<PacketError>? OnError;

    internal static void RaiseStateChanged(ConnectionState state) =>
        Runtime.RunOnMainThread(() => OnStateChanged?.Invoke(state));

    internal static void RaiseError(PacketError error) =>
        Runtime.RunOnMainThread(() => OnError?.Invoke(error));
}
