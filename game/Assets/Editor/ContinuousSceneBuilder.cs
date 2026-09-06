using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Integrates the approved art with an independently collidable, streamed climb route.</summary>
public static class ContinuousSceneBuilder
{
    private const string Env = "Assets/Art/Environment/";
    private const string Prefabs = "Assets/Prefabs/Continuous/";

    [MenuItem("Climb/Rebuild continuous game")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/ReferenceStudy.unity");
        Directory.CreateDirectory(Prefabs);
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Masonry tower" || root.name == "Reference Bot" || root.name == "Hero mounting hold" ||
                root.name.StartsWith("Integrated stone hold") || root.name == "Inset neon" || root.name == "Hold bounce")
                Object.DestroyImmediate(root);
        }
        var center = new GameObject("Continuous tower").transform;
        center.position = new Vector3(0, 0, 3);
        var effects = new GameObject("Round gift objects").transform;
        var tower = center.gameObject.AddComponent<ContinuousTower>();
        tower.Configure(center, BuildPlatformPrefab(), BuildBodyPrefab(), effects);
        var spawn = new GameObject("Spawn").transform;
        spawn.position = tower.GetStandingPosition(0);
        var botObject = new GameObject("Bot");
        botObject.transform.position = spawn.position;
        var controller = botObject.AddComponent<CharacterController>();
        controller.height = 1.4f; controller.radius = .3f; controller.stepOffset = .25f;
        controller.skinWidth = .03f; controller.minMoveDistance = 0;
        var visual = new GameObject("Bot visual").transform;
        visual.SetParent(botObject.transform, false);
        visual.localPosition = new Vector3(0, -.72f, 0);
        visual.localRotation = Quaternion.Euler(0, 15, 0);
        var model = Model("Assets/Art/Bot/Bot.fbx", visual);
        model.transform.localScale *= 1.3f;
        RemapBot(model);
        var bot = botObject.AddComponent<ClimbBot>();
        botObject.AddComponent<BotVisualMotion>();
        var camera = Camera.main;
        var approvedCameraPosition = camera.transform.position;
        var approvedCameraRotation = camera.transform.rotation;
        var follow = camera.gameObject.AddComponent<ClimbFollowCamera>();
        follow.Configure(botObject.transform, tower, new Vector3(6.5f, -2.57f, -10.6f), new Vector3(0, 1.43f, 2.4f));
        bot.ConfigureContinuous(tower, spawn, follow, visual, 3f);
        follow.Snap();
        var client = botObject.AddComponent<GameEventClient>();
        client.Configure(bot, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Small.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Medium.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Smoke.prefab"));
        client.ConfigureEffectsRoot(effects);
        // Camera-relative atmosphere retains the approved framing as height and azimuth change.
        var ambience = new GameObject("Following atmosphere").AddComponent<ClimbAtmosphereFollow>();
        var roots = scene.GetRootGameObjects().Where(o => o.name is "Distant cloud bank" or "High wisps" or "Low mist" or "Magenta rim" or "Cold face bounce" or "Moon key").Select(o => o.transform).ToArray();
        foreach (var element in roots)
        {
            var relative = Quaternion.Inverse(approvedCameraRotation) * (element.position - approvedCameraPosition);
            element.SetPositionAndRotation(camera.transform.position + camera.transform.rotation * relative,
                camera.transform.rotation * Quaternion.Inverse(approvedCameraRotation) * element.rotation);
        }
        ambience.Configure(camera.transform, roots);
        foreach (var element in roots)
        {
            var renderer = element.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial.shader.name != "Climb/Cloud Volume") continue;
            string path = Prefabs + element.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(renderer.sharedMaterial); AssetDatabase.CreateAsset(material, path); }
            material.CopyPropertiesFromMaterial(renderer.sharedMaterial);
            material.SetFloat("_Steps", 16); material.SetFloat("_CheapLighting", 1);
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(material);
        }
        PlayerSettings.defaultScreenWidth = 1080; PlayerSettings.defaultScreenHeight = 1920;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Climb.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Climb.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("CONTINUOUS_SCENE_READY: approved art, streamed helix, follow camera, three existing actions");
    }

    private static GameObject BuildBodyPrefab()
    {
        var root = new GameObject("Streamed masonry segment");
        var close = Model(Env + "ContinuousBody.fbx", root.transform);
        var distant = Model(Env + "ContinuousBodyLOD.fbx", root.transform);
        close.transform.localRotation = Quaternion.Euler(0, 180, 0) * close.transform.localRotation;
        distant.transform.localRotation = Quaternion.Euler(0, 180, 0) * distant.transform.localRotation;
        RemapStone(close); RemapStone(distant);
        var renderers = close.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        if (Mathf.Abs(bounds.size.y - 7.8f) > .05f)
            throw new InvalidOperationException($"Body FBX must import in metres; observed height={bounds.size.y}");
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(.42f, close.GetComponentsInChildren<Renderer>()), new LOD(.045f, distant.GetComponentsInChildren<Renderer>()) });
        lod.fadeMode = LODFadeMode.CrossFade; lod.animateCrossFading = false; lod.RecalculateBounds();
        var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 3.9f, 0); collider.size = new Vector3(4.3f, 7.8f, 4.3f);
        return Save(root, "Body.prefab");
    }

    private static GameObject BuildPlatformPrefab()
    {
        var root = new GameObject("Streamed stone platform");
        var support = new GameObject("Stone support").transform; support.SetParent(root.transform, false);
        support.localPosition = new Vector3(0, 0, -.65f); support.localScale = new Vector3(1, 1, 2.5f);
        var ledge = Model(Env + "Ledge.fbx", support); RemapStone(ledge);
        var collider = root.AddComponent<BoxCollider>(); collider.center = new Vector3(0, -.15f, 0); collider.size = new Vector3(1.4f, .3f, 1.2f);
        var neon = GameObject.CreatePrimitive(PrimitiveType.Cube); neon.name = "Neon"; neon.transform.SetParent(root.transform, false);
        neon.transform.localPosition = new Vector3(0, .025f, .4f); neon.transform.localScale = new Vector3(.68f, .06f, .13f);
        Object.DestroyImmediate(neon.GetComponent<Collider>());
        neon.GetComponent<Renderer>().sharedMaterial = Material("Hold cyan");
        var light = new GameObject("Hold bounce").AddComponent<Light>(); light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0, .25f, .6f); light.type = LightType.Point; light.intensity = .9f; light.range = 2.2f;
        root.AddComponent<ClimbPlatformVisual>().Configure(neon.GetComponent<Renderer>(), light);
        return Save(root, "Platform.prefab");
    }

    private static GameObject Model(string path, Transform parent)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Missing imported model: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static GameObject Save(GameObject root, string name)
    {
        var result = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name);
        Object.DestroyImmediate(root);
        return result;
    }

    private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Env + "Materials/" + name + ".mat");

    private static void RemapStone(GameObject model)
    {
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Material(m.name.StartsWith("Stone") ? m.name : m.name == "Ivy" ? "Ivy" : "Mortar")).ToArray();
    }

    private static void RemapBot(GameObject model)
    {
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Material(m.name.Contains("Orange") ? "Bot orange" : m.name.Contains("Visor") ? "Bot visor" : m.name.Contains("Joint") ? "Bot joints" : m.name.Contains("Metal") ? "Bot metal" : "Bot shell")).ToArray();
    }
}
