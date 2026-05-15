using System.Collections;
using UnityEngine;

namespace Packet.Behaviors;

internal sealed class Heartbeat : MonoBehaviour
{
    void Start() => StartCoroutine(Loop());

    static IEnumerator Loop()
    {
        var wait = new WaitForSeconds(25f);
        while (true)
        {
            yield return wait;
            PacketRuntime.Client.Ping();
        }
    }
}
