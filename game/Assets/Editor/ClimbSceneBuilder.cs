using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ClimbSceneBuilder
{
    [MenuItem("Climb/Rebuild tower composition")]
    public static void Build()
    {
        GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Materials");
        var concrete = Material("Concrete", new Color(0.227f, 0.255f, 0.4f));
        var botMaterial = Material("Bot", new Color(0.91f, 0.925f, 0.925f));
        var obstacleMaterial = Material("Obstacle", new Color(0.32f, 0.34f, 0.37f));
        var cyan = Material("Cyan", new Color(0.031f, 0.843f, 1f));
        var magenta = Material("Magenta", new Color(0.643f, 0.09f, 0.957f));
        Cube("Ground", new Vector3(0, -0.2f, 1.4f), new Vector3(7, 0.4f, 6), concrete);
        BuildTower(concrete);
        var spawn = new GameObject("Spawn").transform;
        spawn.position = new Vector3(-0.2f, 0.72f, -1.1f);
        var route = new GameObject("Waypoints").transform;
        var waypoints = new Transform[8];
        for (int i = 0; i < waypoints.Length; i++)
        {
            float x = i % 2 == 0 ? -0.55f : 0.85f;
            float z = i * 0.6f;
            float surface = (i + 1) * 0.65f;
            Cube($"Platform {i + 1:00}", new Vector3(x, surface - 0.15f, z + 0.225f),
                new Vector3(0.9f, 0.3f, 1.65f), concrete);
            Cube($"Hold {i + 1:00}", new Vector3(x, surface - 0.08f, z - 0.62f),
                new Vector3(0.8f, 0.12f, 0.08f), i % 2 == 0 ? cyan : magenta);
            waypoints[i] = new GameObject($"Waypoint {i + 1:00}").transform;
            waypoints[i].SetParent(route);
            waypoints[i].position = new Vector3(x, surface + 0.72f, z - 0.3f);
        }
        Cube("Obstacle", new Vector3(-3.1f, 0.2f, 0), new Vector3(0.15f, 0.4f, 0.6f), obstacleMaterial);
        var summit = new GameObject("Summit");
        summit.transform.position = waypoints[7].position + Vector3.up * 0.65f;
        var trigger = summit.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(0.35f, 0.1f, 0.35f);
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
        var climbBot = bot.AddComponent<ClimbBot>();
        climbBot.Configure(spawn, waypoints);
        var movement = new SerializedObject(climbBot);
        movement.FindProperty("moveSpeed").floatValue = 2.8f;
        movement.ApplyModifiedPropertiesWithoutUndo();
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = false;
        camera.fieldOfView = 23;
        camera.orthographicSize = 6.5f;
        camera.aspect = 1080f / 1920f;
        camera.transform.position = new Vector3(9, 1, -30);
        camera.transform.LookAt(new Vector3(0.4f, 1.9f, 0));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.004f, 0.055f, 0.204f);
        camera.gameObject.AddComponent<AudioListener>();
        var light = new GameObject("Cool fill").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.65f;
        light.color = new Color(0.65f, 0.8f, 1f);
        light.transform.rotation = Quaternion.Euler(20, -35, 0);
        RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.48f);
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
        GameEventSetup.Install();
        Debug.Log("CLIMB_SCENE_READY: 8 platforms, URP, 1080x1920 Windowed");
    }

    private static void BuildTower(Material stone)
    {
        Directory.CreateDirectory("Assets/Meshes");
        const string path = "Assets/Meshes/Tower.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        var corners = new[] {
            new Vector3(-2.7f, -3.2f, -2.453846f), new Vector3(2.7f, -3.2f, -2.453846f),
            new Vector3(2.7f, 14.4f, 13.792308f), new Vector3(-2.7f, 14.4f, 13.792308f),
            new Vector3(-2.7f, -3.2f, 17), new Vector3(2.7f, -3.2f, 17),
            new Vector3(2.7f, 14.4f, 17), new Vector3(-2.7f, 14.4f, 17)
        };
        int[] faces = { 0, 3, 2, 1, 1, 2, 6, 5, 5, 6, 7, 4, 4, 7, 3, 0, 3, 7, 6, 2, 4, 0, 1, 5 };
        var vertices = new Vector3[24]; var triangles = new int[36];
        for (int face = 0; face < 6; face++)
        {
            int v = face * 4, t = face * 6;
            for (int i = 0; i < 4; i++) vertices[v + i] = corners[faces[v + i]];
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh.vertices = vertices; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var tower = new GameObject("Tower core");
        tower.AddComponent<MeshFilter>().sharedMesh = mesh;
        tower.AddComponent<MeshRenderer>().sharedMaterial = stone;
        tower.AddComponent<MeshCollider>().sharedMesh = mesh;
        // A continuous raked face follows the route without ceilings over the jumps.
        var rake = Quaternion.Euler(42.7094f, 0, 0);
        for (int row = -4; row < 23; row++)
        {
            float y = row * 0.65f + 0.325f;
            for (int col = 0; col < 5; col++)
                Cube($"Facade {row + 4:00}-{col}", new Vector3(-2.16f + col * 1.08f, y, 0.5f + y * (0.6f / 0.65f) - 0.05f),
                    new Vector3(1.04f, 0.845f, 0.12f), stone).transform.rotation = rake;
        }
        foreach (float y in new[] { -0.15f, 2.6f, 5.2f })
            Cube("Facade belt", new Vector3(0, y, 0.5f + y * (0.6f / 0.65f) - 0.08f),
                new Vector3(5.7f, 0.18f, 0.35f), stone);
        foreach (float x in new[] { -2.7f, 2.7f })
            Cube("Corner pier", new Vector3(x, 5.6f, 5.669231f), new Vector3(0.3f, 24, 0.3f), stone).transform.rotation = rake;
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

    private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }
}
