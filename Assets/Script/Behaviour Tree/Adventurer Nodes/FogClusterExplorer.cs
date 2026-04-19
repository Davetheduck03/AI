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
    [SerializeField] private int   minClusterSize = 6;
    [SerializeField] private float sampleDistance = 3f;

    [Header("Priority Settings")]
    [SerializeField] private float healthThreshold = 0.5f;

    private void Start()
    {
        if (fogManager == null)
            fogManager = FindAnyObjectByType<FogOfWarManager>();
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

        List<Vector3> unrevealedTiles = fogManager.GetUnrevealedPositions();
        if (unrevealedTiles.Count == 0) return result;

        List<FogCluster> clusters = FindFogClusters(unrevealedTiles, fromPosition);
        List<FogCluster> valid    = FilterClustersByHealth(clusters, currentHealthPercent);

        if (minDistance > 0f)
            valid = valid.Where(c => c.distanceFromPlayer >= minDistance).ToList();
        if (maxDistance < float.MaxValue)
            valid = valid.Where(c => c.distanceFromPlayer <= maxDistance).ToList();

        // Largest clusters first — most unexplored area wins.
        valid.Sort((a, b) => b.size.CompareTo(a.size));

        foreach (var c in valid)
        {
            result.Add(c.center);
            if (result.Count >= maxResults) return result;
        }

        // Pad with individual fog tiles when clusters are sparse.
        // Only add a tile-fallback if it also satisfies the distance constraints.
        if (result.Count < maxResults)
        {
            Vector3? extra = GetNearestFogInRange(unrevealedTiles, fromPosition,
                                                  minDistance, maxDistance);
            if (extra.HasValue && !result.Contains(extra.Value))
                result.Add(extra.Value);
        }

        return result;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private List<FogCluster> FindFogClusters(List<Vector3> unrevealedTiles, Vector3 fromPosition)
    {
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
