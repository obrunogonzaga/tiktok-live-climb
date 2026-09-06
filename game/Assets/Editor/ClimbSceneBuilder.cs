using UnityEditor;

/// <summary>Keeps the original rebuild entry point on the current approved game architecture.</summary>
public static class ClimbSceneBuilder
{
    [MenuItem("Climb/Rebuild tower composition")]
    public static void Build() => ContinuousSceneBuilder.Build();
}
