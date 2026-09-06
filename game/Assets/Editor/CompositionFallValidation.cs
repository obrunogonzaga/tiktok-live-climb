using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CompositionFallValidation
{
    private const string Active = "CompositionFallValidation.Active";
    private static bool injected, sawFall;
    private static int firstRound;
    private static float started;
    static CompositionFallValidation() { EditorApplication.update += Tick; }

    [MenuItem("Climb/Validate fall reset")]
    public static void Run()
    {
        injected = sawFall = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        var bot = Object.FindFirstObjectByType<ClimbBot>();
        if (bot == null) return;
        if (!injected)
        {
            firstRound = bot.RoundIndex;
            started = Time.realtimeSinceStartup;
            var controller = bot.GetComponent<CharacterController>();
            controller.enabled = false;
            bot.transform.position = GameObject.Find("Spawn").transform.position + Vector3.down * 3;
            controller.enabled = true;
            Physics.SyncTransforms();
            injected = true;
        }
        sawFall |= bot.RoundStatus == "fell";
        bool passed = sawFall && bot.RoundIndex == firstRound + 1 && bot.State == ClimbBot.BotState.Climb &&
            Vector3.Distance(bot.transform.position, GameObject.Find("Spawn").transform.position) < 1f;
        if (!passed && Time.realtimeSinceStartup - started < 5) return;
        Directory.CreateDirectory("../docs/evidence/issue14");
        File.WriteAllText("../docs/evidence/issue14/fall-reset.txt",
            $"{(passed ? "PASS" : "FAIL")} Composition_belowSpawn_fallsAndResets injectedY=spawnY-3 observedFell={sawFall} rounds={firstRound}->{bot.RoundIndex} state={bot.State} position={bot.transform.position}\n");
        SessionState.SetBool(Active, false);
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }
}
