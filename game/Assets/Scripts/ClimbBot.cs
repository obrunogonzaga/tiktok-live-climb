using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class ClimbBot : MonoBehaviour
{
    public enum BotState { Idle, Climb, Avoid, Recover, Celebrate, Reset }

    [Header("Runtime state")]
    [SerializeField] private BotState state = BotState.Idle;
    public BotState State { get => state; private set => state = value; }
    [SerializeField] private int roundIndex;
    public int RoundIndex { get => roundIndex; private set => roundIndex = value; }
    public string RoundStatus { get; private set; } = "idle";
    public bool ContinuousMode => continuousMode && continuousTower != null;
    public int RouteIndex => ContinuousMode ? continuousTargetIndex : waypointIndex;
    public int NextRouteIndex => RouteIndex;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public Transform VisualRoot => visualRoot;

    [Header("Finite route")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform[] waypoints;

    [Header("Continuous route")]
    [SerializeField] private bool continuousMode;
    [SerializeField] private ContinuousTower continuousTower;
    [SerializeField] private ClimbFollowCamera followCamera;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private int continuousTargetIndex = 1;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
    [SerializeField, Min(0.1f)] private float jumpVelocity = 6.8f;
    [SerializeField, Min(0.1f)] private float gravity = 18f;
    [SerializeField, Min(0.01f)] private float waypointReachDistance = 0.28f;
    [SerializeField, Min(0.01f)] private float waypointVerticalTolerance = 0.3f;
    [SerializeField, Min(0.01f)] private float jumpTriggerHeight = 0.25f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Header("Avoidance and recovery")]
    [SerializeField, Min(0.1f)] private float obstacleProbeDistance = 1.05f;
    [SerializeField] private float obstacleProbeHeight = -0.25f;
    [SerializeField, Min(0.05f)] private float avoidDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float recoverDuration = 0.45f;
    [SerializeField, Min(0f)] private float recoverSpeed = 1.25f;

    [Header("Round lifecycle")]
    [SerializeField, Min(0.05f)] private float celebrationDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float fallBelowSpawnDistance = 1f;
    [SerializeField, Min(0.2f)] private float jumpRetryDelay = 0.7f;
    [SerializeField, Min(1)] private int maximumJumpRetries = 3;
    [SerializeField, Min(1f)] private float stalledTargetTimeout = 5.5f;

    private readonly RaycastHit[] raycastHits = new RaycastHit[8];

    private struct MovementTarget
    {
        public int Index;
        public Vector3 Position;
    }

    private CharacterController controller;
    private bool configured;
    private int waypointIndex;
    private bool jumpedForWaypoint;
    private bool jumpedDuringAvoid;
    private bool resetApplied;
    private bool fallRequested;
    private float verticalVelocity;
    private float avoidElapsed;
    private float celebrationElapsed;
    private float recoverUntil;
    private Vector3 avoidDirection;
    private Vector3 recoverDirection;
    private int activeTargetIndex = -1;
    private float targetStartedAt;
    private float lastTargetProgressAt;
    private float bestTargetDistance;
    private float nextJumpRetryAt;
    private int jumpRetries;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (spawnPoint == null) spawnPoint = transform;
        configured = HasUsableRoute();
        waypointIndex = FindNextWaypoint(0);
        continuousTargetIndex = Mathf.Max(1, continuousTargetIndex);
    }

    private void Start()
    {
        RebindContinuousRoute();
        if (configured && TryGetRouteTarget(out _)) ChangeState(BotState.Climb);
    }

    private void FixedUpdate()
    {
        if (!configured || controller == null || !HasUsableRoute())
        {
            if (State != BotState.Idle) ChangeState(BotState.Idle);
            ApplyMotion(Vector3.zero);
            return;
        }

        ReportContinuousProgress();
        if (State != BotState.Celebrate && State != BotState.Reset && IsBelowResetThreshold()) RequestFallReset();

        switch (State)
        {
            case BotState.Idle: ChangeState(BotState.Climb); break;
            case BotState.Climb: HandleClimb(); break;
            case BotState.Avoid: HandleAvoid(); break;
            case BotState.Recover: HandleRecover(); break;
            case BotState.Celebrate: HandleCelebrate(); break;
            case BotState.Reset: HandleReset(); break;
        }

        ReportContinuousProgress();
        TryRebase();
    }

    /// <summary>Supplies an ordered finite route and preserves legacy summit behavior.</summary>
    public void Configure(Transform spawn, Transform[] route)
    {
        UnregisterContinuousRoute();
        continuousMode = false;
        spawnPoint = spawn != null ? spawn : transform;
        waypoints = route ?? Array.Empty<Transform>();
        waypointIndex = FindNextWaypoint(0);
        jumpedForWaypoint = false;
        configured = HasUsableWaypoints();
        ResetTargetTracking();
        if (Application.isPlaying && configured && State == BotState.Idle) ChangeState(BotState.Climb);
    }

    /// <summary>
    /// Switches the Bot to a pooled infinite route. Spawn is standing center over
    /// platform index zero; index one is always the first climb target.
    /// </summary>
    public void ConfigureContinuous(
        ContinuousTower tower,
        Transform spawn,
        ClimbFollowCamera camera = null,
        Transform modelRoot = null,
        float continuousMoveSpeed = -1f)
    {
        UnregisterContinuousRoute();
        continuousTower = tower;
        continuousMode = continuousTower != null;
        spawnPoint = spawn != null ? spawn : transform;
        followCamera = camera;
        visualRoot = modelRoot;
        waypoints = Array.Empty<Transform>();
        waypointIndex = 0;
        continuousTargetIndex = 1;
        if (continuousMoveSpeed > 0f) moveSpeed = continuousMoveSpeed;

        configured = HasUsableRoute();
        jumpedForWaypoint = false;
        fallRequested = false;
        ResetTargetTracking();
        if (configured)
        {
            continuousTower.RegisterFloatingTransform(transform);
            continuousTower.EnsureRouteAround(continuousTargetIndex);
            continuousTower.NotifyLanded(0);
        }

        ConfigureVisualMotion();
        if (Application.isPlaying && configured && State == BotState.Idle) ChangeState(BotState.Climb);
    }

    public void SetVisualRoot(Transform modelRoot)
    {
        visualRoot = modelRoot;
        ConfigureVisualMotion();
    }

    private void HandleClimb()
    {
        if (!TryGetRouteTarget(out MovementTarget target)) { CompleteRoute(); return; }
        BeginTarget(target);
        if (IsTargetStalled()) { RequestFallReset(); return; }

        Vector3 travelDirection = DirectionTo(target.Position, transform.forward);
        if (IsObstacleAhead(travelDirection)) { ChangeState(BotState.Avoid); return; }

        FaceDirection(travelDirection);
        TryStartJump(target, false);
        ApplyMotion(travelDirection * moveSpeed);
        if (HasReachedTarget(target.Position)) CompleteTarget(target);
    }

    private void HandleAvoid()
    {
        if (!TryGetRouteTarget(out MovementTarget target)) { CompleteRoute(); return; }
        BeginTarget(target);
        if (IsTargetStalled()) { RequestFallReset(); return; }

        Vector3 travelDirection = DirectionTo(target.Position, transform.forward);
        if (avoidDirection.sqrMagnitude < 0.001f) avoidDirection = PickAvoidanceDirection(travelDirection);

        FaceDirection(travelDirection);
        TryStartJump(target, true);
        Vector3 sidestep = travelDirection * 0.92f + avoidDirection * 0.38f;
        ApplyMotion(sidestep.normalized * moveSpeed);
        avoidElapsed += Time.deltaTime;
        if (avoidElapsed >= avoidDuration && !IsObstacleAhead(travelDirection)) ChangeState(BotState.Climb);
    }

    private void HandleRecover()
    {
        if (IsTargetStalled()) { RequestFallReset(); return; }
        if (Time.time < recoverUntil)
        {
            ApplyMotion(recoverDirection * recoverSpeed);
            return;
        }
        ChangeState(BotState.Climb);
    }

    private void HandleCelebrate()
    {
        ApplyMotion(Vector3.zero);
        celebrationElapsed += Time.deltaTime;
        if (celebrationElapsed >= Mathf.Max(0.05f, celebrationDuration)) ChangeState(BotState.Reset);
    }

    private void HandleReset()
    {
        if (resetApplied) return;
        resetApplied = true;
        if (ContinuousMode) ResetContinuousRoute();

        TeleportToSpawn();
        RoundIndex++;
        Debug.Log($"Round {RoundIndex}: reset to spawn", this);
        waypointIndex = FindNextWaypoint(0);
        continuousTargetIndex = 1;
        jumpedForWaypoint = false;
        jumpedDuringAvoid = false;
        verticalVelocity = 0f;
        fallRequested = false;
        ResetTargetTracking();
        followCamera?.Snap();
        ChangeState(configured && TryGetRouteTarget(out _) ? BotState.Climb : BotState.Idle);
    }

    private void ResetContinuousRoute()
    {
        if (continuousTower == null) return;
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        continuousTower.ResetRoute();
        if (wasEnabled) controller.enabled = true;
        continuousTower.NotifyLanded(0);
    }

    private void CompleteTarget(MovementTarget target)
    {
        if (ContinuousMode)
        {
            continuousTower.NotifyLanded(target.Index);
            continuousTargetIndex = target.Index + 1;
            continuousTower.EnsureRouteAround(continuousTargetIndex);
            jumpedForWaypoint = false;
            jumpedDuringAvoid = false;
            ResetTargetTracking();
            return;
        }

        waypointIndex = FindNextWaypoint(waypointIndex + 1);
        jumpedForWaypoint = false;
        jumpedDuringAvoid = false;
        ResetTargetTracking();
        if (!TryGetRouteTarget(out _)) ChangeState(BotState.Celebrate);
    }

    private void CompleteRoute()
    {
        if (ContinuousMode) { RequestFallReset(); return; }
        ChangeState(BotState.Celebrate);
    }

    private void TryStartJump(MovementTarget target, bool avoiding)
    {
        if (!controller.isGrounded || target.Position.y - transform.position.y <= jumpTriggerHeight) return;
        bool alreadyJumped = avoiding ? jumpedDuringAvoid : jumpedForWaypoint;
        if (alreadyJumped && Time.time < nextJumpRetryAt) return;

        if (alreadyJumped) jumpRetries++;
        if (jumpRetries > maximumJumpRetries) { RequestFallReset(); return; }

        verticalVelocity = jumpVelocity;
        nextJumpRetryAt = Time.time + jumpRetryDelay;
        if (avoiding) jumpedDuringAvoid = true;
        else jumpedForWaypoint = true;
    }

    private void BeginTarget(MovementTarget target)
    {
        float distance = (target.Position - transform.position).sqrMagnitude;
        if (target.Index != activeTargetIndex)
        {
            activeTargetIndex = target.Index;
            targetStartedAt = Time.time;
            lastTargetProgressAt = Time.time;
            bestTargetDistance = distance;
            jumpRetries = 0;
            nextJumpRetryAt = 0f;
            return;
        }

        if (distance + 0.015f < bestTargetDistance)
        {
            bestTargetDistance = distance;
            lastTargetProgressAt = Time.time;
        }
    }

    private bool IsTargetStalled()
    {
        if (activeTargetIndex < 0 || Time.time - targetStartedAt < stalledTargetTimeout) return false;
        return Time.time - lastTargetProgressAt > stalledTargetTimeout * 0.45f;
    }

    private void ResetTargetTracking()
    {
        activeTargetIndex = -1;
        targetStartedAt = 0f;
        lastTargetProgressAt = 0f;
        bestTargetDistance = float.PositiveInfinity;
        jumpRetries = 0;
        nextJumpRetryAt = 0f;
    }

    private void ApplyMotion(Vector3 horizontalVelocity)
    {
        if (controller == null || !controller.enabled) return;
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity -= gravity * Time.deltaTime;
        Vector3 motion = horizontalVelocity;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    private void TeleportToSpawn()
    {
        Vector3 destinationPosition;
        Quaternion destinationRotation;
        if (spawnPoint != null && spawnPoint != transform)
        {
            destinationPosition = spawnPoint.position;
            destinationRotation = spawnPoint.rotation;
        }
        else if (ContinuousMode)
        {
            destinationPosition = continuousTower.GetStandingPosition(0);
            destinationRotation = transform.rotation;
        }
        else
        {
            destinationPosition = transform.position;
            destinationRotation = transform.rotation;
        }

        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        transform.SetPositionAndRotation(destinationPosition, destinationRotation);
        if (wasEnabled) controller.enabled = true;
        Physics.SyncTransforms();
    }

    private void ChangeState(BotState nextState)
    {
        if (State == nextState) return;
        State = nextState;
        if (nextState == BotState.Celebrate)
            RoundStatus = fallRequested || ContinuousMode || IsBelowResetThreshold() ? "fell" : "summit";
        else if (nextState == BotState.Idle || nextState == BotState.Reset) RoundStatus = "idle";
        else RoundStatus = "climbing";

        switch (nextState)
        {
            case BotState.Avoid:
                avoidElapsed = 0f;
                jumpedDuringAvoid = false;
                avoidDirection = Vector3.zero;
                break;
            case BotState.Recover:
                recoverUntil = Time.time + Mathf.Max(0.05f, recoverDuration);
                if (recoverDirection.sqrMagnitude < 0.001f) recoverDirection = -transform.forward;
                break;
            case BotState.Celebrate:
                celebrationElapsed = 0f;
                break;
            case BotState.Reset:
                resetApplied = false;
                break;
        }
    }

    private bool HasReachedTarget(Vector3 targetPosition)
    {
        if (!controller.isGrounded) return false;
        Vector3 offset = targetPosition - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= waypointReachDistance * waypointReachDistance &&
               transform.position.y >= targetPosition.y - waypointVerticalTolerance;
    }

    private bool TryGetRouteTarget(out MovementTarget target)
    {
        target = default;
        if (ContinuousMode)
        {
            if (!continuousTower.TryGetTarget(continuousTargetIndex, out ContinuousTower.RouteTarget continuousTarget)) return false;
            target = new MovementTarget { Index = continuousTarget.Index, Position = continuousTarget.Position };
            return true;
        }

        if (!TryGetWaypoint(out Transform waypoint)) return false;
        target = new MovementTarget { Index = waypointIndex, Position = waypoint.position };
        return true;
    }

    private bool TryGetWaypoint(out Transform waypoint)
    {
        waypointIndex = FindNextWaypoint(waypointIndex);
        if (waypoints == null || waypointIndex >= waypoints.Length)
        {
            waypoint = null;
            return false;
        }
        waypoint = waypoints[waypointIndex];
        return waypoint != null;
    }

    private int FindNextWaypoint(int startIndex)
    {
        if (waypoints == null) return 0;
        int index = Mathf.Max(0, startIndex);
        while (index < waypoints.Length && waypoints[index] == null) index++;
        return index;
    }

    private bool HasUsableWaypoints() => FindNextWaypoint(0) < (waypoints == null ? 0 : waypoints.Length);
    private bool HasUsableRoute() => ContinuousMode ? continuousTower != null && continuousTower.IsConfigured : HasUsableWaypoints();

    private Vector3 DirectionTo(Vector3 target, Vector3 fallback)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = fallback;
            direction.y = 0f;
        }
        return direction.sqrMagnitude < 0.001f ? Vector3.forward : direction.normalized;
    }

    private void FaceDirection(Vector3 direction)
    {
        if (turnSpeed <= 0f || direction.sqrMagnitude < 0.001f) return;
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private Vector3 PickAvoidanceDirection(Vector3 travelDirection)
    {
        Vector3 side = Vector3.Cross(Vector3.up, travelDirection).normalized;
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        Vector3 origin = transform.position + Vector3.up * obstacleProbeHeight;
        bool leftBlocked = RaycastForObstacle(origin, side, obstacleProbeDistance * 0.8f);
        bool rightBlocked = RaycastForObstacle(origin, -side, obstacleProbeDistance * 0.8f);
        return leftBlocked && !rightBlocked ? -side : side;
    }

    private bool IsObstacleAhead(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * obstacleProbeHeight;
        if (RaycastForObstacle(origin, direction, obstacleProbeDistance)) return true;
        return RaycastForObstacle(origin + Vector3.up * 0.55f, direction, obstacleProbeDistance);
    }

    private bool RaycastForObstacle(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(origin, direction, raycastHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++) if (IsObstacleCollider(raycastHits[i].collider)) return true;
        return false;
    }

    private bool IsObstacleCollider(Collider collider)
    {
        if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform)) return false;
        return HasName(collider.transform, "Obstacle");
    }

    private bool IsBelowResetThreshold()
    {
        if (ContinuousMode) return continuousTower.HasFallenBelowHighest(GetFootWorldHeight());
        return spawnPoint != null && transform.position.y < spawnPoint.position.y - Mathf.Max(0.1f, fallBelowSpawnDistance);
    }

    private float GetFootWorldHeight()
    {
        if (ContinuousMode) return transform.position.y - continuousTower.StandingCenterHeight;
        float center = controller != null ? controller.center.y : 0f;
        float halfHeight = controller != null ? controller.height * 0.5f : 0.7f;
        return transform.position.y + center - halfHeight;
    }

    private void ReportContinuousProgress()
    {
        if (ContinuousMode) continuousTower.ReportCurrentFootHeight(GetFootWorldHeight());
    }

    private void TryRebase()
    {
        if (!ContinuousMode || State == BotState.Reset || controller == null) return;
        bool wasEnabled = controller.enabled;
        if (wasEnabled) controller.enabled = false;
        bool shifted = continuousTower.RebaseIfNeeded(transform);
        if (wasEnabled) controller.enabled = true;
        if (shifted)
        {
            ReportContinuousProgress();
            followCamera?.Snap();
        }
    }

    private void RequestFallReset()
    {
        if (fallRequested || State == BotState.Celebrate || State == BotState.Reset) return;
        fallRequested = true;
        ChangeState(BotState.Celebrate);
    }

    private void ConfigureVisualMotion()
    {
        BotVisualMotion motion = GetComponent<BotVisualMotion>();
        if (motion != null && visualRoot != null) motion.Configure(this, visualRoot);
    }

    private void UnregisterContinuousRoute()
    {
        if (continuousTower == null) return;
        continuousTower.UnregisterFloatingTransform(transform);
    }

    private void RebindContinuousRoute()
    {
        if (!ContinuousMode)
            return;

        configured = HasUsableRoute();
        if (!configured)
            return;

        continuousTargetIndex = Mathf.Max(1, continuousTargetIndex);
        continuousTower.RegisterFloatingTransform(transform);
        continuousTower.EnsureRouteAround(continuousTargetIndex);
        continuousTower.NotifyLanded(0);
        ConfigureVisualMotion();
    }

    private static bool HasName(Transform candidate, string expected)
    {
        while (candidate != null)
        {
            if (string.Equals(candidate.name, expected, StringComparison.OrdinalIgnoreCase)) return true;
            candidate = candidate.parent;
        }
        return false;
    }

    private static bool IsSummitCollider(Collider collider) => collider != null && HasName(collider.transform, "Summit");

    private void OnTriggerEnter(Collider other)
    {
        if (!ContinuousMode && IsSummitCollider(other)) ChangeState(BotState.Celebrate);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!ContinuousMode && IsSummitCollider(hit.collider))
        {
            ChangeState(BotState.Celebrate);
            return;
        }
        if (!IsObstacleCollider(hit.collider) || State == BotState.Celebrate || State == BotState.Reset || Time.time < recoverUntil) return;

        recoverDirection = hit.normal;
        recoverDirection.y = 0f;
        if (recoverDirection.sqrMagnitude < 0.001f) recoverDirection = -transform.forward;
        recoverDirection.Normalize();
        ChangeState(BotState.Recover);
    }
}
