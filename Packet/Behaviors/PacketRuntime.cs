using System;
using System.Collections.Concurrent;
using Packet.Logging;
using Packet.Net;
using UnityEngine;

namespace Packet.Behaviors;

internal sealed class PacketRuntime : MonoBehaviour
{
    static PacketRuntime? Instance;
    static readonly ConcurrentQueue<Action> Queue = new();

    internal static PacketClient Client { get; } = new();

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        PacketLog.Info("PacketRuntime ready");
    }

    void Update()
    {
        while (Queue.TryDequeue(out var action))
            action();
    }

    void OnDestroy()
    {
        _ = Client.LeaveRoomAsync();
        Instance = null;
    }

    internal static void RunOnMainThread(Action action) => Queue.Enqueue(action);

    internal static void OnRoomJoined() => _ = JoinAsync();

    static async System.Threading.Tasks.Task JoinAsync()
    {
        var room = Photon.Pun.PhotonNetwork.CurrentRoom?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(room)) return;

        string userId = string.Empty;
        for (var i = 0; i < 5; i++)
        {
            userId = Photon.Pun.PhotonNetwork.LocalPlayer?.UserId ?? string.Empty;
            if (!string.IsNullOrEmpty(userId)) break;
            await System.Threading.Tasks.Task.Delay(1000);
        }

        if (string.IsNullOrEmpty(userId)) { PacketLog.Warn("couldn't get user id, skipping connect"); return; }
        _ = Client.JoinRoomAsync(Constants.BackendUrl, userId, room);
    }

    internal static void OnRoomLeft() => _ = Client.LeaveRoomAsync();
}
