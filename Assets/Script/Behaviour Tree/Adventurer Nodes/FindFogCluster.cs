using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ACTION: Finds a fog cluster to explore and exposes it as bb["target"].
///
/// Three systems make exploration feel human rather than mechanical:
///
/// 1. HERO VARIANT — each hero has a stable 0-6 index derived from its instance ID.
///    When candidates are ranked by score, every hero picks a different rank, so the
///    party naturally fans out across separate regions of the dungeon instead of all
///    converging on the same optimal cluster.
///
/// 2. EXPLORATION MEMORY — the hero permanently remembers every grid tile it has
///    stepped on.  When scoring cluster candidates, points around each cluster are
///    sampled and checked against this tile set.  Clusters surrounded by already-
///    visited tiles are penalised, so heroes naturally prefer genuinely fresh regions.
///    Unlike the old time-limited log, this memory never expires — tiles stay
///    remembered for the entire run.
///
/// 3. SCATTER — a small random offset (≤ ScatterRadius units) is applied to the
///    final target.  Heroes sharing a cluster don't walk to the exact same tile, and
///    paths look organic rather than geometrically identical every run.
///
/// 4. TWO-PASS SEARCH — clusters are first searched locally (within LocalSearchRadius).
///    If none are found the hero enters backtrack mode and the search opens up to the
///    entire map, sending the hero back toward unexplored territory far away.
///
/// The cluster search is throttled to ClusterRefreshInterval seconds and the target
/// GO is reused across ticks so MoveTowardsTarget doesn't restart A* every frame.
/// </summary>
public class FindFogCluster : Node
{
    private readonly FogClusterExplorer _clusterExplorer;

    // ── Explore distances ─────────────────────────────────────────────────────
    private const float MinExploreDistance = 2.5f;
    private const float LocalSearchRadius  = 15f;

    // ── Refresh throttle ──────────────────────────────────────────────────────
    private const float ClusterRefreshInterval = 0.8f;
    private float _nextRefreshTime = 0f;

    // Persistent reusable GO — never destroyed between ticks, just repositioned.
    private GameObject _targetGO      = null;
    private bool       _hasValidTarget = false;

    // ── Hero variant ──────────────────────────────────────────────────────────
    private int _heroVariant = -1;

    // ── Tile memory ───────────────────────────────────────────────────────────
    // Every grid tile this hero has ever stepped on, stored as a quantised integer
    // coordinate.  The set grows throughout the run and never shrinks — tiles are
    // remembered permanently so the hero never revisits explored rooms just because
    // the old time-based log expired.
    //
    // TileSize should match the dungeon's grid cell size (world units per tile).
    // At 1 u/tile, a hero moving at 3 u/s records ~3 new tiles per second.
    // A 50×50 dungeon produces at most 2 500 entries — negligible memory cost.
    private readonly HashSet<Vector2Int> _visitedTiles = new();
    private const float TileSize = 1f;

    // Scoring: sample this many points around each cluster candidate.
    // The centre tile plus one ring of MemorySampleRingCount points at
    // MemorySampleRadius.  Score penalty = MemoryPenaltyMax × (visited / total).
    private const int   MemorySampleRingCount = 8;
    private const float MemorySampleRadius    = 5f;
    private const float MemoryPenaltyMax      = 0.85f;

    // How often to record the hero's current tile (every BT tick is fine since
    // HashSet.Add is O(1) and ignores duplicates automatically).
    // We record on every Evaluate() call — no gap check needed.

    // ── Backtrack state ───────────────────────────────────────────────────────
    private bool _backtracking = false;

    // ── Scatter ───────────────────────────────────────────────────────────────
    private const float ScatterRadius          = 1.3f;
    private const float ScatterRadiusBacktrack = 0.6f;

    // The cluster center that produced the current _targetGO position.
    // Scatter is re-rolled only when this changes by more than SameClusterThreshold,
    // i.e. when the hero genuinely switches to a different cluster.
    // Without this, every 0.8 s refresh rolls new scatter, moving _targetGO by up
    // to 2.6 u — above MoveTowardsTarget's TargetMovedThreshold (1.5 u) — which
    // cancels the in-progress path and starts a new one every interval, producing
    // constant twitching with no net forward movement.
    private Vector3 _lastClusterCenter        = new Vector3(float.MaxValue, 0f, 0f);
    private const float SameClusterThreshold  = 1.5f;

    // ── Constructor ───────────────────────────────────────────────────────────

    public FindFogCluster(Blackboard bb) : base(bb)
    {
        _clusterExplorer = Object.FindAnyObjectByType<FogClusterExplorer>();
        if (_clusterExplorer == null)
            Debug.LogWarning("FindFogCluster: No FogClusterExplorer in scene!");
    }

    // ── Evaluate ──────────────────────────────────────────────────────────────

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null || _clusterExplorer == null) return NodeState.Failure;

        // Assign variant once — stable for this hero's lifetime.
        if (_heroVariant < 0)
            _heroVariant = Mathf.Abs(self.GetInstanceID()) % 7;

        // Record the current tile on every tick — O(1), duplicates are silently ignored.
        _visitedTiles.Add(WorldToTile(self.position));

        if (Time.time < _nextRefreshTime && _targetGO != null && _hasValidTarget)
        {
            float distToTarget = Vector3.Distance(self.position, _targetGO.transform.position);

            if (distToTarget > 0.6f)
            {
                // Still travelling — honour the cache so the target stays stable.
                // Previously this always fell through when distToTarget < 1.0 f,
                // which re-rolled scatter mid-approach and made MoveTowardsTarget see
                // a "moved" target every 0.8 s, restarting A* and causing twitching.
                bb.Set("target", _targetGO.transform);
                return NodeState.Success;
            }

            // Hero has arrived at this cluster (within MoveTowardsTarget's 0.5 f
            // approach range + a small margin).  Expire the cache immediately so
            // the next Evaluate picks a genuinely new cluster.  Without this, the
            // same target is returned every tick and MoveTowardsTarget's arrival
            // check fires repeatedly with distance ≈ 0 — the hero never moves on.
            _nextRefreshTime   = 0f;
            _hasValidTarget    = false;
            _lastClusterCenter = new Vector3(float.MaxValue, 0f, 0f);
        }

        // ── Periodic refresh ─────────────────────────────────────────────────
        _nextRefreshTime = Time.time + ClusterRefreshInterval;

        float healthPct = 1f;
        var hc = self.GetComponent<HealthComponent>();
        if (hc != null) healthPct = hc.currentHealth / hc.maxHealth;

        // ── Two-pass cluster search ───────────────────────────────────────────
        // Pass 1: local clusters only (within LocalSearchRadius).
        List<Vector3> candidates = _clusterExplorer.GetRankedTargets(
            self.position, healthPct, MinExploreDistance,
            maxResults: 6, maxDistance: LocalSearchRadius);

        if (candidates.Count > 0)
        {
            if (_backtracking)
            {
                _backtracking = false;
                Debug.Log($"FindFogCluster [{self.name}]: local clusters found, leaving backtrack mode");
            }
        }
        else
        {
            // Pass 2: global search — backtrack to any unrevealed region on the map.
            candidates = _clusterExplorer.GetRankedTargets(
                self.position, healthPct, MinExploreDistance,
                maxResults: 6, maxDistance: float.MaxValue);

            if (candidates.Count > 0 && !_backtracking)
            {
                _backtracking = true;
                Debug.Log($"FindFogCluster [{self.name}]: no local clusters, entering backtrack mode");
            }
        }

        if (candidates.Count == 0)
        {
            _hasValidTarget = false;
            _backtracking   = false;
            Debug.Log($"FindFogCluster [{self.name}]: no unrevealed tiles remain");
            return NodeState.Failure;
        }

        // ── Score & pick ─────────────────────────────────────────────────────
        // Score = 1 - MemoryPenaltyMax × (fraction of sample points already visited).
        // A cluster entirely surrounded by visited tiles scores close to 0.15;
        // a cluster in completely fresh territory scores 1.0.
        float[] scores = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
            scores[i] = 1f - MemoryPenaltyMax * VisitedFraction(candidates[i]);

        // Build a score-sorted index list (highest first).
        List<int> ranked = new();
        for (int i = 0; i < candidates.Count; i++) ranked.Add(i);
        ranked.Sort((a, b) => scores[b].CompareTo(scores[a]));

        // Hero variant offsets into the ranked list so different heroes pick
        // different clusters while still preferring well-scored candidates.
        int pickRank   = _heroVariant % ranked.Count;
        int pickIdx    = ranked[pickRank];
        Vector3 center = candidates[pickIdx];

        // ── Scatter ──────────────────────────────────────────────────────────
        // Only re-scatter when switching to a genuinely different cluster.
        // If the same center is chosen again on refresh (common in mostly-explored
        // areas where the same cluster keeps winning), keep the existing _targetGO
        // position so MoveTowardsTarget never sees a "target moved" event and
        // never cancels the in-progress path.
        bool clusterChanged = Vector3.Distance(center, _lastClusterCenter) > SameClusterThreshold;

        if (_targetGO == null)
            _targetGO = new GameObject("_FogClusterTarget")
                        { hideFlags = HideFlags.HideAndDontSave };

        if (clusterChanged || !_hasValidTarget)
        {
            float   scatter   = _backtracking ? ScatterRadiusBacktrack : ScatterRadius;
            Vector2 scatter2D = Random.insideUnitCircle * scatter;
            Vector3 chosen    = center + new Vector3(scatter2D.x, scatter2D.y, 0f);

            _targetGO.transform.position = chosen;
            _lastClusterCenter           = center;
        }
        // else: same cluster → keep existing _targetGO.transform.position (no path restart)

        _hasValidTarget = true;

        Debug.Log($"FindFogCluster [{self.name}] v{_heroVariant} " +
                  $"{(_backtracking ? "[BACKTRACK]" : "[LOCAL]")}: " +
                  $"rank[{pickRank}] → cluster[{pickIdx}] @ {center} " +
                  $"score={scores[pickIdx]:F2} visited={_visitedTiles.Count} tiles");

        bb.Set("target", _targetGO.transform);
        return NodeState.Success;
    }

    // ── Memory helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns what fraction of sample points around <paramref name="center"/>
    /// the hero has already visited.  0 = completely fresh; 1 = fully explored.
    /// </summary>
    private float VisitedFraction(Vector3 center)
    {
        if (_visitedTiles.Count == 0) return 0f;

        int total   = 1 + MemorySampleRingCount;
        int visited = _visitedTiles.Contains(WorldToTile(center)) ? 1 : 0;

        float angleStep = 360f / MemorySampleRingCount * Mathf.Deg2Rad;
        for (int i = 0; i < MemorySampleRingCount; i++)
        {
            float   a      = i * angleStep;
            Vector3 sample = center + new Vector3(
                Mathf.Cos(a) * MemorySampleRadius,
                Mathf.Sin(a) * MemorySampleRadius, 0f);
            if (_visitedTiles.Contains(WorldToTile(sample))) visited++;
        }

        return (float)visited / total;
    }

    /// <summary>
    /// Converts a world position to a grid tile coordinate using <see cref="TileSize"/>.
    /// </summary>
    private static Vector2Int WorldToTile(Vector3 pos) =>
        new Vector2Int(
            Mathf.RoundToInt(pos.x / TileSize),
            Mathf.RoundToInt(pos.y / TileSize));
}
