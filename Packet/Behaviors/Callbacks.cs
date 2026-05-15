using Photon.Pun;

namespace Packet.Behaviors;

internal sealed class Callbacks : MonoBehaviourPunCallbacks
{
    public override void OnJoinedRoom() => PacketRuntime.OnRoomJoined();
    public override void OnLeftRoom() => PacketRuntime.OnRoomLeft();
}
