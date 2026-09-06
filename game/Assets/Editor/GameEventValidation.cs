using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GameEventValidation
{
    private const string Active = "Climb.EventsValidation";
    private static float lastWrite;
    private static float started;
    private static bool initialized;
    private static bool captured;
    private static int lastSmall, lastMedium, lastSmoke;
    [Serializable] private sealed class Snapshot
    {
        public bool connected;
        public int small;
        public int medium;
        public int smoke;
        public int round;
        public string status;
    }

    static GameEventValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        File.Delete("Logs/issue3-state.json");
        File.Delete("Logs/issue3-finish");
        SessionState.SetBool(Active, true);
        initialized = false;
        captured = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        if (!initialized) { initialized = true; started = Time.realtimeSinceStartup; }
        if (Time.realtimeSinceStartup - lastWrite < 0.1f) return;
        lastWrite = Time.realtimeSinceStartup;
        var client = UnityEngine.Object.FindFirstObjectByType<GameEventClient>();
        var bot = UnityEngine.Object.FindFirstObjectByType<ClimbBot>();
        if (client == null || bot == null) return;
        var snapshot = new Snapshot
        {
            connected = client.Connected, small = client.SmallCount, medium = client.MediumCount,
            smoke = client.SmokeCount, round = bot.RoundIndex + 1, status = bot.RoundStatus
        };
        File.WriteAllText("Logs/issue3-state.json", JsonUtility.ToJson(snapshot));
        if (snapshot.small > lastSmall) ClimbValidation.Capture("Logs/issue3-small.png");
        if (snapshot.medium > lastMedium) ClimbValidation.Capture("Logs/issue3-medium.png");
        if (snapshot.smoke > lastSmoke) ClimbValidation.Capture("Logs/issue3-smoke.png");
        lastSmall = snapshot.small; lastMedium = snapshot.medium; lastSmoke = snapshot.smoke;
        if (!captured && snapshot.small >= 2 && snapshot.medium >= 1 && snapshot.smoke >= 1)
        {
            captured = true;
            ClimbValidation.Capture("Logs/issue3-spawns.png");
        }
        bool timeout = Time.realtimeSinceStartup - started > 120;
        if (!File.Exists("Logs/issue3-finish") && !timeout) return;
        bool passed = !timeout && snapshot.small == 2 && snapshot.medium == 1 && snapshot.smoke == 1;
        Debug.Log($"{(passed ? "PASS" : "FAIL")} Events_fixtures_spawnExactlyThreeKinds " + JsonUtility.ToJson(snapshot));
        SessionState.SetBool(Active, false);
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }
}
