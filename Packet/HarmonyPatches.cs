using System.Reflection;

namespace Packet;

internal static class HarmonyPatches
{
    static HarmonyLib.Harmony? _harmony;

    internal static void Apply()
    {
        _harmony = new HarmonyLib.Harmony(Constants.Guid);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    internal static void Remove() => _harmony?.UnpatchSelf();
}