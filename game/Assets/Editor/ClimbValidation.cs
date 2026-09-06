using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class ClimbValidation
{
    private const string ActiveKey = "ClimbValidation.Active";
    private static readonly HashSet<ClimbBot.BotState> States = new();
    private static float started;
    private static float maxHeight;
    private static int firstRound;
    private static bool initialized;
    private static bool captured;

    static ClimbValidation()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Climb/Validate 30 seconds")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop PlayMode before validation.");
        if (PlayerSettings.defaultScreenWidth != 1080 || PlayerSettings.defaultScreenHeight != 1920 ||
            PlayerSettings.fullScreenMode != FullScreenMode.Windowed || !PlayerSettings.runInBackground)
            throw new InvalidOperationException("Player settings do not match issue #2.");
        EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        if (UnityEngine.Object.FindFirstObjectByType<ContinuousTower>() != null)
        {
            ContinuousValidation.Run();
            return;
        }
        SessionState.SetBool(ActiveKey, true);
        initialized = false;
        captured = false;
        States.Clear();
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying)
            return;
        var bot = UnityEngine.Object.FindFirstObjectByType<ClimbBot>();
        if (bot == null)
            return;
        if (!initialized)
        {
            initialized = true;
            started = Time.realtimeSinceStartup;
            firstRound = bot.RoundIndex;
            maxHeight = bot.transform.position.y;
        }
        EditorApplication.QueuePlayerLoopUpdate();
        States.Add(bot.State);
        maxHeight = Mathf.Max(maxHeight, bot.transform.position.y);
        float elapsed = Time.realtimeSinceStartup - started;
        if (!captured && elapsed > 8)
        {
            captured = true;
            Capture();
        }
        if (elapsed < 30)
            return;
        bool passed = maxHeight > 6 && bot.RoundIndex > firstRound &&
            States.Contains(ClimbBot.BotState.Climb) && States.Contains(ClimbBot.BotState.Avoid);
        string report = $"{(passed ? "PASS" : "FAIL")} Climb_play30Seconds_climbsAndResets " +
            $"elapsed={elapsed:F2}s maxY={maxHeight:F2} firstRound={firstRound} round={bot.RoundIndex} " +
            $"states={string.Join(",", States)} position={bot.transform.position} " +
            $"gameTime={Time.time} frames={Time.frameCount} paused={EditorApplication.isPaused}";
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/issue2-playmode.txt", report + Environment.NewLine);
        Debug.Log(report);
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode)
            EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    public static double LastRenderMilliseconds { get; private set; }
    public static double LastReadbackMilliseconds { get; private set; }
    public static double LastEncodeMilliseconds { get; private set; }

    public static void Capture(string path = "Logs/issue2-playmode.png")
    {
        var camera = Camera.main;
        var target = new RenderTexture(1080, 1920, 24);
        var previous = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        float previousAspect = camera.aspect;
        var texture = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        try
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            camera.aspect = 1080f / 1920f;
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else
            {
                camera.targetTexture = target;
                camera.Render();
            }
            LastRenderMilliseconds = timer.Elapsed.TotalMilliseconds; timer.Restart();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            texture.Apply();
            LastReadbackMilliseconds = timer.Elapsed.TotalMilliseconds; timer.Restart();
            var bytes = texture.EncodeToPNG();
            LastEncodeMilliseconds = timer.Elapsed.TotalMilliseconds;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
