using BepInEx;
using Packet.Behaviors;
using Photon.Pun;
using static Packet.HarmonyPatches;

namespace Packet;

[BepInPlugin(Constants.Guid, Constants.ModName, Constants.Version)]
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        GorillaTagger.OnPlayerSpawned(Init);
    }

    private void Init()
    {
        Apply();

        gameObject.AddComponent<PacketRuntime>();
        gameObject.AddComponent<Heartbeat>();
        gameObject.AddComponent<Callbacks>();
    }

    private void OnDestroy() => Remove();
}