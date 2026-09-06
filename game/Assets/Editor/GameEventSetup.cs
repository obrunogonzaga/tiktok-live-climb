using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameEventSetup
{
    [MenuItem("Climb/Install event client")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Climb.unity");
        Directory.CreateDirectory("Assets/Prefabs");
        var small = Prefab("Small", PrimitiveType.Cube, 0.45f, false);
        var medium = Prefab("Medium", PrimitiveType.Cube, 0.95f, false);
        var smoke = Prefab("Smoke", PrimitiveType.Sphere, 1.3f, true);
        var bot = Object.FindFirstObjectByType<ClimbBot>();
        var client = bot.GetComponent<GameEventClient>();
        if (client == null) client = bot.gameObject.AddComponent<GameEventClient>();
        client.Configure(bot, small, medium, smoke);
        EditorUtility.SetDirty(client);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("GAME_EVENTS_READY: three prefabs and WS client");
    }

    private static GameObject Prefab(string name, PrimitiveType shape, float size, bool smoke)
    {
        var instance = GameObject.CreatePrimitive(shape);
        instance.name = smoke ? "Smoke" : "Obstacle";
        instance.transform.localScale = Vector3.one * size;
        instance.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            smoke ? "Assets/Materials/Bot.mat" : "Assets/Materials/Obstacle.mat");
        var collider = instance.GetComponent<Collider>();
        collider.isTrigger = smoke;
        if (!smoke) instance.AddComponent<Rigidbody>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"Assets/Prefabs/{name}.prefab");
        Object.DestroyImmediate(instance);
        return prefab;
    }
}
