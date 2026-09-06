using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ContinuousValidation
{
    private const string Active = "Climb.ContinuousValidation";
    private static bool initialized;
    private static int frames, lastFrame = -1, firstRound, lastIndex;
    private static float realStart, gameStart, lastProgress, maxHeight;
    private static bool injectedFall, sawFell;
    private static Vector3 spawnPosition;
    private static Rigidbody rebaseProbe;
    private static double probeHeight;
    private static bool originStable;
    private static float maxPoolCoordinate;
    private static double renderMs, readbackMs, encodeMs;
    private static int renderSamples;
    private static string Output => Path.GetFullPath("../docs/evidence/continuous");
    private static float Duration => float.TryParse(Environment.GetEnvironmentVariable("CLIMB_SECONDS"), out var v) ? v : 45;
    private static bool Record => Environment.GetEnvironmentVariable("CLIMB_RECORD") == "1";
    private static bool FallTest => Environment.GetEnvironmentVariable("CLIMB_FALL") == "1";

    static ContinuousValidation() { EditorApplication.update += Tick; }

    [MenuItem("Climb/Validate continuous game")]
    public static void Run()
    {
        initialized = injectedFall = sawFell = false; originStable = true; rebaseProbe = null; frames = 0; lastFrame = -1; maxHeight = maxPoolCoordinate = 0;
        EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        Directory.CreateDirectory(Output); Directory.CreateDirectory("Logs/continuous-frames");
        foreach (var f in Directory.GetFiles("Logs/continuous-frames", "*.png")) File.Delete(f);
        File.WriteAllText(Output + "/run.txt", $"Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName}, 1080x1920; record={Record}; duration={Duration}; fallTest={FallTest}\n");
        var tower = UnityEngine.Object.FindFirstObjectByType<ContinuousTower>();
        if (float.TryParse(Environment.GetEnvironmentVariable("CLIMB_REBASE"), out var threshold))
        {
            var serialized = new SerializedObject(tower);
            serialized.FindProperty("rebaseHeight").floatValue = threshold;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        var bot = UnityEngine.Object.FindFirstObjectByType<ClimbBot>();
        var tower = UnityEngine.Object.FindFirstObjectByType<ContinuousTower>();
        if (bot == null || tower == null) return;
        if (!initialized)
        {
            initialized = true; originStable = true; renderMs = readbackMs = encodeMs = 0; renderSamples = 0; realStart = lastProgress = Time.realtimeSinceStartup; gameStart = Time.time;
            firstRound = bot.RoundIndex; lastIndex = tower.HighestLandedRouteIndex;
            spawnPosition = GameObject.Find("Spawn").transform.position;
            if (Environment.GetEnvironmentVariable("CLIMB_REBASE") != null && !FallTest)
            {
                var probe = new GameObject("Rebase verification object");
                probe.transform.SetParent(tower.EffectsRoot, false);
                probe.transform.position = bot.transform.position + Vector3.up * 2;
                rebaseProbe = probe.AddComponent<Rigidbody>(); rebaseProbe.isKinematic = true; rebaseProbe.useGravity = false;
                probeHeight = rebaseProbe.position.y + tower.LogicalOriginHeight;
            }
            if (Record) Time.captureDeltaTime = 1f / 15;
            var bounds = new Bounds(bot.VisualRoot.position, Vector3.zero);
            foreach(var renderer in bot.VisualRoot.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            File.AppendAllText(Output + "/run.txt", $"camera={Camera.main.transform.position} euler={Camera.main.transform.eulerAngles} botViewport={Camera.main.WorldToViewportPoint(bot.transform.position)} visualBounds={bounds} renderers={bot.VisualRoot.GetComponentsInChildren<Renderer>().Length} visualViewport={Camera.main.WorldToViewportPoint(bounds.center)}\n");
        }
        if (tower.HighestLandedRouteIndex != lastIndex)
        {
            lastIndex = tower.HighestLandedRouteIndex; lastProgress = Time.realtimeSinceStartup;
            File.AppendAllText(Output + "/run.txt", $"landed={lastIndex} height={tower.CurrentHeight:F2} record={tower.RecordHeight:F2} worldY={bot.transform.position.y:F2} t={Time.time-gameStart:F2} round={bot.RoundIndex} recycled={tower.Recycles} rebases={tower.Rebases}\n");
        }
        maxHeight = Mathf.Max(maxHeight, tower.CurrentHeight);
        maxPoolCoordinate = Mathf.Max(maxPoolCoordinate, tower.MaxActivePoolLocalHeight);
        originStable &= tower.PoolPositionsFinite && tower.MaxActivePoolLocalHeight < 256;
        if (rebaseProbe != null)
        {
            bool validOrigin = Math.Abs(rebaseProbe.position.y + tower.LogicalOriginHeight - probeHeight) < .01 && Mathf.Abs(tower.EffectsRoot.position.y) < .01f;
            if (originStable && !validOrigin)
                File.AppendAllText(Output + "/run.txt", $"ORIGIN_PROBE expected={probeHeight} rigidbody={rebaseProbe.position} transform={rebaseProbe.transform.position} origin={tower.LogicalOriginHeight} effectsRoot={tower.EffectsRoot.position} rebases={tower.Rebases}\n");
            originStable &= validOrigin;
        }
        if (Record)
        {
            ClimbValidation.Capture($"Logs/continuous-frames/{frames:00000}.png");
            if (frames >= 15) { renderMs += ClimbValidation.LastRenderMilliseconds; readbackMs += ClimbValidation.LastReadbackMilliseconds; encodeMs += ClimbValidation.LastEncodeMilliseconds; renderSamples++; }
        }
        if (frames == 0) ClimbValidation.Capture(Output + "/start.png");
        if (frames == 120) ClimbValidation.Capture(Output + "/middle.png");
        frames++;
        float elapsed = Time.time - gameStart;
        if (FallTest && !injectedFall && maxHeight > (float.TryParse(Environment.GetEnvironmentVariable("CLIMB_FALL_AT"), out var fallAt) ? fallAt : 8))
        {
            injectedFall = true;
            var controller = bot.GetComponent<CharacterController>(); controller.enabled = false;
            bot.transform.position += Vector3.down * 5;
            controller.enabled = true; Physics.SyncTransforms();
        }
        sawFell |= bot.RoundStatus == "fell";
        bool stalled = Time.realtimeSinceStartup - lastProgress > 10;
        if (!stalled && elapsed < Duration && !(injectedFall && bot.RoundIndex > firstRound)) return;
        int platformObjects = UnityEngine.Object.FindObjectsByType<ClimbPlatformVisual>(FindObjectsSortMode.None).Length;
        bool bounded = platformObjects == tower.ActivePlatformCount && platformObjects == 32 && tower.ActiveBodyCount == 4;
        bool passed = originStable && !stalled && maxHeight > (FallTest ? 8 : 10) && bounded && tower.RecordHeight >= tower.CurrentHeight;
        if (FallTest) passed &= sawFell && bot.RoundIndex == firstRound + 1 && tower.CurrentHeight < 2 && tower.RecordHeight >= maxHeight;
        else passed &= bot.RoundIndex == firstRound && tower.Recycles > 0;
        if (Environment.GetEnvironmentVariable("CLIMB_REBASE") != null && !FallTest) passed &= tower.Rebases > 0;
        ClimbValidation.Capture(Output + "/finish.png");
        File.AppendAllText(Output + "/run.txt", $"{(passed ? "PASS" : "FAIL")} Continuous_play_climbsWithBoundedPools elapsedGame={elapsed:F2} elapsedReal={Time.realtimeSinceStartup-realStart:F2} maxHeight={maxHeight:F2} round={bot.RoundIndex} platforms={platformObjects}/{tower.ActivePlatformCount} bodies={tower.ActiveBodyCount} recycles={tower.Recycles} rebases={tower.Rebases} stalled={stalled} sawFell={sawFell} state={bot.State} position={bot.transform.position} target={tower.GetStandingPosition(tower.HighestLandedRouteIndex+1)} origin={tower.OriginOffset} originStable={originStable} maxPoolLocalY={maxPoolCoordinate}\n");
        if (renderSamples > 0) File.AppendAllText(Output + "/run.txt", $"Capture timings (Editor, not player FPS): render={renderMs/renderSamples:F1}ms readback={readbackMs/renderSamples:F1}ms PNG={encodeMs/renderSamples:F1}ms n={renderSamples}\n");
        Time.captureDeltaTime = 0; SessionState.SetBool(Active, false); EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }
}
