using System;
using System.IO;
using UnityEngine;

public sealed class ContinuousPerformanceProbe : MonoBehaviour
{
    private float started, measuredFrom;
    private int frames;
    private double total, largest;
    private RenderTexture target;
    private void Start()
    {
        if(Application.isBatchMode || SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.LogError("Performance probe requires a windowed graphics player; batch/nographics does not measure rendering.");
            enabled=false;Application.Quit(2);return;
        }
        QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Application.runInBackground=true;
        target=new RenderTexture(1080,1920,24);target.Create();Camera.main.targetTexture=target;
        started=Time.realtimeSinceStartup;
    }
    private void LateUpdate()
    {
        float now=Time.realtimeSinceStartup;
        if(now-started<3)return;
        if(measuredFrom==0){measuredFrom=now;return;}
        frames++;total+=Time.unscaledDeltaTime;largest=Math.Max(largest,Time.unscaledDeltaTime);
        if(now-measuredFrom<15)return;
        string output="/tmp/continuous-player-performance.json";
        var tower=FindFirstObjectByType<ContinuousTower>();
        string json=$"{{\"unity\":\"{Application.unityVersion}\",\"gpu\":\"{SystemInfo.graphicsDeviceName}\",\"width\":1080,\"height\":1920,\"seconds\":{(now-measuredFrom).ToString(System.Globalization.CultureInfo.InvariantCulture)},\"frames\":{frames},\"fps\":{(frames/(now-measuredFrom)).ToString(System.Globalization.CultureInfo.InvariantCulture)},\"meanFrameMs\":{(total/frames*1000).ToString(System.Globalization.CultureInfo.InvariantCulture)},\"maxFrameMs\":{(largest*1000).ToString(System.Globalization.CultureInfo.InvariantCulture)},\"height\":{tower.CurrentHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"platforms\":{tower.ActivePlatformCount},\"bodies\":{tower.ActiveBodyCount}}}";
        File.WriteAllText(output,json+"\n");Application.Quit(0);
    }
}
