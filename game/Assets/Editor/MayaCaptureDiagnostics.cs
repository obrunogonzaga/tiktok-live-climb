using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Measures the rendered avatar silhouette with the gameplay camera, restoring all temporary changes.</summary>
public static class MayaCaptureDiagnostics
{
    [Serializable] private sealed class Measurement
    {
        public string method = "Unity camera silhouette pass; isolated avatar, same pose/projection, postprocessing off";
        public int canvasWidth = 1080, canvasHeight = 1920;
        public int x, y, width, height;
        public bool insideSafeZone, nearTargetScale;
    }

    public static bool Capture(ClimbBot bot, string prefix)
    {
        if (bot.GetComponent<MayaVisualMotion>() == null) return true;
        var camera = Camera.main;
        var data = camera.GetUniversalAdditionalCameraData();
        var renderers = bot.VisualRoot.GetComponentsInChildren<Renderer>();
        var saved = new Dictionary<Renderer, Material[]>();
        var layers = new Dictionary<GameObject, int>();
        var white = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        white.SetColor("_BaseColor", Color.white);
        white.SetFloat("_Cull", 0);
        int culling = camera.cullingMask;
        var clear = camera.clearFlags;
        var background = camera.backgroundColor;
        bool post = data.renderPostProcessing;
        string path = prefix + "-silhouette.png";
        try
        {
            foreach (var renderer in renderers)
            {
                saved.Add(renderer, renderer.sharedMaterials);
                layers.TryAdd(renderer.gameObject, renderer.gameObject.layer);
                renderer.gameObject.layer = 30;
                var materials = new Material[renderer.sharedMaterials.Length];
                Array.Fill(materials, white);
                renderer.sharedMaterials = materials;
            }
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            data.renderPostProcessing = false;
            ClimbValidation.Capture(path);
        }
        finally
        {
            foreach (var pair in saved) pair.Key.sharedMaterials = pair.Value;
            foreach (var pair in layers) pair.Key.layer = pair.Value;
            camera.cullingMask = culling; camera.clearFlags = clear; camera.backgroundColor = background;
            data.renderPostProcessing = post;
            UnityEngine.Object.DestroyImmediate(white);
        }
        var image = new Texture2D(2, 2);
        image.LoadImage(File.ReadAllBytes(path));
        var pixels = image.GetPixels32();
        int minX = image.width, minY = image.height, maxX = -1, maxY = -1;
        for (int y = 0; y < image.height; y++)
        for (int x = 0; x < image.width; x++)
        {
            if (pixels[y * image.width + x].r < 180) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < 0)
        {
            UnityEngine.Object.DestroyImmediate(image);
            File.WriteAllText(prefix + "-measurement.json", "{\"error\":\"empty_silhouette\"}");
            return false;
        }
        var result = new Measurement
        {
            x = minX, y = image.height - 1 - maxY, width = maxX - minX + 1, height = maxY - minY + 1,
            nearTargetScale = maxY - minY + 1 >= 180 && maxY - minY + 1 <= 260,
            insideSafeZone = minX >= 24 && maxX <= 940 && image.height - 1 - maxY >= 160 && image.height - minY <= 1260
        };
        UnityEngine.Object.DestroyImmediate(image);
        File.WriteAllText(prefix + "-measurement.json", JsonUtility.ToJson(result, true));
        return result.insideSafeZone && result.nearTargetScale;
    }

    public static void Inspect(ClimbBot bot, string directory)
    {
        if (bot.GetComponent<MayaVisualMotion>() == null) return;
        var camera = Camera.main;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        float fov = camera.fieldOfView;
        try
        {
            var root = bot.VisualRoot;
            var center = root.GetComponentsInChildren<Transform>().First(t => t.name == "Head").position + Vector3.up * .08f;
            camera.fieldOfView = 34;
            camera.transform.position = center + root.forward * 1.25f + root.right * .45f;
            camera.transform.LookAt(center);
            ClimbValidation.Capture(directory + "/maya-inspection-front.png");
            camera.transform.position = center + root.right * 1.3f + root.forward * .25f;
            camera.transform.LookAt(center);
            ClimbValidation.Capture(directory + "/maya-inspection-side.png");
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
        }
    }
}
