using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class ReferenceSceneBuilder
{
    private const string Env = "Assets/Art/Environment/";
    private const string Mats = Env + "Materials/";

    [MenuItem("Climb/Rebuild and capture reference study")]
    public static void BuildAndCapture() { Build(); ReferenceStudyCapture.Run(); }

    [MenuItem("Climb/Rebuild reference study")]
    public static void Build()
    {
        Directory.CreateDirectory(Mats);
        ConfigureTextures();
        PackSmoothness();
        GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var stone = new Material[6];
        for (int i = 0; i < 6; i++)
        {
            stone[i] = Lit("Stone" + i, new Color(.8f, .82f, .9f) * (.86f + .045f * i), .08f);
            stone[i].SetTexture("_BaseMap", Texture("stone-color.jpg"));
            stone[i].SetTexture("_BumpMap", Texture("stone-normal.jpg"));
            stone[i].SetTexture("_MetallicGlossMap", Texture("stone-smoothness.png")); stone[i].EnableKeyword("_METALLICSPECGLOSSMAP"); stone[i].SetFloat("_Smoothness", 1);
            stone[i].SetFloat("_BumpScale", .9f); stone[i].EnableKeyword("_NORMALMAP");
            stone[i].SetTexture("_OcclusionMap", Texture("stone-ao.jpg")); stone[i].SetFloat("_OcclusionStrength", .75f);
            stone[i].EnableKeyword("_OCCLUSIONMAP");
        }
        var mortar = Lit("Mortar", new Color(.065f, .078f, .105f), .1f);
        var ivy = Lit("Ivy", new Color(.055f, .16f, .022f), .2f);
        var tower = Instantiate(Env + "Tower.fbx", "Masonry tower", new Vector3(0, -5, 3));
        foreach (var r in tower.GetComponentsInChildren<Renderer>())
        {
            var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string name = materials[i].name;
                materials[i] = name.StartsWith("Stone") && int.TryParse(name.Substring(5), out var n) ? stone[Mathf.Clamp(n, 0, 5)] : name == "Ivy" ? ivy : mortar;
            }
            r.sharedMaterials = materials;
        }
        var cyan = Emission("Hold cyan", new Color(.01f, .7f, 1), 4);
        var magenta = Emission("Hold magenta", new Color(.6f, .015f, 1), 4);
        // This first art gate is a representative scene, before the full route is rebuilt.
        float[] x = { -1.1f, .1f, -.8f, .7f, -.4f, 1.0f, -.65f, .65f };
        for (int i = 0; i < x.Length; i++)
        {
            float y = -.3f + i * 1.1f;
            var ledge = Instantiate(Env + "Ledge.fbx", "Integrated stone hold " + (i + 1), new Vector3(x[i], y, .45f));
            foreach (var r in ledge.GetComponentsInChildren<Renderer>()) r.sharedMaterial = stone[i % 6];
            var lightMaterial = i % 2 == 0 ? cyan : magenta;
            Box("Inset neon", new Vector3(x[i], y + .015f, -.025f), new Vector3(.68f, .065f, .15f), lightMaterial);
            Point("Hold bounce", new Vector3(x[i], y + .2f, -.28f), i % 2 == 0 ? new Color(.08f, .7f, 1) : new Color(.7f, .05f, 1), .9f, 2.2f);
        }
        var heroHold=Instantiate(Env+"Ledge.fbx","Hero mounting hold",new Vector3(-.8f,2.85f,.1f));
        foreach(var r in heroHold.GetComponentsInChildren<Renderer>())r.sharedMaterial=stone[3];
        var botPath = "Assets/Art/Bot/Bot.fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(botPath) != null)
        {
            var bot = Instantiate(botPath, "Reference Bot", new Vector3(-.8f, 2.85f, -.4f));
            bot.transform.rotation = Quaternion.Euler(0, 105, 0) * bot.transform.rotation;
            bot.transform.localScale *= 1.3f;
            foreach (var r in bot.GetComponentsInChildren<Renderer>())
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = BotMaterial(materials[i].name);
                r.sharedMaterials = materials;
            }
        }
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";camera.transform.position = new Vector3(5.7f, 1, -11);
        camera.transform.LookAt(new Vector3(-.8f, 5, 2));camera.fieldOfView = 52;camera.aspect = 1080f / 1920;
        camera.nearClipPlane = .1f;camera.farClipPlane = 250;camera.allowHDR = true;camera.clearFlags = CameraClearFlags.Skybox;
        camera.gameObject.AddComponent<AudioListener>();
        var data = camera.GetUniversalAdditionalCameraData();data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        var sky = Material("Photographic night sky", "Climb/Photographic Star Sky");
        sky.shader=Shader.Find("Climb/Photographic Star Sky");
        sky.SetTexture("_MainTex", Texture("night-stars.jpg"));sky.SetColor("_Tint", new Color(.36f,.45f,.72f));sky.SetFloat("_Exposure", .65f);sky.SetFloat("_Rotation", 105);
        RenderSettings.skybox = sky;RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.14f,.18f,.32f);RenderSettings.ambientEquatorColor = new Color(.05f,.07f,.14f);RenderSettings.ambientGroundColor = new Color(.018f,.02f,.05f);
        RenderSettings.fog = true;RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.025f,.045f,.12f);RenderSettings.fogDensity = .009f;
        var moon = new GameObject("Moon key").AddComponent<Light>();moon.type = LightType.Directional;
        moon.color = new Color(.72f,.79f,1);moon.intensity = 2.8f;moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(38, -35, 0);moon.shadowBias=.03f;moon.shadowNormalBias=.15f;
        Point("Magenta rim", new Vector3(3.5f, 3, 1.7f), new Color(.75f,.015f,1), 14, 9);
        Point("Cold face bounce", new Vector3(-3, 3, -3), new Color(.1f,.55f,1), 4.5f, 9);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Env + "ReferenceVolume.asset");
        if (profile == null) { profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Env+"ReferenceVolume.asset"); }
        foreach(var old in profile.components.ToArray()) Object.DestroyImmediate(old,true);profile.components.Clear();
        var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(1.1f);bloom.intensity.Override(.32f);bloom.scatter.Override(.55f);
        var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.55f);color.contrast.Override(12);color.saturation.Override(8);
        var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.16f);vignette.smoothness.Override(.6f);
        foreach(var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        var volume=new GameObject("Night grading").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        Cloud("Distant cloud bank",new Vector3(-12,3,12),new Vector3(19,7,10),1,12);
        Cloud("High wisps",new Vector3(-12,10,20),new Vector3(21,5,10),8,9);
        Cloud("Low mist",new Vector3(-5,0,8),new Vector3(18,5,8),14,6);
        PlayerSettings.defaultScreenWidth=1080;PlayerSettings.defaultScreenHeight=1920;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/ReferenceStudy.unity");AssetDatabase.SaveAssets();
        Debug.Log("REFERENCE_STUDY_READY: real masonry, photographic sky, volumetric clouds, URP materials");
    }
    private static GameObject Instantiate(string path,string name,Vector3 position)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(source==null)throw new InvalidOperationException("Missing asset: "+path);
        var o=(GameObject)PrefabUtility.InstantiatePrefab(source);o.name=name;o.transform.position=position;
        if(path.StartsWith(Env))o.transform.rotation=Quaternion.Euler(0,180,0)*o.transform.rotation;
        return o;
    }
    private static Material BotMaterial(string name)
    {
        if(name.Contains("Orange"))return Emission("Bot orange",new Color(1,.27f,.012f),3);
        if(name.Contains("Visor"))return Lit("Bot visor",new Color(.006f,.009f,.018f),.85f);
        if(name.Contains("Joint"))return Lit("Bot joints",new Color(.025f,.029f,.038f),.32f);
        if(name.Contains("Metal"))return Lit("Bot metal",new Color(.25f,.3f,.36f),.6f);
        return Lit("Bot shell",new Color(.86f,.89f,.91f),.64f);
    }
    private static void ConfigureTextures()
    {
        foreach(var path in Directory.GetFiles(Env+"Textures"))
        {
            if(path.EndsWith(".meta"))continue;
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            importer.maxTextureSize=path.Contains("night-stars")?4096:2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            if(path.Contains("normal"))importer.textureType=TextureImporterType.NormalMap;
            if(path.Contains("ao")||path.Contains("roughness")||path.Contains("smoothness"))importer.sRGBTexture=false;
            if(path.Contains("roughness"))importer.isReadable=true;
            importer.SaveAndReimport();
        }
    }
    private static void PackSmoothness()
    {
        var rough=Texture("stone-roughness.jpg");var pixels=rough.GetPixels32();
        for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(0,0,0,(byte)(255-pixels[i].r));
        var packed=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,false,true);
        packed.SetPixels32(pixels);packed.Apply();
        File.WriteAllBytes(Env+"Textures/stone-smoothness.png",packed.EncodeToPNG());Object.DestroyImmediate(packed);
        var roughImporter=(TextureImporter)AssetImporter.GetAtPath(Env+"Textures/stone-roughness.jpg");roughImporter.isReadable=false;roughImporter.SaveAndReimport();
        AssetDatabase.ImportAsset(Env+"Textures/stone-smoothness.png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(Env+"Textures/stone-smoothness.png");importer.sRGBTexture=false;importer.SaveAndReimport();
    }
    private static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Env+"Textures/"+name);
    private static Material Material(string name,string shader)
    {
        string path=Mats+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}return m;
    }
    private static Material Lit(string name,Color color,float smoothness)
    {var m=Material(name,"Universal Render Pipeline/Lit");m.color=color;m.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(m);return m;}
    private static Material Emission(string name,Color color,float power)
    {var m=Lit(name,color,.45f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*power);return m;}
    private static void Box(string name,Vector3 position,Vector3 scale,Material material)
    {var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.position=position;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(o.GetComponent<Collider>());}
    private static void Point(string name,Vector3 position,Color color,float intensity,float range)
    {var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Point;l.transform.position=position;l.color=color;l.intensity=intensity;l.range=range;}
    private static void Cloud(string name,Vector3 position,Vector3 scale,float seed,float density)
    {var m=Material(name,"Climb/Cloud Volume");m.SetFloat("_Seed",seed);m.SetFloat("_Density",density);Box(name,position,scale,m);}
}
