using System.Collections.Generic;
using UnityEngine;

/// <summary>Small generic-rig poses driven by the existing Bot state; never moves its controller.</summary>
[DefaultExecutionOrder(120)]
public sealed class MayaVisualMotion : MonoBehaviour
{
    [SerializeField] private ClimbBot bot;
    [SerializeField] private Transform visualRoot;
    private readonly Dictionary<string, Transform> bones = new();
    private readonly Dictionary<string, Quaternion> bindRotations = new();
    private readonly Dictionary<string, Vector3> bindPositions = new();
    private bool ready;
    private float poseScale = 1;

    public void Configure(ClimbBot owner, Transform root)
    {
        bot = owner;
        visualRoot = root;
    }

    private void Start()
    {
        if (bot == null) bot = GetComponent<ClimbBot>();
        if (visualRoot == null && bot != null) visualRoot = bot.VisualRoot;
        if (visualRoot == null) return;
        foreach (var child in visualRoot.GetComponentsInChildren<Transform>())
        {
            bones[child.name] = child;
            bindRotations[child.name] = child.localRotation;
            bindPositions[child.name] = visualRoot.InverseTransformPoint(child.position);
        }
        string[] required = { "Head", "UpperArm.L", "Forearm.L", "Hand.L", "UpperArm.R", "Forearm.R", "Hand.R",
            "Thigh.L", "Shin.L", "Foot.L", "Thigh.R", "Shin.R", "Foot.R" };
        ready = true;
        foreach (string name in required)
        {
            if (bones.ContainsKey(name)) continue;
            Debug.LogError("Maya rig is missing bone: " + name, this);
            ready = false;
        }
        if (ready) poseScale = Vector3.Distance(bindPositions["UpperArm.L"], bindPositions["Forearm.L"]) / .26f;
    }

    private void LateUpdate()
    {
        if (!ready || bot == null) return;
        foreach (var pair in bones) pair.Value.localRotation = bindRotations[pair.Key];
        bool airborne = !bot.IsGrounded && bot.State != ClimbBot.BotState.Reset;
        float cycle = Mathf.Sin(Time.time * 7f);
        PoseArm("L", 1, cycle, airborne);
        PoseArm("R", -1, -cycle, airborne);
        PoseLeg("L", 1, cycle, airborne);
        PoseLeg("R", -1, -cycle, airborne);
        var camera = Camera.main;
        if (camera == null) return;
        Vector3 towardCamera = camera.transform.position - bones["Head"].position;
        towardCamera.y = 0;
        float yaw = Mathf.Clamp(Vector3.SignedAngle(visualRoot.forward, towardCamera, Vector3.up), -35f, 35f);
        bones["Head"].rotation = Quaternion.AngleAxis(yaw * .5f, visualRoot.up) * bones["Head"].rotation;
    }

    private void PoseArm(string side, float sign, float cycle, bool airborne)
    {
        string upper = "UpperArm." + side, lower = "Forearm." + side, hand = "Hand." + side;
        Vector3 shoulder = bindPositions[upper];
        Vector3 elbow = shoulder + (airborne
            ? new Vector3(sign * .07f, -.21f, .12f)
            : new Vector3(sign * .07f, -.22f, .045f + cycle * .04f)) * poseScale;
        Vector3 wrist = shoulder + (airborne
            ? new Vector3(sign * .06f, -.29f, .30f)
            : new Vector3(sign * .08f, -.36f + cycle * .025f, .16f)) * poseScale;
        Aim(upper, lower, elbow);
        Aim(lower, hand, wrist);
    }

    private void PoseLeg(string side, float sign, float cycle, bool airborne)
    {
        string thigh = "Thigh." + side, shin = "Shin." + side, foot = "Foot." + side;
        Vector3 hip = bindPositions[thigh];
        float stride = airborne ? cycle * .06f : cycle * .035f;
        Vector3 knee = hip + new Vector3(sign * .025f, -.37f, airborne ? .10f : stride) * poseScale;
        Vector3 ankle = hip + new Vector3(sign * .04f, airborne ? -.69f : -.78f, -stride) * poseScale;
        Aim(thigh, shin, knee);
        Aim(shin, foot, ankle);
    }

    private void Aim(string parent, string child, Vector3 targetLocal)
    {
        var from = bones[parent];
        Vector3 current = bones[child].position - from.position;
        Vector3 desired = visualRoot.TransformPoint(targetLocal) - from.position;
        if (current.sqrMagnitude > .00001f && desired.sqrMagnitude > .00001f)
            from.rotation = Quaternion.FromToRotation(current, desired) * from.rotation;
    }
}
