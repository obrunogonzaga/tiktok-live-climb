using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Captures actual PlayMode renders of the visual study, independently of gameplay validation.</summary>
[InitializeOnLoad]
public static class ReferenceStudyCapture
{
    private const string Active="Climb.ReferenceCapture";
    private const string Recording="Climb.ReferenceRecording";
    private static int frames, lastFrame=-1;
    private static Vector3 cameraStart;
    private static Quaternion rotationStart;
    private static bool initialized;
    static ReferenceStudyCapture(){EditorApplication.update+=Tick;}

    [MenuItem("Climb/Capture reference study")]
    public static void Run() { Begin(false); }

    [MenuItem("Climb/Record reference study")]
    public static void Record() { Begin(true); }

    private static void Begin(bool record)
    {
        frames=0;lastFrame=-1;initialized=false;
        EditorSceneManager.OpenScene("Assets/Scenes/ReferenceStudy.unity");
        Directory.CreateDirectory("../docs/evidence/rebuild");
        if(record)
        {
            Directory.CreateDirectory("Logs/reference-frames");
            foreach(var path in Directory.GetFiles("Logs/reference-frames","*.png"))File.Delete(path);
        }
        SessionState.SetBool(Recording,record);SessionState.SetBool(Active,true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(Time.time<2||lastFrame==Time.frameCount)return;
        lastFrame=Time.frameCount;
        var camera=Camera.main;
        if(!initialized)
        {
            initialized=true;cameraStart=camera.transform.position;rotationStart=camera.transform.rotation;
            ClimbValidation.Capture("../docs/evidence/rebuild/study.png");
            File.WriteAllText("../docs/evidence/rebuild/capture.txt",$"Unity {Application.unityVersion} PlayMode 1080x1920; scene ReferenceStudy; camera={cameraStart} FOV={camera.fieldOfView}\nVisual study, not a completed gameplay route.\n");
        }
        bool record=SessionState.GetBool(Recording,false);
        if(record)
        {
            Time.captureDeltaTime=1f/15;
            // First five seconds retain the proposed camera. The remaining seven inspect its depth.
            float seconds=frames/15f;
            float angle=seconds<5?0:Mathf.Sin((seconds-5)/7*Mathf.PI)*8;
            var pivot=new Vector3(0,4,2);
            camera.transform.position=pivot+Quaternion.Euler(0,angle,0)*(cameraStart-pivot);
            camera.transform.rotation=Quaternion.Euler(0,angle,0)*rotationStart;
            ClimbValidation.Capture($"Logs/reference-frames/{frames:00000}.png");
            if(frames==120)ClimbValidation.Capture("../docs/evidence/rebuild/side.png");
            if(++frames<180)return;
            File.AppendAllText("../docs/evidence/rebuild/capture.txt","Video: 180 Unity frames at 15fps; 5s fixed view plus 7s inspection orbit. Cloud density moves with Unity time; Bot pose is static.\n");
        }
        Time.captureDeltaTime=0;SessionState.SetBool(Active,false);
        EditorApplication.ExitPlaymode();
        if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
    }
}
