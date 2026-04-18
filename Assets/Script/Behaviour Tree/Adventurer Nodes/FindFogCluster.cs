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
/// 2. EXPLORATION MEMORY — the node logs the hero's position every refresh.
///    Clusters close to recently visited positions are penalised, so heroes
///    naturally drift toward unexplored territory rather than circling the same rooms.
///
/// 3. SCATTER — a small random offset (≤ ScatterRadius units) is applied to the
///    final target.  Heroes sharing a cluster don't walk to the exact same tile, and
///    paths look organic rather than geometrically identical every run.
///
/// The cluster search is throttled to ClusterRefreshInterval seconds and the target
/// GO is reused across ticks so MoveTowardsTarget doesn't restart A* every frame.
/// </summary>
public class FindFogCluster : Node
{
    private readonly FogClusterExplorer _clusterExplorer;

    // ── Explore distances ─────────────────────────────────────────────────────
    // Clusters this close to the hero would be arrived-at almost immediately;
    // skip them and look for something further away.
    private const float MinExploreDistance = 2.5f;

    // ── Refresh throttle ──────────────────────────────────────────────────────
    // How often to re-run the cluster search. Between refreshes the cached target GO
    // is reused unchanged so MoveTowardsTarget does not restart A* every frame.
    private const float ClusterRefreshInterval = 1.5f;
    private float _nextRefreshTime = 0f;

    // Persistent reusable GO — never destroyed between ticks, just repositioned.
    private GameObject _targetGO      = null;
    private bool       _hasValidTarget = false;

    // ── Hero variant ──────────────────────────────────────────────────────────
    // A stable 0-6 index determined once from the hero's instance ID.
    // Different heroes pick different ranked candidates, spreading the party.
    private int _heroVariant = -1;

    // ── Exploration memory ────────────────────────────────────────────────────
    // A short circular log of world positions this hero recently visited.
    // Clusters near these positions are scored down so heroes prefer fresh territory.
    private struct MemoryEntry { public Vector3 pos; public float time; }
    private readonly List<MemoryEntry> _recentPositions = new();

    private const int   MemoryCapacity    = 7;     // max entries kept
    private const float MemoryDuration    = 40f;   // seconds before a position is forgotten
    private const float MemoryRecordGap   = 2.0f;  // min movement before a new entry is added
    private const float MemoryPenaltyRadius = 4.5f; // penalty radius around each memory entry
    private const float MemoryPenaltyMax    = 0.5f; // max penalty deducted per nearby memory

    // ── Scatter ───────────────────────────────────────────────────────────────
    // Random 2-D offset added to the chosen cluster centre.
    // Keeps heroes from always walking to the exact geometric tile every run.
    private const float ScatterRadius = 1.3f;

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

        if (Time.time < _nextRefreshTime && _targetGO != null && _hasValidTarget)
        {
            bb.Set("target", _targetGO.transform);
            return NodeState.Success;
        }

        // ── Periodic refresh ─────────────────────────────────────────────────
        _nextRefreshTime = Time.time + ClusterRefreshInterval;

        // Log current position and prune stale entries.
        RecordPosition(self.position);
        PruneMemory();

        float healthPct = 1f;
        var hc = self.GetComponent<HealthComponent>();
        if (hc != null) healthPct = hc.currentHealth / hc.maxHealth;

        // Get multiple ranked cluster centres.
        List<Vector3> candidates = _clusterExplorer.GetRankedTargets(
            self.position, healthPct, MinExploreDistance, maxResults: 6);

        if (candidates.Count == 0)
        {
            _hasValidTarget = false;
            Debug.Log($"FindFogCluster [{self.name}]: no unrevealed tiles remain");
            return NodeState.Failure;
        }

        // ── Score & pick ─────────────────────────────────────────────────────
        // Each candidate starts at score 1.  Nearby memory entries subtract a
        // fraction of MemoryPenaltyMax proportional to proximity.
        float[] scores = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            scores[i] = 1f;
            foreach (var entry in _recentPositions)
            {
                float d = Vector3.Distance(candidates[i], entry.pos);
                if (d < MemoryPenaltyRadius)
                    scores[i] -= MemoryPenaltyMax * (1f - d / MemoryPenaltyRadius);
            }
        }

        // Build a score-sorted index list (highest first).
        List<int> ranked = new();
        for (int i = 0; i < candidates.Count; i++) ranked.Add(i);
        ranked.Sort((a, b) => scores[b].CompareTo(scores[a]));

        // Hero variant offsets into the ranked list so different heroes pick
        // different clusters while still preferring well-scored candidates.
        int pickRank  = _heroVariant % ranked.Count;
        int pickIdx   = ranked[pickRank];
        Vector3 center = candidates[pickIdx];

        // ── Scatter ──────────────────────────────────────────────────────────
        Vector2 scatter2D = Random.insideUnitCircle * ScatterRadius;
        Vector3 chosen    = center + new Vector3(scatter2D.x, scatter2D.y, 0f);

        // ── Update the persistent GO ─────────────────────────────────────────
        if (_targetGO == null)
            _targetGO = new GameObject("_FogClusterTarget")
                        { hideFlags = HideFlags.HideAndDontSave };

        _targetGO.transform.position = chosen;
        _hasValidTarget = true;

        Debug.Log($"FindFogCluster [{self.name}] v{_heroVariant}: " +
                  $"rank[{pickRank}] → cluster[{pickIdx}] @ {center} " +
                  $"score={scores[pickIdx]:F2} scatter=({scatter2D.x:F1},{scatter2D.y:F1})");

        bb.Set("target", _targetGO.transform);
        return NodeState.Success;
    }

    // ── Memory helpers ────────────────────────────────────────────────────────

    private void RecordPosition(Vector3 pos)
    {
        // Skip if the hero hasn't moved far enough since the last recorded point.
        if (_recentPositions.Count > 0 &&
            Vector3.Distance(_recentPositions[^1].pos, pos) < MemoryRecordGap)
            return;

        _recentPositions.Add(new MemoryEntry { pos = pos, time = Time.time });

        // Trim to capacity — oldest entry goes first.
        while (_recentPositions.Count > MemoryCapacity)
            _recentPositions.RemoveAt(0);
    }

    private void PruneMemory()
    {
        float cutoff = Time.time - MemoryDuration;
        _recentPositions.RemoveAll(e => e.time < cutoff);
    }
}
