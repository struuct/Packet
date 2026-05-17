using System;
using System.Collections.Generic;
using BepInEx;
using Packet.Behaviors;
using Packet.Models;

namespace Packet.Api;

public static class PacketApi
{
    static readonly List<string> _mods = new();

    public static ModRegistration RegisterMod(BaseUnityPlugin plugin)
    {
        var guid = plugin.Info.Metadata.GUID;
        if (!string.IsNullOrEmpty(guid) && !_mods.Contains(guid))
            _mods.Add(guid);
        return new(guid);
    }

    internal static string[] GetRegisteredMods() => _mods.ToArray();

    public static bool Connected => PacketRuntime.Client.State == ConnectionState.Connected;

    public static event Action<ConnectionState>? OnStateChanged;
    public static event Action<PacketError>? OnError;

    internal static void RaiseStateChanged(ConnectionState state) =>
        PacketRuntime.RunOnMainThread(() => OnStateChanged?.Invoke(state));

    internal static void RaiseError(PacketError error) =>
        PacketRuntime.RunOnMainThread(() => OnError?.Invoke(error));
}
