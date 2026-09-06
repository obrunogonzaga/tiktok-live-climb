using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Records actual Unity PlayMode frames and checks route completion independently of the Bot.</summary>
[InitializeOnLoad]
public static class CompositionValidation
{
    private const string Active = "CompositionValidation.Active";
    private const int FrameRate = 15;
    private static readonly HashSet<int> Landings = new();
    private static int lastFrame = -1;
    private static int frames;
    private static float started;
    private static float startGame;
    private static int firstRound;
    private static bool capturedStart;
    private static bool pendingMiddle, pendingSummit;
    private static bool ordered = true, sawSummit;
    private static bool middle;
    private static bool summit;
    private static Vector2 screenMin = Vector2.positiveInfinity;
    private static Vector2 screenMax = Vector2.negativeInfinity;
    private static Vector3 cameraPosition;
    private static Quaternion cameraRotation;
    private static bool cameraMoved;
    private static string Output => Path.GetFullPath("../docs/evidence/issue14");

    static CompositionValidation() { EditorApplication.update += Tick; }

    [MenuItem("Climb/Validate composition")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop PlayMode before validation.");
        Landings.Clear(); frames = 0; lastFrame = -1;
        capturedStart = middle = summit = pendingMiddle = pendingSummit = cameraMoved = sawSummit = false;
        ordered = true; screenMin = Vector2.positiveInfinity; screenMax = Vector2.negativeInfinity;
        EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory("Logs/issue14-frames");
        foreach (var frame in Directory.GetFiles("Logs/issue14-frames", "*.png")) File.Delete(frame);
        File.WriteAllText(Output + "/measurements.txt", "Unity camera render 1080x1920; top-left projected renderer bounds (conservative).\n");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        var bot = UnityEngine.Object.FindFirstObjectByType<ClimbBot>();
        if (bot == null || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        if (frames == 0)
        {
            started = Time.realtimeSinceStartup;
            startGame = Time.time;
            firstRound = bot.RoundIndex;
            Time.captureDeltaTime = 1f / FrameRate;
            cameraPosition = Camera.main.transform.position;
            cameraRotation = Camera.main.transform.rotation;
            bot.gameObject.AddComponent<CompositionProbe>();
        }
        sawSummit |= bot.RoundStatus == "summit";
        var rect = ProjectBot(bot);
        screenMin = Vector2.Min(screenMin, rect.min);
        screenMax = Vector2.Max(screenMax, rect.max);
        cameraMoved |= Camera.main.transform.position != cameraPosition || Camera.main.transform.rotation != cameraRotation;
        var framePath = $"Logs/issue14-frames/{frames:00000}.png";
        ClimbValidation.Capture(framePath);
        if (!capturedStart) { capturedStart = true; SaveRecordedStage("start", framePath, bot); }
        if (pendingMiddle) { pendingMiddle = false; SaveRecordedStage("middle", framePath, bot); }
        if (pendingSummit) { pendingSummit = false; SaveRecordedStage("summit", framePath, bot); }
        frames++;
        if (Time.time - startGame < 30 || Time.realtimeSinceStartup - started < 30) return;
        bool safe = screenMin.x >= 24 && screenMax.x <= 940 && screenMin.y >= 240 && screenMax.y <= 1260;
        bool passed = capturedStart && Landings.Count == 8 && middle && summit && ordered && sawSummit && bot.RoundIndex > firstRound && safe && !cameraMoved &&
            !Camera.main.orthographic && PlayerSettings.fullScreenMode == FullScreenMode.Windowed &&
            PlayerSettings.defaultScreenWidth == 1080 && PlayerSettings.defaultScreenHeight == 1920 &&
            bot.GetComponent<GameEventClient>() != null;
        File.AppendAllText(Output + "/measurements.txt",
            $"{(passed ? "PASS" : "FAIL")} Composition_play30Seconds_landsOnEightPlatformsAndResets gameSeconds={Time.time-startGame:F2} realSeconds={Time.realtimeSinceStartup-started:F2} rounds={firstRound}->{bot.RoundIndex} landings={Landings.Count}/8 frames={frames} fps={FrameRate}\n" +
            $"all-frame bounds={screenMin}..{screenMax} safe={safe} cameraFixed={!cameraMoved} ordered={ordered} observedSummit={sawSummit}\n");
        Time.captureDeltaTime = 0;
        SessionState.SetBool(Active, false);
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    public static void ObserveLanding(ClimbBot bot)
    {
        if (bot.GetComponent<CharacterController>().isGrounded)
        {
            for (int i = 0; i < 8; i++)
            {
                var target = GameObject.Find($"Waypoint {i + 1:00}").transform.position;
                if (Mathf.Abs(bot.transform.position.y - target.y) > 0.16f) continue;
                var delta = bot.transform.position - target; delta.y = 0;
                if (delta.magnitude > 0.4f) continue;
                if (!Physics.Raycast(bot.transform.position, Vector3.down, out var support, 1f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || support.collider.name != $"Platform {i + 1:00}") continue;
                if (!Landings.Contains(i + 1)) ordered &= i + 1 == Landings.Count + 1;
                if (Landings.Add(i + 1))
                    File.AppendAllText(Output + "/measurements.txt", $"landed platform={i + 1} t={Time.time:F2} position={bot.transform.position}\n");
                if (i == 3 && !middle) { middle = true; pendingMiddle = true; }
                if (i == 7 && !summit) { summit = true; pendingSummit = true; }
            }
        }
    }

    private static Rect ProjectBot(ClimbBot bot)
    {
        Camera.main.aspect = 1080f / 1920f;
        var bounds = bot.GetComponentInChildren<Renderer>().bounds;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var p = Camera.main.WorldToViewportPoint(corner);
            var pixel = new Vector2(p.x * 1080, (1 - p.y) * 1920);
            min = Vector2.Min(min, pixel); max = Vector2.Max(max, pixel);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static void SaveRecordedStage(string stage, string framePath, ClimbBot bot)
    {
        File.Copy(framePath, Output + "/" + stage + ".png", true);
        var rect = ProjectBot(bot);
        File.AppendAllText(Output + "/measurements.txt", $"{stage}: frame={frames} position={bot.transform.position} bounds={rect.min}..{rect.max} height={rect.height:F1}px\n");
    }
}

// Observe each physics step: a landing can be followed by a jump between recorded frames.
[DefaultExecutionOrder(1000)]
public sealed class CompositionProbe : MonoBehaviour
{
    private ClimbBot bot;
    private void Awake() { bot = GetComponent<ClimbBot>(); }
    private void FixedUpdate() { CompositionValidation.ObserveLanding(bot); }
}
