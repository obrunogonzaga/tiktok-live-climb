using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the authored Maya mesh and builds portable URP materials.</summary>
public static class MayaAssetSetup
{
    public const float VisualScale = .88f;
    private const string Root = "Assets/Art/Maya/";

    public static GameObject Create(Transform parent)
    {
        ConfigureImport(Root + "Maya.fbx");
        ConfigureImport(Root + "LOD/MayaLOD.fbx");
        string folder = Root + "Materials";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        Make("MayaSkin", Color.white, .30f, texture: "MayaSkinAtlas.png");
        Make("MayaFace", Color.white, .28f, texture: "MayaFace.png");
        var hair = Make("MayaHair", Color.white, .10f, texture: "MayaHair.png");
        hair.SetFloat("_Cull", 0);
        hair.SetFloat("_SpecularHighlights", 0);
        hair.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        var hairline = Make("MayaHairline", Color.white, .10f, texture: "MayaHairline.png");
        hairline.SetFloat("_Cull", 0); hairline.SetFloat("_AlphaClip", 1); hairline.SetFloat("_Cutoff", .4f);
        hairline.EnableKeyword("_ALPHATEST_ON"); hairline.SetOverrideTag("RenderType", "TransparentCutout");
        hairline.renderQueue = 2450;
        Make("MayaTank", new Color(.24f, .27f, .30f), .12f, texture: "MayaFabric.png");
        Make("MayaCargo", new Color(.50f, .56f, .63f), .12f, texture: "MayaFabric.png");
        Make("MayaLeather", new Color(.22f, .15f, .11f), .28f);
        Make("MayaRubber", new Color(.045f, .05f, .06f), .08f);
        Make("MayaStitch", new Color(.43f, .40f, .35f), .12f);
        Make("MayaGold", new Color(.83f, .62f, .27f), .65f, .75f);
        Make("MayaBuckle", new Color(.5f, .52f, .55f), .6f, .8f);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Maya.fbx");
        if (source == null) throw new InvalidOperationException("Maya.fbx must exist before rebuilding the game.");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        model.transform.SetParent(parent, false);
        model.transform.localScale *= VisualScale;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                m != null ? AssetDatabase.LoadAssetAtPath<Material>(folder + "/" + m.name + ".mat") ??
                throw new InvalidOperationException("Unmapped Maya material: " + m.name) :
                throw new InvalidOperationException("Null material slot in Maya FBX.")).ToArray();
            if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }
        AddDistanceMesh(model);
        AssetDatabase.SaveAssets();
        return model;
    }

    private static void ConfigureImport(string modelPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Maya FBX importer missing.");
        bool changed = importer.animationType != ModelImporterAnimationType.Generic ||
            importer.importAnimation || importer.optimizeGameObjects || !importer.useFileScale || importer.globalScale != 1f;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.optimizeGameObjects = false;
        importer.useFileScale = true;
        importer.globalScale = 1;
        if (changed) importer.SaveAndReimport();
        var normal = AssetImporter.GetAtPath(Root + "Textures/MayaFabricNormal.png") as TextureImporter;
        if (normal != null && normal.textureType != TextureImporterType.NormalMap)
        {
            normal.textureType = TextureImporterType.NormalMap;
            normal.SaveAndReimport();
        }
    }

    private static void AddDistanceMesh(GameObject model)
    {
        var close = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        var bones = model.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "LOD/MayaLOD.fbx");
        if (source == null) throw new InvalidOperationException("Maya distance mesh is missing.");
        var distant = new System.Collections.Generic.List<Renderer>();
        foreach (var original in source.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var matching = close.FirstOrDefault(r => r.name == original.name);
            if (matching == null) throw new InvalidOperationException("Unmatched Maya LOD renderer: " + original.name);
            var child = new GameObject("Distant " + original.name);
            child.transform.SetParent(matching.transform.parent, false);
            child.transform.localPosition = matching.transform.localPosition;
            child.transform.localRotation = matching.transform.localRotation;
            child.transform.localScale = matching.transform.localScale;
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = original.sharedMesh;
            renderer.sharedMaterials = matching.sharedMaterials;
            renderer.bones = original.bones.Select(b => bones[b.name]).ToArray();
            renderer.rootBone = original.rootBone != null ? bones[original.rootBone.name] : null;
            renderer.localBounds = original.localBounds;
            renderer.updateWhenOffscreen = false;
            distant.Add(renderer);
        }
        var lod = model.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(.065f, close), new LOD(.008f, distant.ToArray()) });
        lod.fadeMode = LODFadeMode.None;
        lod.RecalculateBounds();
    }

    private static Material Make(string name, Color color, float smoothness, float metallic = 0, string texture = null)
    {
        string path = Root + "Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (texture != null)
        {
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + texture);
            if (image == null) throw new InvalidOperationException("Missing Maya texture: " + texture);
            mat.SetTexture("_BaseMap", image);
            if (texture == "MayaFabric.png")
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/MayaFabricNormal.png"));
                mat.SetFloat("_BumpScale", .14f);
                mat.EnableKeyword("_NORMALMAP");
            }
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
