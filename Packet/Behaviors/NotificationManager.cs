using TMPro;
using UnityEngine;

namespace Packet.Behaviors;

internal static class NotificationManager
{
    // very basic lackluster (did i use that right)
    internal static void ShowOutdated(string current, string latest)
    {
        var head = GorillaTagger.Instance?.offlineVRRig?.headMesh.transform;
        if (!head) return;

        var go = new GameObject("PacketNotification");
        go.transform.SetParent(head, false);
        go.transform.localPosition = new Vector3(0, 0.14f, 0.30f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.004f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = $"<b>Packet outdated</b>\nv{current} -> v{latest}\ngithub.com/struuct/Packet";
        tmp.color = Color.red;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;

        Object.Destroy(go, 12f);
    }
}