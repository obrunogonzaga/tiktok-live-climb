using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameEventClient : MonoBehaviour
{
    [SerializeField] private ClimbBot bot;
    [SerializeField] private GameObject smallPrefab;
    [SerializeField] private GameObject mediumPrefab;
    [SerializeField] private GameObject smokePrefab;
    [SerializeField] private Transform effectsRoot;
    [SerializeField] private int smallCount;
    [SerializeField] private int mediumCount;
    [SerializeField] private int smokeCount;
    public int SmallCount => smallCount;
    public int MediumCount => mediumCount;
    public int SmokeCount => smokeCount;
    public bool Connected => connected;
    public Transform EffectsRoot => effectsRoot;

    private readonly ConcurrentQueue<string> received = new();
    private CancellationTokenSource lifetime;
    private volatile bool connected;
    private string latestRound;
    private string pendingRound;
    private Task connectionTask;
    private ContinuousTower progressTower;

    [Serializable] private sealed class Command { public string action; }
    [Serializable] private sealed class Round { public int index; public string status; }
    [Serializable] private sealed class ProgressRound
    {
        public int index;
        public string status;
        public int height;
        public int record;
    }

    public void Configure(ClimbBot target, GameObject small, GameObject medium, GameObject smoke)
    {
        bot = target;
        smallPrefab = small;
        mediumPrefab = medium;
        smokePrefab = smoke;
        if (effectsRoot == null && bot != null && bot.ContinuousMode)
            effectsRoot = UnityEngine.Object.FindFirstObjectByType<ContinuousTower>()?.EffectsRoot;
    }

    public void Configure(ClimbBot target, GameObject small, GameObject medium, GameObject smoke, Transform dynamicEffectsRoot)
    {
        Configure(target, small, medium, smoke);
        ConfigureEffectsRoot(dynamicEffectsRoot);
    }

    public void ConfigureEffectsRoot(Transform dynamicEffectsRoot)
    {
        effectsRoot = dynamicEffectsRoot;
    }

    private void OnEnable()
    {
        lifetime = new CancellationTokenSource();
        connectionTask = ConnectLoop(lifetime.Token);
    }

    private void OnDisable()
    {
        lifetime?.Cancel();
        lifetime?.Dispose();
        connected = false;
        while (received.TryDequeue(out _)) { }
    }

    private void Update()
    {
        if (bot == null) return;
        // The bot counts completed rounds from zero; OverlayState numbers the current round from one.
        if (progressTower == null && bot.ContinuousMode) progressTower = UnityEngine.Object.FindFirstObjectByType<ContinuousTower>();
        var tower = progressTower;
        string round = tower != null && bot.ContinuousMode
            ? JsonUtility.ToJson(new ProgressRound { index = bot.RoundIndex + 1, status = bot.RoundStatus,
                height = Mathf.FloorToInt(tower.CurrentHeight), record = Mathf.FloorToInt(tower.RecordHeight) })
            : JsonUtility.ToJson(new Round { index = bot.RoundIndex + 1, status = bot.RoundStatus });
        if (round != latestRound)
        {
            latestRound = round;
            Interlocked.Exchange(ref pendingRound, round);
        }
        for (int i = 0; i < 32 && received.TryDequeue(out string message); i++)
            ApplyCommand(message);
    }

    private async Task ConnectLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                await socket.ConnectAsync(new Uri("ws://127.0.0.1:8766/game"), token).ConfigureAwait(false);
                connected = true;
                Interlocked.Exchange(ref pendingRound, latestRound);
                Task receive = Receive(socket, connection.Token);
                Task send = Send(socket, connection.Token);
                await Task.WhenAny(receive, send).ConfigureAwait(false);
                connection.Cancel();
                socket.Abort();
                await Task.WhenAll(receive, send).ConfigureAwait(false);
            }
            catch (Exception error) when (error is WebSocketException || error is OperationCanceledException ||
                                           error is IOException || error is ObjectDisposedException)
            {
                // Reconnect locally without logging viewer data or network payloads.
            }
            finally { connected = false; }
            if (!token.IsCancellationRequested)
            {
                try { await Task.Delay(1000, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task Receive(ClientWebSocket socket, CancellationToken token)
    {
        byte[] buffer = new byte[16384];
        while (!token.IsCancellationRequested)
        {
            int length = 0;
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), token)
                    .ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return;
                if (result.MessageType != WebSocketMessageType.Text) throw new IOException("Text required");
                length += result.Count;
                if (length == buffer.Length && !result.EndOfMessage) throw new IOException("Message too large");
            } while (!result.EndOfMessage);
            if (received.Count < 128) received.Enqueue(Encoding.UTF8.GetString(buffer, 0, length));
        }
    }

    private async Task Send(ClientWebSocket socket, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            string round = Interlocked.Exchange(ref pendingRound, null);
            if (round != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(round);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token)
                    .ConfigureAwait(false);
            }
            await Task.Delay(50, token).ConfigureAwait(false);
        }
    }

    private void ApplyCommand(string json)
    {
        Command command;
        try { command = JsonUtility.FromJson<Command>(json); }
        catch (ArgumentException) { return; }
        if (command == null) return;
        GameObject prefab;
        switch (command.action)
        {
            case "SpawnObstacle.Small": prefab = smallPrefab; break;
            case "SpawnObstacle.Medium": prefab = mediumPrefab; break;
            case "SpawnSmoke": prefab = smokePrefab; break;
            default: return;
        }
        if (prefab == null) return;
        Vector3 position = bot.transform.position + bot.transform.forward * 0.9f + Vector3.up * 1.2f;
        var instance = effectsRoot != null
            ? Instantiate(prefab, position, Quaternion.identity, effectsRoot)
            : Instantiate(prefab, position, Quaternion.identity);
        instance.name = command.action == "SpawnSmoke" ? "Smoke" : "Obstacle";
        Destroy(instance, 6);
        switch (command.action)
        {
            case "SpawnObstacle.Small": smallCount++; break;
            case "SpawnObstacle.Medium": mediumCount++; break;
            case "SpawnSmoke": smokeCount++; break;
        }
        Debug.Log($"SPAWN {command.action} small={smallCount} medium={mediumCount} smoke={smokeCount}", this);
    }
}
