using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Validates the saved Unity import independently of the Blender export report.</summary>
public static class MayaAssetValidation
{
    [Serializable] private sealed class ImportReport
    {
        public string unity;
        public int triangles, distanceTriangles, renderers, rigBones, kitModules;
        public Vector3 visualSize;
        public Vector3 cameraPosition, cameraEuler, spawn;
        public float fieldOfView;
        public string displayMode;
        public bool materialsValid, weightsValid, rigsValid, compositionValid;
    }

    public static void RebuildAndRun()
    {
        ContinuousSceneBuilder.Build();
        Run();
    }

    [MenuItem("Climb/Validate Maya import")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        var bot = UnityEngine.Object.FindAnyObjectByType<ClimbBot>();
        if (bot == null || bot.VisualRoot == null) throw new InvalidOperationException("Saved Maya Bot is missing.");
        var group = bot.VisualRoot.GetComponentInChildren<LODGroup>();
        if (group == null || group.GetLODs().Length != 2) throw new InvalidOperationException("Maya needs two detail levels.");
        var renderers = group.GetLODs()[0].renderers.Cast<SkinnedMeshRenderer>().ToArray();
        var distant = group.GetLODs()[1].renderers.Cast<SkinnedMeshRenderer>().ToArray();
        int distanceTriangles = distant.Sum(r => r.sharedMesh.triangles.Length / 3);
        string[] required = { "Head", "UpperArm.L", "Forearm.L", "Hand.L", "UpperArm.R", "Forearm.R", "Hand.R",
            "Thigh.L", "Shin.L", "Foot.L", "Thigh.R", "Shin.R", "Foot.R" };
        var names = bot.VisualRoot.GetComponentsInChildren<Transform>().Select(t => t.name).ToHashSet();
        var bounds = new Bounds();
        bool hasBounds = false;
        int triangles = 0;
        bool materials = true, weights = true;
        foreach (var renderer in renderers)
        {
            var baked = new Mesh();
            renderer.BakeMesh(baked);
            foreach (var vertex in baked.vertices)
            {
                // BakeMesh already includes the inherited model scale.
                Vector3 point = renderer.transform.position + renderer.transform.rotation * vertex;
                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(point);
            }
            UnityEngine.Object.DestroyImmediate(baked);
            var mesh = renderer.sharedMesh;
            triangles += mesh.triangles.Length / 3;
            weights &= mesh.boneWeights.Length == mesh.vertexCount && renderer.bones.All(b => b != null);
            foreach (var weight in mesh.boneWeights)
                weights &= Mathf.Abs(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3 - 1f) < .01f;
            materials &= renderer.sharedMaterials.All(m => m != null && m.shader != null &&
                m.shader.name == "Universal Render Pipeline/Lit");
        }
        var camera = Camera.main;
        var spawn = GameObject.Find("Spawn").transform.position;
        var tower = UnityEngine.Object.FindAnyObjectByType<ContinuousTower>();
        bool composition = (camera.transform.position - new Vector3(5.657357f, -1.85f, -11.56184f)).magnitude < .05f &&
            (spawn - tower.GetStandingPosition(0)).magnitude < .01f && tower.ActivePlatformCount == 32 &&
            tower.ActiveBodyCount == 4 && bot.GetComponent<CharacterController>().height == 1.4f;
        var report = new ImportReport
        {
            unity = Application.unityVersion, triangles = triangles, distanceTriangles = distanceTriangles, renderers = renderers.Length,
            rigBones = renderers.SelectMany(r => r.bones).Distinct().Count(),
            kitModules = Directory.GetFiles("Assets/Art/Environment/Kit", "*.fbx").Length,
            visualSize = bounds.size, cameraPosition = camera.transform.position, cameraEuler = camera.transform.eulerAngles,
            spawn = spawn, fieldOfView = camera.fieldOfView, displayMode = PlayerSettings.fullScreenMode.ToString(),
            materialsValid = materials, weightsValid = weights, rigsValid = required.All(names.Contains), compositionValid = composition
        };
        Directory.CreateDirectory("../docs/evidence/issue15");
        File.WriteAllText("../docs/evidence/issue15/import.json", JsonUtility.ToJson(report, true));
        if (triangles >= 12000 || triangles < 8000 || distanceTriangles >= 4000 || distanceTriangles < 2000 || !materials || !weights || !report.rigsValid || !composition ||
            report.kitModules != 12 || bounds.size.y < 1.45f || bounds.size.y > 1.65f ||
            PlayerSettings.fullScreenMode != FullScreenMode.Windowed || PlayerSettings.defaultScreenWidth != 1080 ||
            PlayerSettings.defaultScreenHeight != 1920)
            throw new InvalidOperationException("Maya import validation failed; inspect docs/evidence/issue15/import.json.");
        Debug.Log($"MAYA_IMPORT_PASS triangles={triangles} renderers={renderers.Length} height={bounds.size.y:F3}m distant={distanceTriangles} kit={report.kitModules}");
    }
}
