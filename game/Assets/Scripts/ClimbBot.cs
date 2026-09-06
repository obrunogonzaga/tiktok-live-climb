using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class ClimbBot : MonoBehaviour
{
    public enum BotState
    {
        Idle,
        Climb,
        Avoid,
        Recover,
        Celebrate,
        Reset
    }

    [Header("Runtime state")]
    [SerializeField] private BotState state = BotState.Idle;
    public BotState State { get => state; private set => state = value; }

    [SerializeField] private int roundIndex;
    public int RoundIndex { get => roundIndex; private set => roundIndex = value; }

    [Header("Route")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform[] waypoints;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4.5f;
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

    private readonly RaycastHit[] raycastHits = new RaycastHit[8];

    private CharacterController controller;
    private bool configured;
    private int waypointIndex;
    private bool jumpedForWaypoint;
    private bool jumpedDuringAvoid;
    private bool resetApplied;
    private float verticalVelocity;
    private float avoidElapsed;
    private float celebrationElapsed;
    private float recoverUntil;
    private Vector3 avoidDirection;
    private Vector3 recoverDirection;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (spawnPoint == null)
            spawnPoint = transform;

        configured = HasUsableWaypoints();
        waypointIndex = FindNextWaypoint(0);
    }

    private void Start()
    {
        if (configured && TryGetWaypoint(out _))
            ChangeState(BotState.Climb);
    }

    private void FixedUpdate()
    {
        if (!configured || controller == null || !HasUsableWaypoints())
        {
            if (State != BotState.Idle)
                ChangeState(BotState.Idle);

            ApplyMotion(Vector3.zero);
            return;
        }

        if (State != BotState.Celebrate && State != BotState.Reset && IsBelowSpawn())
            ChangeState(BotState.Celebrate);

        switch (State)
        {
            case BotState.Idle:
                ChangeState(BotState.Climb);
                break;

            case BotState.Climb:
                HandleClimb();
                break;

            case BotState.Avoid:
                HandleAvoid();
                break;

            case BotState.Recover:
                HandleRecover();
                break;

            case BotState.Celebrate:
                HandleCelebrate();
                break;

            case BotState.Reset:
                HandleReset();
                break;
        }
    }

    /// <summary>
    /// Supplies the standing spawn and ordered bot-center waypoints for a round route.
    /// Waypoints should be reachable standing positions above each platform.
    /// </summary>
    public void Configure(Transform spawn, Transform[] route)
    {
        spawnPoint = spawn != null ? spawn : transform;
        waypoints = route ?? Array.Empty<Transform>();
        waypointIndex = FindNextWaypoint(0);
        jumpedForWaypoint = false;
        configured = HasUsableWaypoints();

        if (Application.isPlaying && configured && State == BotState.Idle)
            ChangeState(BotState.Climb);
    }

    private void HandleClimb()
    {
        if (!TryGetWaypoint(out var target))
        {
            ChangeState(BotState.Celebrate);
            return;
        }

        Vector3 travelDirection = DirectionTo(target.position, transform.forward);
        if (IsObstacleAhead(travelDirection))
        {
            ChangeState(BotState.Avoid);
            return;
        }

        FaceDirection(travelDirection);

        if (controller.isGrounded && !jumpedForWaypoint &&
            target.position.y - transform.position.y > jumpTriggerHeight)
        {
            verticalVelocity = jumpVelocity;
            jumpedForWaypoint = true;
        }

        ApplyMotion(travelDirection * moveSpeed);

        if (!HasReachedWaypoint(target))
            return;

        waypointIndex = FindNextWaypoint(waypointIndex + 1);
        jumpedForWaypoint = false;

        if (!TryGetWaypoint(out _))
            ChangeState(BotState.Celebrate);
    }

    private void HandleAvoid()
    {
        if (!TryGetWaypoint(out var target))
        {
            ChangeState(BotState.Celebrate);
            return;
        }

        Vector3 travelDirection = DirectionTo(target.position, transform.forward);
        if (avoidDirection.sqrMagnitude < 0.001f)
            avoidDirection = PickAvoidanceDirection(travelDirection);

        FaceDirection(travelDirection);

        if (controller.isGrounded && !jumpedDuringAvoid)
        {
            verticalVelocity = jumpVelocity;
            jumpedDuringAvoid = true;
        }

        Vector3 sidestep = travelDirection * 0.92f + avoidDirection * 0.38f;
        ApplyMotion(sidestep.normalized * moveSpeed);
        avoidElapsed += Time.deltaTime;

        if (avoidElapsed >= avoidDuration && !IsObstacleAhead(travelDirection))
            ChangeState(BotState.Climb);
    }

    private void HandleRecover()
    {
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

        if (celebrationElapsed >= Mathf.Max(0.05f, celebrationDuration))
            ChangeState(BotState.Reset);
    }

    private void HandleReset()
    {
        if (resetApplied)
            return;

        resetApplied = true;
        TeleportToSpawn();
        RoundIndex++;
        Debug.Log($"Round {RoundIndex}: reset to spawn", this);
        waypointIndex = FindNextWaypoint(0);
        jumpedForWaypoint = false;
        verticalVelocity = 0f;

        ChangeState(configured && TryGetWaypoint(out _) ? BotState.Climb : BotState.Idle);
    }

    private void ApplyMotion(Vector3 horizontalVelocity)
    {
        if (controller == null || !controller.enabled)
            return;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity -= gravity * Time.deltaTime;
        Vector3 motion = horizontalVelocity;
        motion.y = verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    private void TeleportToSpawn()
    {
        Transform destination = spawnPoint != null ? spawnPoint : transform;
        controller.enabled = false;
        transform.SetPositionAndRotation(destination.position, destination.rotation);
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    private void ChangeState(BotState nextState)
    {
        if (State == nextState)
            return;

        State = nextState;

        switch (nextState)
        {
            case BotState.Avoid:
                avoidElapsed = 0f;
                jumpedDuringAvoid = false;
                avoidDirection = Vector3.zero;
                break;

            case BotState.Recover:
                recoverUntil = Time.time + Mathf.Max(0.05f, recoverDuration);
                if (recoverDirection.sqrMagnitude < 0.001f)
                    recoverDirection = -transform.forward;
                break;

            case BotState.Celebrate:
                celebrationElapsed = 0f;
                break;

            case BotState.Reset:
                resetApplied = false;
                break;
        }
    }

    private bool HasReachedWaypoint(Transform target)
    {
        if (!controller.isGrounded)
            return false;

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        // A jump can land on a higher overlapping step before reaching this waypoint.
        return offset.sqrMagnitude <= waypointReachDistance * waypointReachDistance &&
               transform.position.y >= target.position.y - waypointVerticalTolerance;
    }

    private bool TryGetWaypoint(out Transform waypoint)
    {
        waypointIndex = FindNextWaypoint(waypointIndex);
        if (waypointIndex >= waypoints.Length)
        {
            waypoint = null;
            return false;
        }

        waypoint = waypoints[waypointIndex];
        return waypoint != null;
    }

    private int FindNextWaypoint(int startIndex)
    {
        if (waypoints == null)
            return 0;

        int index = Mathf.Max(0, startIndex);
        while (index < waypoints.Length && waypoints[index] == null)
            index++;
        return index;
    }

    private bool HasUsableWaypoints()
    {
        return FindNextWaypoint(0) < (waypoints == null ? 0 : waypoints.Length);
    }

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
        if (turnSpeed <= 0f || direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private Vector3 PickAvoidanceDirection(Vector3 travelDirection)
    {
        Vector3 side = Vector3.Cross(Vector3.up, travelDirection).normalized;
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;

        Vector3 origin = transform.position + Vector3.up * obstacleProbeHeight;
        bool leftBlocked = RaycastForObstacle(origin, side, obstacleProbeDistance * 0.8f);
        bool rightBlocked = RaycastForObstacle(origin, -side, obstacleProbeDistance * 0.8f);

        if (leftBlocked && !rightBlocked)
            return -side;
        return side;
    }

    private bool IsObstacleAhead(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * obstacleProbeHeight;
        if (RaycastForObstacle(origin, direction, obstacleProbeDistance))
            return true;

        // A second ray lets the bot spot taller greybox obstacles without seeing platforms.
        return RaycastForObstacle(origin + Vector3.up * 0.55f, direction, obstacleProbeDistance);
    }

    private bool RaycastForObstacle(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            raycastHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            if (IsObstacleCollider(raycastHits[i].collider))
                return true;
        }

        return false;
    }

    private bool IsObstacleCollider(Collider collider)
    {
        if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform))
            return false;

        return HasName(collider.transform, "Obstacle");
    }

    private static bool HasName(Transform candidate, string expected)
    {
        while (candidate != null)
        {
            if (string.Equals(candidate.name, expected, StringComparison.OrdinalIgnoreCase))
                return true;
            candidate = candidate.parent;
        }

        return false;
    }

    private bool IsBelowSpawn()
    {
        if (spawnPoint == null)
            return false;

        return transform.position.y < spawnPoint.position.y - Mathf.Max(0.1f, fallBelowSpawnDistance);
    }

    private static bool IsSummitCollider(Collider collider)
    {
        return collider != null && HasName(collider.transform, "Summit");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsSummitCollider(other))
            ChangeState(BotState.Celebrate);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (IsSummitCollider(hit.collider))
        {
            ChangeState(BotState.Celebrate);
            return;
        }

        if (!IsObstacleCollider(hit.collider) || State == BotState.Celebrate || State == BotState.Reset ||
            Time.time < recoverUntil)
            return;

        recoverDirection = hit.normal;
        recoverDirection.y = 0f;
        if (recoverDirection.sqrMagnitude < 0.001f)
            recoverDirection = -transform.forward;
        recoverDirection.Normalize();
        ChangeState(BotState.Recover);
    }
}
