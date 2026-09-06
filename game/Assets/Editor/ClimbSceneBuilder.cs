using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ClimbSceneBuilder
{
    [MenuItem("Climb/Rebuild greybox scene")]
    public static void Build()
    {
        GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Materials");
        var concrete = Material("Concrete", new Color(0.48f, 0.51f, 0.55f));
        var botMaterial = Material("Bot", new Color(0.85f, 0.88f, 0.9f));
        var obstacleMaterial = Material("Obstacle", new Color(0.32f, 0.34f, 0.37f));
        Cube("Ground", new Vector3(0, -0.2f, 0), new Vector3(7, 0.4f, 4), concrete);
        var spawn = new GameObject("Spawn").transform;
        spawn.position = new Vector3(-2.9f, 0.72f, 0);
        var route = new GameObject("Waypoints").transform;
        var waypoints = new Transform[8];
        for (int i = 0; i < waypoints.Length; i++)
        {
            float x = -2.1f + i * 0.6f;
            float surface = (i + 1) * 0.8f;
            Cube($"Platform {i + 1:00}", new Vector3(x, surface - 0.15f, 0),
                new Vector3(1.2f, 0.3f, 3), concrete);
            waypoints[i] = new GameObject($"Waypoint {i + 1:00}").transform;
            waypoints[i].SetParent(route);
            waypoints[i].position = new Vector3(x, surface + 0.72f, 0);
        }
        Cube("Obstacle", new Vector3(-2.5f, 0.3f, 0), new Vector3(0.25f, 0.6f, 1), obstacleMaterial);
        var summit = new GameObject("Summit");
        summit.transform.position = waypoints[7].position;
        var trigger = summit.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.2f, 0.8f, 2);
        var rigidbody = summit.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        var bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bot.name = "Bot";
        Object.DestroyImmediate(bot.GetComponent<CapsuleCollider>());
        bot.transform.position = spawn.position;
        // Keep the controller's transform unscaled; scale only the primitive visual.
        var mesh = new GameObject("Capsule");
        mesh.transform.SetParent(bot.transform, false);
        mesh.transform.localScale = new Vector3(0.6f, 0.7f, 0.6f);
        mesh.AddComponent<MeshFilter>().sharedMesh = bot.GetComponent<MeshFilter>().sharedMesh;
        mesh.AddComponent<MeshRenderer>().sharedMaterial = botMaterial;
        Object.DestroyImmediate(bot.GetComponent<MeshFilter>());
        Object.DestroyImmediate(bot.GetComponent<MeshRenderer>());
        var controller = bot.AddComponent<CharacterController>();
        controller.height = 1.4f;
        controller.radius = 0.3f;
        controller.stepOffset = 0.85f;
        controller.minMoveDistance = 0;
        controller.skinWidth = 0.03f;
        bot.AddComponent<ClimbBot>().Configure(spawn, waypoints);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 6.5f;
        camera.aspect = 1080f / 1920f;
        camera.transform.position = new Vector3(0.5f, 7.2f, -18);
        camera.transform.LookAt(new Vector3(0.5f, 3.8f, 0));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
        camera.gameObject.AddComponent<AudioListener>();
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.9f;
        light.transform.rotation = Quaternion.Euler(40, -25, 0);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        PlayerSettings.defaultScreenWidth = 1080;
        PlayerSettings.defaultScreenHeight = 1920;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.productName = "TikTok LIVE Climb";
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Climb.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Climb.unity", true) };
        AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
        AssetDatabase.DeleteAsset("Assets/Readme.asset");
        AssetDatabase.DeleteAsset("Assets/TutorialInfo");
        AssetDatabase.SaveAssets();
        Debug.Log("CLIMB_SCENE_READY: 8 platforms, URP, 1080x1920 Windowed");
    }

    private static Material Material(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        return material;
    }

    private static void Cube(string name, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }
}
