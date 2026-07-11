using Photon.Pun;

namespace Packet.Behaviors;

internal sealed class Callbacks : MonoBehaviourPunCallbacks
{
    public override void OnJoinedRoom() => Runtime.OnRoomJoined();
    public override void OnLeftRoom() => Runtime.OnRoomLeft();
}
