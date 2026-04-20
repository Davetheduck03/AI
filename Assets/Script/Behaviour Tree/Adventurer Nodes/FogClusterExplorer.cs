using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Finds clusters of unrevealed fog tiles and ranks them for exploration.
///
/// Cluster radius is intentionally small (5 u) so heroes target precise local
/// pockets of fog rather than vague centroids of half-explored room groups.
/// A maxDistance parameter lets callers restrict the search to a local radius,
/// enabling the two-pass "explore locally → backtrack globally" pattern used
/// by FindFogCluster.
/// </summary>
public class FogClusterExplorer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FogOfWarManager fogManager;

    [Header("Cluster Settings")]
    // Radius used to group nearby fog tiles into one cluster.
    // Smaller = more granular targets; heroes aim at precise pockets rather
    // than room-sized blobs whose centre may already be explored.
    [SerializeField] private float clusterRadius  = 5f;

    // Minimum number of frontier tiles for a cluster to be considered valid.
    // With frontier-based exploration the candidate set is much smaller than with
    // all-unrevealed tiles, so we lower this from 10 to 4.  A frontier cluster
    // of 4 tiles represents a doorway or narrow corridor worth exploring.
    // The old reasoning (tiny rooms causing switching) no longer applies because
    // frontier clusters naturally shrink to zero as the hero enters a room,
    // rather than persisting as interior unrevealed tiles did.
    [SerializeField] private int   minClusterSize = 4;
    [SerializeField] private float sampleDistance = 3f;

    // ── Shared cluster cache ──────────────────────────────────────────────────
    // FogClusterExplorer is a MonoBehaviour singleton shared by all heroes.
    // FindFogClusters() does an O(n²) proximity scan over all unrevealed tiles —
    // running it once per hero (×4) per 0.8 s refresh interval causes a CPU spike
    // that skips movement frames and triggers false stuck-detection repaths.
    //
    // Fix: cache the raw cluster list for ClusterCacheLifetime seconds.  All heroes
    // read the same result between rebuilds; only one rebuild happens per interval
    // regardless of how many heroes are calling GetRankedTargets simultaneously.
    // The cache is longer than FindFogCluster's own 0.8 s refresh interval so the
    // first hero to refresh always hits a live cache and pays zero rebuild cost.
    private List<FogCluster> _cachedClusters    = null;
    private float            _cacheExpiry        = 0f;
    private const float      ClusterCacheLifetime = 2.0f;

    [Header("Priority Settings")]
    [SerializeField] private float healthThreshold = 0.5f;

    private void Start()
    {
        if (fogManager == null)
            fogManager = FindAnyObjectByType<FogOfWarManager>();
    }

    /// <summary>
    /// Invalidates the shared cluster cache so the next call to
    /// <see cref="GetRankedTargets"/> triggers a full rebuild.
    /// Call this whenever the fog state changes (tiles revealed) so heroes
    /// don't keep heading toward clusters that are already fully explored.
    /// <see cref="FogOfWarManager"/> should call this after each reveal pass.
    /// </summary>
    public void InvalidateClusterCache()
    {
        _cachedClusters = null;
        _cacheExpiry    = 0f;
    }

    /// <summary>
    /// Returns up to <paramref name="maxResults"/> cluster centres ranked by size
    /// (largest unexplored area first), filtered by health, minimum distance, and
    /// an optional maximum distance.  When <paramref name="maxDistance"/> is set,
    /// only clusters within that radius of <paramref name="fromPosition"/> are
    /// considered — pass <c>float.MaxValue</c> for an unlimited global search.
    /// Falls back to the nearest individual fog tile when clusters are sparse.
    /// </summary>
    public List<Vector3> GetRankedTargets(
        Vector3 fromPosition,
        float   currentHealthPercent,
        float   minDistance,
        int     maxResults  = 6,
        float   maxDistance = float.MaxValue)
    {
        var result = new List<Vector3>();
        if (fogManager == null) return result;

        // ── Frontier-based tile selection ─────────────────────────────────────
        // Use only frontier tiles (unrevealed walkable tiles adjacent to already-
        // revealed walkable tiles) rather than ALL unrevealed tiles.
        //
        // Benefits vs. clustering all unrevealed tiles:
        //   • Smaller candidate set: O(perimeter) not O(area) — typically 10–20×
        //     fewer tiles mid-dungeon, making the O(n²) cluster build proportionally
        //     cheaper and reducing per-frame CPU spikes.
        //   • Stable centroids: frontier clusters sit on the edge of explored space
        //     and shrink slowly as the hero approaches, not as distant tiles are
        //     revealed elsewhere.  Fewer centroid drift restarts mean fewer A* calls.
        //   • Natural behaviour: heroes expand the known boundary tile-by-tile
        //     (like a human explorer following walls to find doorways) rather than
        //     teleporting toward the middle of rooms they can't enter yet.
        //
        // Fall back to all-unrevealed only if the frontier is empty (first frame
        // before any tiles are revealed, or rare incremental-update lag).
        List<Vector3> candidateTiles = fogManager.GetFrontierPositions();
        if (candidateTiles.Count == 0)
            candidateTiles = fogManager.GetUnrevealedPositions();
        if (candidateTiles.Count == 0) return result;

        List<FogCluster> clusters = FindFogClusters(candidateTiles, fromPosition);
        List<FogCluster> valid    = FilterClustersByHealth(clusters, currentHealthPercent);

        if (minDistance > 0f)
            valid = valid.Where(c => c.distanceFromPlayer >= minDistance).ToList();
        if (maxDistance < float.MaxValue)
            valid = valid.Where(c => c.distanceFromPlayer <= maxDistance).ToList();

        // Largest clusters first — most unexplored frontier wins.
        valid.Sort((a, b) => b.size.CompareTo(a.size));

        foreach (var c in valid)
        {
            result.Add(c.center);
            if (result.Count >= maxResults) return result;
        }

        // Pad with individual frontier tiles when clusters are sparse.
        if (result.Count < maxResults)
        {
            Vector3? extra = GetNearestFogInRange(candidateTiles, fromPosition,
                                                  minDistance, maxDistance);
            if (extra.HasValue && !result.Contains(extra.Value))
                result.Add(extra.Value);
        }

        return result;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private List<FogCluster> FindFogClusters(List<Vector3> unrevealedTiles, Vector3 fromPosition)
    {
        // Return the shared cache when still valid.  All heroes that call this
        // within ClusterCacheLifetime seconds of the last rebuild share one result,
        // eliminating the per-hero O(n²) spike that was skipping movement frames.
        // distanceFromPlayer is re-computed per-caller below in GetRankedTargets so
        // distance filtering remains hero-specific despite the shared cluster list.
        if (_cachedClusters != null && Time.time < _cacheExpiry)
        {
            // Update distanceFromPlayer for this caller's position so the distance
            // filter in GetRankedTargets gives correct results with the cached data.
            foreach (var c in _cachedClusters)
                c.distanceFromPlayer = Vector3.Distance(fromPosition, c.center);
            return _cachedClusters;
        }

        // Cache miss — rebuild from scratch.
        var clusters       = new List<FogCluster>();
        var processedTiles = new HashSet<Vector3>();
        var samplePoints   = SampleFogPoints(unrevealedTiles);

        foreach (Vector3 samplePoint in samplePoints)
        {
            if (processedTiles.Contains(samplePoint)) continue;

            List<Vector3> clusterTiles = GetNearbyFog(samplePoint, unrevealedTiles, clusterRadius);
            if (clusterTiles.Count < minClusterSize) continue;

            foreach (Vector3 tile in clusterTiles)
                processedTiles.Add(tile);

            Vector3 clusterCenter = CalculateCenter(clusterTiles);
            clusters.Add(new FogCluster
            {
                center             = clusterCenter,
                size               = clusterTiles.Count,
                distanceFromPlayer = Vector3.Distance(fromPosition, clusterCenter)
            });
        }

        _cachedClusters = clusters;
        _cacheExpiry    = Time.time + ClusterCacheLifetime;
        return clusters;
    }

    private List<Vector3> SampleFogPoints(List<Vector3> allFog)
    {
        var samples = new List<Vector3>();
        int step    = Mathf.Max(1, (int)sampleDistance);
        for (int i = 0; i < allFog.Count; i += step)
            samples.Add(allFog[i]);
        return samples;
    }

    private List<Vector3> GetNearbyFog(Vector3 center, List<Vector3> allFog, float radius)
    {
        var nearby = new List<Vector3>();
        foreach (Vector3 fog in allFog)
            if (Vector3.Distance(center, fog) <= radius)
                nearby.Add(fog);
        return nearby;
    }

    private List<FogCluster> FilterClustersByHealth(List<FogCluster> clusters, float healthPercent)
    {
        if (healthPercent >= healthThreshold) return clusters;
        int maxSafeSize = Mathf.RoundToInt(minClusterSize * 1.5f);
        var safeOnly = clusters.Where(c => c.size <= maxSafeSize).ToList();
        // Prefer small clusters when low HP, but never block exploration entirely.
        // If all remaining fog is in large clusters (common late-game), fall back to
        // the full list so the hero still moves rather than standing still.
        return safeOnly.Count > 0 ? safeOnly : clusters;
    }

    /// <summary>Returns the nearest fog tile within [minDistance, maxDistance].</summary>
    private Vector3? GetNearestFogInRange(List<Vector3> fogTiles, Vector3 fromPosition,
                                          float minDist, float maxDist)
    {
        Vector3? nearest    = null;
        float    closestDist = float.MaxValue;

        foreach (Vector3 fog in fogTiles)
        {
            float dist = Vector3.Distance(fromPosition, fog);
            if (dist < minDist || dist > maxDist) continue;
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest     = fog;
            }
        }
        return nearest;
    }

    private Vector3 CalculateCenter(List<Vector3> positions)
    {
        if (positions.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (Vector3 pos in positions) sum += pos;
        Vector3 avg = sum / positions.Count;

        // Return the actual tile closest to the mean so the result snaps to a
        // valid tile centre rather than a fractional world position.
        Vector3 closest  = positions[0];
        float   bestDist = Vector3.Distance(avg, closest);
        for (int i = 1; i < positions.Count; i++)
        {
            float d = Vector3.Distance(avg, positions[i]);
            if (d < bestDist) { bestDist = d; closest = positions[i]; }
        }
        return closest;
    }

    private class FogCluster
    {
        public Vector3 center;
        public int     size;
        public float   distanceFromPlayer;
    }
}
