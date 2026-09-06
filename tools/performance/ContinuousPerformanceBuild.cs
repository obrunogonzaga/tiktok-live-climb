using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ContinuousPerformanceBuild
{
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        new GameObject("Performance probe").AddComponent<ContinuousPerformanceProbe>();
        EditorSceneManager.SaveScene(scene,"Assets/Performance/PerformanceProbe.unity");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{"Assets/Performance/PerformanceProbe.unity"}, locationPathName="Builds/ContinuousProbe.app", target=BuildTarget.StandaloneOSX, options=BuildOptions.Development });
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Performance player build failed: "+report.summary.result);
    }
}
