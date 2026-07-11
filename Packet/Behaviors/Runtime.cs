using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Packet.Extensions;
using Packet.Logging;
using Packet.Net;
using UnityEngine;

namespace Packet.Behaviors;

internal sealed class Runtime : MonoBehaviour
{
    static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/struuct/Packet/releases/latest");
    static readonly TimeSpan UserIdRetryDelay = TimeSpan.FromSeconds(1);
    static Runtime? Instance;
    static readonly ConcurrentQueue<Action> Queue = new();
    static readonly HttpClient VersionHttp = new();
    static bool _outdated;
    const int UserIdLookupAttempts = 5;
    float _pingTimer;

    internal static Client Connection { get; } = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CheckVersionAsync().Forget("Packet version check");
        PacketLog.Info("Packet runtime ready");
    }

    void Update()
    {
        while (Queue.TryDequeue(out var action))
            action();

        _pingTimer += Time.deltaTime;
        if (_pingTimer >= 90f)
        {
            _pingTimer = 0f;
            Connection.Ping();
        }
    }

    void OnDestroy()
    {
        Connection.LeaveRoomAsync().Forget("Packet shutdown");
        Instance = null;
    }

    internal static void RunOnMainThread(Action action) => Queue.Enqueue(action);

    internal static void OnRoomJoined() => JoinAsync().Forget("Packet room join");

    static async Task CheckVersionAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
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
        catch (Exception ex)
        {
            PacketLog.Warn($"version check failed: {ex.Message}");
        }
    }

    static async Task JoinAsync()
    {
        if (_outdated) { PacketLog.Warn("Packet is outdated, skipping connect"); return; }

        var room = Photon.Pun.PhotonNetwork.CurrentRoom?.Name ?? string.Empty;
        if (string.IsNullOrEmpty(room)) return;

        string userId = string.Empty;
        for (var i = 0; i < UserIdLookupAttempts; i++)
        {
            userId = Photon.Pun.PhotonNetwork.LocalPlayer?.UserId ?? string.Empty;
            if (!string.IsNullOrEmpty(userId)) break;
            await Task.Delay(UserIdRetryDelay);
        }

        if (string.IsNullOrEmpty(userId)) { PacketLog.Warn("couldn't get user id, skipping connect"); return; }
        Connection.JoinRoomAsync(Constants.BackendUrl, userId, room).Forget("Packet room connect");
    }

    internal static void OnRoomLeft() => Connection.LeaveRoomAsync().Forget("Packet room leave");
}
