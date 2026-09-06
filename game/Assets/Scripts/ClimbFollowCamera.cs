using UnityEngine;

/// <summary>
/// Smoothly tracks the continuous helix while preserving the approved study
/// camera's Bot-relative framing. Both the camera and its look point orbit
/// together, so the Bot stays center-left instead of flattening against the wall.
/// </summary>
[DisallowMultipleComponent]
public sealed class ClimbFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private ContinuousTower tower;
    [SerializeField] private Vector3 referenceOffset = new(6.5f, -2.57f, -10.6f);
    [SerializeField] private Vector3 aimOffset = new(0f, 1.43f, 2.4f);
    [SerializeField] private float azimuthBiasDegrees;
    [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.22f;
    [SerializeField, Min(0.01f)] private float rotationSharpness = 10f;

    private Vector3 positionVelocity;
    private bool hasAzimuth;
    private float previousAzimuth;
    private float accumulatedAzimuth;
    private bool subscribed;

    public Transform Target => target;
    public Vector3 ReferenceOffset => referenceOffset;
    public Vector3 AimOffset => aimOffset;

    public void Configure(
        Transform newTarget,
        ContinuousTower newTower,
        Vector3 newReferenceOffset,
        float newAzimuthBiasDegrees = 0f)
    {
        Configure(newTarget, newTower, newReferenceOffset, Vector3.up * 2f, newAzimuthBiasDegrees);
    }

    public void Configure(
        Transform newTarget,
        ContinuousTower newTower,
        Vector3 newReferenceOffset,
        Vector3 newAimOffset,
        float newAzimuthBiasDegrees = 0f)
    {
        Unsubscribe();
        target = newTarget;
        tower = newTower;
        referenceOffset = newReferenceOffset;
        aimOffset = newAimOffset;
        azimuthBiasDegrees = newAzimuthBiasDegrees;
        ResetAzimuth();

        Subscribe();

        Snap();
    }

    private void Awake()
    {
        ResetAzimuth();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        if (Application.isPlaying)
            Snap();
    }

    public void Snap()
    {
        if (target == null)
            return;

        Vector3 position = DesiredPosition();
        transform.position = position;
        transform.rotation = DesiredRotation(position);
        positionVelocity = Vector3.zero;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = DesiredPosition();
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);

        Quaternion desiredRotation = DesiredRotation(transform.position);
        float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
    }

    private Vector3 DesiredPosition()
    {
        return target.position + Orbit(referenceOffset);
    }

    private Quaternion DesiredRotation(Vector3 cameraPosition)
    {
        Vector3 lookPoint = target.position + Orbit(aimOffset);
        Vector3 lookDirection = lookPoint - cameraPosition;
        return lookDirection.sqrMagnitude < 0.0001f
            ? transform.rotation
            : Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private Vector3 Orbit(Vector3 offset)
    {
        // Helix outward is (sin(theta), 0, -cos(theta)); Unity's positive Y
        // rotation turns forward toward -X, so camera framing uses -theta.
        return Quaternion.AngleAxis(-CurrentOrbitDegrees(), Vector3.up) * offset;
    }

    private float CurrentOrbitDegrees()
    {
        if (tower == null || target == null)
            return azimuthBiasDegrees;

        float current = tower.GetAzimuthDegrees(target.position);
        if (!hasAzimuth)
        {
            hasAzimuth = true;
            previousAzimuth = current;
            accumulatedAzimuth = 0f;
        }
        else
        {
            accumulatedAzimuth += Mathf.DeltaAngle(previousAzimuth, current);
            previousAzimuth = current;
        }

        return accumulatedAzimuth + azimuthBiasDegrees;
    }

    private void HandleRebased(Vector3 _)
    {
        Snap();
    }

    private void ResetAzimuth()
    {
        hasAzimuth = false;
        accumulatedAzimuth = 0f;
    }

    private void Unsubscribe()
    {
        if (tower != null && subscribed)
        {
            tower.Rebased -= HandleRebased;
            tower.UnregisterFloatingTransform(transform);
        }
        subscribed = false;
    }

    private void Subscribe()
    {
        if (tower == null || subscribed)
            return;

        tower.RegisterFloatingTransform(transform);
        tower.Rebased += HandleRebased;
        subscribed = true;
    }
}
