using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a finite set of platforms and tower body modules arranged as an
/// apparently endless helix. Platform roots sit at their walkable surface;
/// route targets are the CharacterController's standing-center positions.
/// </summary>
[DisallowMultipleComponent]
public sealed class ContinuousTower : MonoBehaviour
{
    [Serializable]
    public struct RouteTarget
    {
        public int Index;
        public Vector3 SurfacePosition;
        public Vector3 Position;
        public Vector3 Outward;
        public Vector3 Tangent;
    }

    [Header("Scene references")]
    [SerializeField] private Transform towerCenter;
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private GameObject bodyPrefab;
    [SerializeField] private Transform effectsRoot;

    [Header("Helix")]
    [SerializeField, Min(0.1f)] private float routeRadius = 4.05f;
    [SerializeField, Min(1f)] private float angularStepDegrees = 24f;
    [SerializeField, Min(0.05f)] private float risePerPlatform = 0.65f;
    [SerializeField] private float initialThetaDegrees = -12f;
    [SerializeField, Min(0.1f)] private float standingCenterHeight = 0.72f;

    [Header("Pools")]
    [SerializeField, Min(8)] private int platformPoolSize = 32;
    [SerializeField, Min(2)] private int bodyPoolSize = 4;
    [SerializeField, Min(0.5f)] private float bodySegmentHeight = 7.8f;
    [SerializeField, Min(2)] private int retainedPlatformsBehind = 10;
    [SerializeField, Min(0)] private int retainedBodySegmentsBehind = 1;

    [Header("Floating origin")]
    [SerializeField, Min(16f)] private float rebaseHeight = 128f;
    [SerializeField, Min(0.5f)] private float fallBelowHighestPlatform = 3f;

    [Header("Runtime progress")]
    [SerializeField] private int activePlatformCount;
    [SerializeField] private int activeBodyCount;
    [SerializeField] private int recycles;
    [SerializeField] private int rebases;
    [SerializeField] private int highestLandedRouteIndex;
    [SerializeField] private float highestLandedFootHeight;
    [SerializeField] private float currentHeight;
    [SerializeField] private float recordHeight;
    [SerializeField] private int milestone;
    [SerializeField] private int highestMilestone;
    [SerializeField] private Vector3 originOffset;
    [SerializeField] private int announcedLandedMilestone;
    [SerializeField] private float maxActivePoolLocalHeight;
    [SerializeField] private bool poolPositionsFinite = true;

    // Stored as double because this is the logical climb height; only the
    // finite local remainder is ever written to Unity transforms.
    private double logicalOriginHeight;
    private double highestLandedLogicalHeight;
    private double logicalCurrentHeight;
    private double logicalRecordHeight;

    public int ActivePlatformCount => activePlatformCount;
    public int ActiveBodyCount => activeBodyCount;
    public int Recycles => recycles;
    public int Rebases => rebases;
    public int HighestLandedRouteIndex => highestLandedRouteIndex;
    public float HighestLandedFootHeight => highestLandedFootHeight;
    public float CurrentHeight => currentHeight;
    public float RecordHeight => recordHeight;
    public int Milestone => milestone;
    public int HighestMilestone => highestMilestone;
    public Vector3 OriginOffset => originOffset;
    public double LogicalOriginHeight => logicalOriginHeight;
    public double LogicalCurrentHeight => logicalCurrentHeight;
    public double LogicalRecordHeight => logicalRecordHeight;
    public float MaxActivePoolLocalHeight => maxActivePoolLocalHeight;
    public bool PoolPositionsFinite => poolPositionsFinite;
    public float StandingCenterHeight => standingCenterHeight;
    public Transform EffectsRoot => effectsRoot;
    public float RebaseHeight => rebaseHeight;
    public float FallBelowHighestPlatform => fallBelowHighestPlatform;
    public bool IsConfigured => platformPrefab != null && (configured || platformPoolRoot != null);

    public event Action<Vector3> Rebased;
    public event Action RouteReset;
    public event Action<int, float> MilestoneReached;

    private struct PoolItem
    {
        public GameObject Instance;
        public int RouteIndex;
    }

    private readonly List<PoolItem> platformPool = new();
    private readonly List<PoolItem> bodyPool = new();
    private readonly HashSet<Transform> floatingTransforms = new();

    [SerializeField] private Transform platformPoolRoot;
    [SerializeField] private Transform bodyPoolRoot;
    private bool configured;
    private float routeBaseHeight;
    private int lastRequestedTargetIndex = 1;

    private void Awake()
    {
        if (towerCenter == null)
            towerCenter = transform;
        configured = platformPrefab != null;
        routeBaseHeight = towerCenter.position.y;
        RebuildRuntimePoolLists();
    }

    /// <summary>
    /// Builds fixed platform/body pools. The platform prefab root must be its
    /// foot/surface position, face outward with local +Z, and own its collider.
    /// </summary>
    public void Configure(
        Transform center,
        GameObject platform,
        GameObject body,
        Transform dynamicEffectsRoot = null,
        int requestedPlatformPoolSize = 32,
        int requestedBodyPoolSize = 4)
    {
        towerCenter = center != null ? center : transform;
        platformPrefab = platform;
        bodyPrefab = body;
        effectsRoot = dynamicEffectsRoot;
        platformPoolSize = Mathf.Max(8, requestedPlatformPoolSize);
        bodyPoolSize = Mathf.Max(2, requestedBodyPoolSize);
        retainedPlatformsBehind = Mathf.Clamp(retainedPlatformsBehind, 2, platformPoolSize - 3);
        routeBaseHeight = towerCenter.position.y;
        logicalOriginHeight = 0d;
        originOffset = Vector3.zero;
        configured = platformPrefab != null;

        RebuildPools();
        ResetRoute();
    }

    public void SetEffectsRoot(Transform dynamicEffectsRoot)
    {
        effectsRoot = dynamicEffectsRoot;
    }

    public void RegisterFloatingTransform(Transform candidate)
    {
        if (candidate != null)
            floatingTransforms.Add(candidate);
    }

    public void UnregisterFloatingTransform(Transform candidate)
    {
        if (candidate != null)
            floatingTransforms.Remove(candidate);
    }

    /// <summary>Returns the platform-root position at foot/surface height.</summary>
    public Vector3 GetPlatformSurfacePosition(int routeIndex)
    {
        routeIndex = Mathf.Max(0, routeIndex);
        Transform center = towerCenter != null ? towerCenter : transform;
        double worldHeight = routeBaseHeight + routeIndex * (double)risePerPlatform - logicalOriginHeight;
        Vector3 horizontal = center.position + OutwardForIndex(routeIndex) * routeRadius;
        horizontal.y = (float)worldHeight;
        return horizontal;
    }

    /// <summary>Returns the CharacterController center position above the platform root.</summary>
    public Vector3 GetStandingPosition(int routeIndex)
    {
        return GetPlatformSurfacePosition(routeIndex) + Vector3.up * standingCenterHeight;
    }

    public float GetThetaDegrees(int routeIndex)
    {
        return initialThetaDegrees + (float)(Math.Max(0, routeIndex) * (double)angularStepDegrees % 360d);
    }

    public float GetAzimuthDegrees(Vector3 worldPosition)
    {
        Vector3 center = (towerCenter != null ? towerCenter.position : transform.position);
        Vector3 relative = worldPosition - center;
        return Mathf.Atan2(relative.x, -relative.z) * Mathf.Rad2Deg;
    }

    public float GetAzimuthDelta(Vector3 worldPosition)
    {
        return Mathf.DeltaAngle(initialThetaDegrees, GetAzimuthDegrees(worldPosition));
    }

    public bool TryGetTarget(int routeIndex, out RouteTarget target)
    {
        target = default;
        if (!IsConfigured)
            return false;

        routeIndex = Mathf.Max(0, routeIndex);
        EnsureRouteAround(routeIndex);

        Vector3 outward = OutwardForIndex(routeIndex);
        target = new RouteTarget
        {
            Index = routeIndex,
            SurfacePosition = GetPlatformSurfacePosition(routeIndex),
            Position = GetStandingPosition(routeIndex),
            Outward = outward,
            Tangent = TangentForIndex(routeIndex)
        };
        return true;
    }

    /// <summary>
    /// Recycles only positions far behind the requested target. The current
    /// support and several recovery platforms remain present below the Bot.
    /// </summary>
    public void EnsureRouteAround(int targetIndex)
    {
        EnsureRouteAround(targetIndex, false);
    }

    private void EnsureRouteAround(int targetIndex, bool forceReposition)
    {
        if (!IsConfigured)
            return;

        RebuildRuntimePoolLists();
        if (platformPool.Count == 0)
            return;

        targetIndex = Mathf.Max(0, targetIndex);
        lastRequestedTargetIndex = targetIndex;
        int firstPlatform = Mathf.Max(0, targetIndex - retainedPlatformsBehind);
        bool changed = false;
        for (int offset = 0; offset < platformPool.Count; offset++)
        {
            int routeIndex = firstPlatform + offset;
            int slot = PositiveModulo(routeIndex, platformPool.Count);
            changed |= PlacePlatform(slot, routeIndex, forceReposition);
        }

        int targetBodySegment = (int)Math.Floor(targetIndex * (double)risePerPlatform / bodySegmentHeight);
        int firstBodySegment = targetBodySegment - retainedBodySegmentsBehind;
        for (int offset = 0; offset < bodyPool.Count; offset++)
        {
            int segmentIndex = firstBodySegment + offset;
            int slot = PositiveModulo(segmentIndex, bodyPool.Count);
            changed |= PlaceBody(slot, segmentIndex, forceReposition);
        }

        UpdatePoolPositionMetrics();
        if (changed && Application.isPlaying)
            Physics.SyncTransforms();
    }

    public void NotifyLanded(int routeIndex)
    {
        routeIndex = Mathf.Max(0, routeIndex);
        double landedHeight = GetRelativeSurfaceHeight(routeIndex);
        if (landedHeight <= highestLandedLogicalHeight + 0.0001d)
            return;

        highestLandedRouteIndex = routeIndex;
        highestLandedLogicalHeight = landedHeight;
        highestLandedFootHeight = ToDisplayHeight(landedHeight);
        ReportCurrentFootHeight(GetPlatformSurfacePosition(routeIndex).y);

        int landedMilestone = (int)Math.Floor(landedHeight / 25d);
        while (announcedLandedMilestone < landedMilestone)
        {
            announcedLandedMilestone++;
            MilestoneReached?.Invoke(announcedLandedMilestone, announcedLandedMilestone * 25f);
        }
    }

    /// <summary>
    /// Reports the Bot's physical foot altitude every physics tick. Progress is
    /// origin-safe and RecordHeight is intentionally never reset between rounds.
    /// </summary>
    public void ReportCurrentFootHeight(float footWorldHeight)
    {
        logicalCurrentHeight = Math.Max(0d, footWorldHeight + logicalOriginHeight - routeBaseHeight);
        logicalRecordHeight = Math.Max(logicalRecordHeight, logicalCurrentHeight);
        currentHeight = ToDisplayHeight(logicalCurrentHeight);
        recordHeight = ToDisplayHeight(logicalRecordHeight);
        milestone = Mathf.FloorToInt(currentHeight / 25f);
        highestMilestone = Mathf.Max(highestMilestone, milestone);
    }

    public bool HasFallenBelowHighest(float footWorldHeight)
    {
        double relativeFootHeight = footWorldHeight + logicalOriginHeight - routeBaseHeight;
        return relativeFootHeight < highestLandedLogicalHeight - Mathf.Max(0.5f, fallBelowHighestPlatform);
    }

    /// <summary>Moves the finite world back near zero while preserving logical height.</summary>
    public bool RebaseIfNeeded(Transform subject, float threshold = -1f)
    {
        if (!IsConfigured || subject == null)
            return false;

        float limit = threshold > 0f ? threshold : rebaseHeight;
        if (subject.position.y < limit)
            return false;

        RegisterFloatingTransform(subject);
        float desiredLocalHeight = limit * 0.5f;
        float amount = subject.position.y - desiredLocalHeight;
        float quantum = Mathf.Max(bodySegmentHeight, risePerPlatform);
        amount = Mathf.Max(quantum, Mathf.Floor(amount / quantum) * quantum);
        Vector3 shift = Vector3.up * amount;
        ShiftWorld(shift);
        logicalOriginHeight += amount;
        originOffset = new Vector3(0f, (float)logicalOriginHeight, 0f);
        EnsureRouteAround(lastRequestedTargetIndex, true);
        rebases++;
        Rebased?.Invoke(shift);
        return true;
    }

    /// <summary>
    /// Clears old dynamic effects and returns fixed pools to their initial route
    /// indices. Call this before teleporting a fallen Bot.
    /// </summary>
    public void ResetRoute()
    {
        logicalOriginHeight = 0d;
        originOffset = Vector3.zero;
        ClearDynamicEffects();
        highestLandedRouteIndex = 0;
        highestLandedLogicalHeight = 0d;
        highestLandedFootHeight = 0f;
        currentHeight = 0f;
        logicalCurrentHeight = 0d;
        milestone = 0;
        announcedLandedMilestone = 0;

        ResetPoolIndices(platformPool);
        ResetPoolIndices(bodyPool);
        EnsureRouteAround(1);
        RouteReset?.Invoke();
    }

    private void RebuildPools()
    {
        ClearPoolRoot(ref platformPoolRoot);
        ClearPoolRoot(ref bodyPoolRoot);
        platformPool.Clear();
        bodyPool.Clear();

        if (!configured)
            return;

        platformPoolRoot = CreatePoolRoot("Continuous platform pool");
        bodyPoolRoot = CreatePoolRoot("Continuous body pool");

        for (int i = 0; i < platformPoolSize; i++)
        {
            GameObject instance = Instantiate(platformPrefab, platformPoolRoot);
            instance.name = $"Continuous platform {i + 1:00}";
            instance.SetActive(false);
            platformPool.Add(new PoolItem { Instance = instance, RouteIndex = -1 });
        }

        if (bodyPrefab == null)
        {
            activePlatformCount = platformPool.Count;
            activeBodyCount = 0;
            return;
        }

        for (int i = 0; i < bodyPoolSize; i++)
        {
            GameObject instance = Instantiate(bodyPrefab, bodyPoolRoot);
            instance.name = $"Continuous tower body {i + 1:00}";
            instance.SetActive(false);
            bodyPool.Add(new PoolItem { Instance = instance, RouteIndex = -1 });
        }

        activePlatformCount = platformPool.Count;
        activeBodyCount = bodyPool.Count;
    }

    // Pool item indices are runtime-only. On an Editor-built scene reload, use
    // the serialized pool roots to restore the finite lists without cloning.
    private void RebuildRuntimePoolLists()
    {
        if (!NeedsPoolListRebuild(platformPool, platformPoolRoot) &&
            !NeedsPoolListRebuild(bodyPool, bodyPoolRoot))
            return;

        RebuildPoolList(platformPool, platformPoolRoot);
        RebuildPoolList(bodyPool, bodyPoolRoot);
        activePlatformCount = platformPool.Count;
        activeBodyCount = bodyPool.Count;
    }

    private static bool NeedsPoolListRebuild(List<PoolItem> pool, Transform root)
    {
        int childCount = root != null ? root.childCount : 0;
        if (pool.Count != childCount)
            return true;

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Instance == null || pool[i].Instance.transform.parent != root)
                return true;
        }
        return false;
    }

    private static void RebuildPoolList(List<PoolItem> pool, Transform root)
    {
        if (root == null)
        {
            pool.Clear();
            return;
        }

        pool.Clear();
        for (int i = 0; i < root.childCount; i++)
            pool.Add(new PoolItem { Instance = root.GetChild(i).gameObject, RouteIndex = -1 });
    }

    private Transform CreatePoolRoot(string name)
    {
        Transform parent = towerCenter != null ? towerCenter : transform;
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        return root;
    }

    private bool PlacePlatform(int slotIndex, int routeIndex, bool forceReposition)
    {
        PoolItem item = platformPool[slotIndex];
        if (item.Instance == null)
            return false;

        bool moved = forceReposition || item.RouteIndex != routeIndex || !item.Instance.activeSelf;
        if (!moved)
            return false;

        if (item.RouteIndex >= 0 && item.RouteIndex != routeIndex)
            recycles++;

        Vector3 outward = OutwardForIndex(routeIndex);
        item.Instance.transform.SetPositionAndRotation(
            GetPlatformSurfacePosition(routeIndex),
            Quaternion.LookRotation(outward, Vector3.up));
        item.Instance.SetActive(true);
        item.RouteIndex = routeIndex;
        platformPool[slotIndex] = item;

        item.Instance.GetComponent<ClimbPlatformVisual>()?.SetIndex(routeIndex);
        return true;
    }

    private bool PlaceBody(int slotIndex, int segmentIndex, bool forceReposition)
    {
        PoolItem item = bodyPool[slotIndex];
        if (item.Instance == null)
            return false;

        bool moved = forceReposition || item.RouteIndex != segmentIndex || !item.Instance.activeSelf;
        if (!moved)
            return false;

        if (item.RouteIndex >= 0 && item.RouteIndex != segmentIndex)
            recycles++;

        Transform center = towerCenter != null ? towerCenter : transform;
        double worldHeight = routeBaseHeight + segmentIndex * (double)bodySegmentHeight - logicalOriginHeight;
        Vector3 bodyPosition = center.position;
        bodyPosition.y = (float)worldHeight;
        item.Instance.transform.SetPositionAndRotation(
            bodyPosition,
            center.rotation);
        item.Instance.SetActive(true);
        item.RouteIndex = segmentIndex;
        bodyPool[slotIndex] = item;
        return true;
    }

    private void ResetPoolIndices(List<PoolItem> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            PoolItem item = pool[i];
            item.RouteIndex = -1;
            if (item.Instance != null)
                item.Instance.SetActive(false);
            pool[i] = item;
        }

        activePlatformCount = platformPool.Count;
        activeBodyCount = bodyPool.Count;
    }

    private void ShiftWorld(Vector3 downwardShift)
    {
        var roots = new List<Transform>();
        if (effectsRoot != null)
            for (int i = 0; i < effectsRoot.childCount; i++) AddShiftRoot(roots, effectsRoot.GetChild(i));
        foreach (Transform candidate in floatingTransforms)
            AddShiftRoot(roots, candidate);

        for (int i = 0; i < roots.Count; i++)
        {
            Transform candidate = roots[i];
            if (candidate == null || HasMovedAncestor(candidate, roots, i))
                continue;
            candidate.position -= downwardShift;
        }

        if (Application.isPlaying)
            Physics.SyncTransforms();
    }

    private void UpdatePoolPositionMetrics()
    {
        maxActivePoolLocalHeight = 0f;
        poolPositionsFinite = true;
        MeasurePool(platformPool);
        MeasurePool(bodyPool);
    }

    private void MeasurePool(List<PoolItem> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            GameObject instance = pool[i].Instance;
            if (instance == null || !instance.activeInHierarchy)
                continue;

            Vector3 local = instance.transform.localPosition;
            if (!IsFinite(local))
                poolPositionsFinite = false;
            maxActivePoolLocalHeight = Mathf.Max(maxActivePoolLocalHeight, Mathf.Abs(local.y));
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static void AddShiftRoot(List<Transform> roots, Transform candidate)
    {
        if (candidate != null && !roots.Contains(candidate))
            roots.Add(candidate);
    }

    private static bool HasMovedAncestor(Transform candidate, List<Transform> roots, int selfIndex)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            if (i == selfIndex || roots[i] == null)
                continue;
            if (candidate.IsChildOf(roots[i]))
                return true;
        }
        return false;
    }

    private void ClearDynamicEffects()
    {
        if (effectsRoot == null)
            return;

        for (int i = effectsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = effectsRoot.GetChild(i);
            child.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void ClearPoolRoot(ref Transform root)
    {
        if (root == null)
            return;

        if (Application.isPlaying)
            Destroy(root.gameObject);
        else
            DestroyImmediate(root.gameObject);
        root = null;
    }

    private Vector3 OutwardForIndex(int routeIndex)
    {
        float radians = GetThetaDegrees(routeIndex) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, -Mathf.Cos(radians));
    }

    private Vector3 TangentForIndex(int routeIndex)
    {
        float radians = GetThetaDegrees(routeIndex) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
    }

    private double GetRelativeSurfaceHeight(int routeIndex)
    {
        return Math.Max(0, routeIndex) * (double)risePerPlatform;
    }

    private static float ToDisplayHeight(double value)
    {
        return (float)Math.Min(float.MaxValue, Math.Max(0d, value));
    }

    private static int PositiveModulo(int value, int modulus)
    {
        return (value % modulus + modulus) % modulus;
    }
}
