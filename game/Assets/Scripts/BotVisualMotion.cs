using UnityEngine;

/// <summary>
/// Optional procedural motion for the named pivots exported with Bot.fbx.
/// It preserves the visual wrapper's authored orientation and only adds small
/// local limb offsets, avoiding a second competing root rotation.
/// </summary>
[DisallowMultipleComponent]
public sealed class BotVisualMotion : MonoBehaviour
{
    [SerializeField] private ClimbBot bot;
    [SerializeField] private Transform visualRoot;
    [SerializeField, Range(0f, 30f)] private float strideDegrees = 12f;
    [SerializeField, Min(0.1f)] private float strideFrequency = 7f;

    private Transform rightArm;
    private Transform leftArm;
    private Transform rightLeg;
    private Transform leftLeg;
    private Quaternion rightArmBase;
    private Quaternion leftArmBase;
    private Quaternion rightLegBase;
    private Quaternion leftLegBase;

    public void Configure(ClimbBot owner, Transform root)
    {
        bot = owner;
        visualRoot = root;
        CachePivots();
        ResetPose();
    }

    public void ResetPose()
    {
        Restore(rightArm, rightArmBase);
        Restore(leftArm, leftArmBase);
        Restore(rightLeg, rightLegBase);
        Restore(leftLeg, leftLegBase);
    }

    private void Awake()
    {
        if (bot == null)
            bot = GetComponentInParent<ClimbBot>();
        if (visualRoot == null && bot != null)
            visualRoot = bot.VisualRoot;
        CachePivots();
    }

    private void OnDisable()
    {
        ResetPose();
    }

    private void LateUpdate()
    {
        if (bot == null || visualRoot == null)
            return;

        float phase = Time.time * strideFrequency;
        float stride = bot.IsGrounded && bot.State == ClimbBot.BotState.Climb
            ? Mathf.Sin(phase) * strideDegrees
            : 0f;

        if (!bot.IsGrounded && bot.State != ClimbBot.BotState.Reset)
        {
            // Keep the pre-authored raised hand readable while airborne.
            SetOffset(rightArm, rightArmBase, 10f);
            SetOffset(leftArm, leftArmBase, -8f);
            SetOffset(rightLeg, rightLegBase, -7f);
            SetOffset(leftLeg, leftLegBase, 7f);
            return;
        }

        SetOffset(rightArm, rightArmBase, stride);
        SetOffset(leftArm, leftArmBase, -stride);
        SetOffset(rightLeg, rightLegBase, -stride);
        SetOffset(leftLeg, leftLegBase, stride);
    }

    private void CachePivots()
    {
        if (visualRoot == null)
            return;

        rightArm = Find(visualRoot, "Arm_R_ShoulderJoint");
        leftArm = Find(visualRoot, "Arm_L_ShoulderJoint");
        rightLeg = Find(visualRoot, "Leg_R_HipJoint");
        leftLeg = Find(visualRoot, "Leg_L_HipJoint");
        rightArmBase = LocalRotation(rightArm);
        leftArmBase = LocalRotation(leftArm);
        rightLegBase = LocalRotation(rightLeg);
        leftLegBase = LocalRotation(leftLeg);
    }

    private static Transform Find(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform match = Find(root.GetChild(index), name);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Quaternion LocalRotation(Transform pivot)
    {
        return pivot != null ? pivot.localRotation : Quaternion.identity;
    }

    private static void Restore(Transform pivot, Quaternion baseRotation)
    {
        if (pivot != null)
            pivot.localRotation = baseRotation;
    }

    private static void SetOffset(Transform pivot, Quaternion baseRotation, float xDegrees)
    {
        if (pivot != null)
            pivot.localRotation = baseRotation * Quaternion.Euler(xDegrees, 0f, 0f);
    }
}
