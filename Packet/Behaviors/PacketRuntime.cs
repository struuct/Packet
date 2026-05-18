using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.RegularExpressions;
using Packet.Logging;
using Packet.Net;
using UnityEngine;

namespace Packet.Behaviors;

internal sealed class PacketRuntime : MonoBehaviour
{
    static PacketRuntime? Instance;
    static readonly ConcurrentQueue<Action> Queue = new();
    static readonly HttpClient VersionHttp = new();
    static bool _outdated;
    float _pingTimer;

    internal static PacketClient Client { get; } = new();

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _ = CheckVersionAsync();
        PacketLog.Info("PacketRuntime ready");
    }

    void Update()
    {
        while (Queue.TryDequeue(out var action))
            action();

        _pingTimer += Time.deltaTime;
        if (_pingTimer >= 45f)
        {
            _pingTimer = 0f;
            Client.Ping();
        }
    }

    void OnDestroy()
    {
        _ = Client.LeaveRoomAsync();
        Instance = null;
    }

    internal static void RunOnMainThread(Action action) => Queue.Enqueue(action);

    internal static void OnRoomJoined() => _ = JoinAsync();

    static async System.Threading.Tasks.Task CheckVersionAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/struuct/Packet/releases/latest");
            req.Headers.TryAddWithoutValidation("User-Agent", Constants.Guid);
            var resp = await VersionHttp.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return;
            var json = await resp.Content.ReadAsStringAsync();
            var match = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
            if (!match.Success) return;
            var latest = match.Groups[1].Value;
            
            if (new Version(latest) > new Version(Constants.Version))
            {
                _outdated = true;
                PacketLog.Warn($"Packet is outdated (v{Constants.Version} -> v{latest}) - update at github.com/struuct/Packet");
                RunOnMainThread(() => NotificationManager.ShowOutdated(Constants.Version, latest));
            }
        }
        catch
        {
            // ignored
        }
    }

    static async System.Threading.Tasks.Task JoinAsync()
    {
        if (_outdated) { PacketLog.Warn("Packet is outdated, skipping connect"); return; }

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
