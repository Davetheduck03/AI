using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Simple exploration system that finds clusters of unrevealed fog.
/// Better for open/corridor-style maps than rigid room detection.
/// Prioritizes exploring larger fog clusters.
/// </summary>
public class FogClusterExplorer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FogOfWarManager fogManager;

    [Header("Cluster Settings")]
    [SerializeField] private float clusterRadius = 10f;
    [SerializeField] private int minClusterSize = 10;
    [SerializeField] private float sampleDistance = 3f;

    [Header("Priority Settings")]
    [SerializeField] private float healthThreshold = 0.5f;

    private void Start()
    {
        if (fogManager == null)
        {
            fogManager = FindAnyObjectByType<FogOfWarManager>();
        }
    }

    /// <summary>
    /// Get the best fog cluster to explore from current position.
    /// Returns center of largest nearby fog cluster, considering health.
    /// Clusters closer than <paramref name="minDistance"/> are skipped; falls back to
    /// the nearest individual unrevealed tile when no qualifying cluster exists.
    /// </summary>
    public Vector3? GetBestExplorationTarget(Vector3 fromPosition, float currentHealthPercent, float minDistance = 0f)
    {
        if (fogManager == null) return null;

        List<Vector3> unrevealedTiles = fogManager.GetUnrevealedPositions();

        if (unrevealedTiles.Count == 0)
        {
            Debug.Log("FogClusterExplorer: No unrevealed tiles left");
            return null;
        }

        List<FogCluster> clusters = FindFogClusters(unrevealedTiles, fromPosition);

        if (clusters.Count == 0)
        {
            return GetNearestFog(unrevealedTiles, fromPosition);
        }

        List<FogCluster> validClusters = FilterClustersByHealth(clusters, currentHealthPercent);

        if (validClusters.Count == 0)
        {
            Debug.Log("FogClusterExplorer: All clusters too dangerous for current health");
            return GetNearestFog(unrevealedTiles, fromPosition);
        }

        // Skip clusters that are too close — we'd arrive without meaningful exploration.
        if (minDistance > 0f)
            validClusters = validClusters.Where(c => c.distanceFromPlayer >= minDistance).ToList();

        if (validClusters.Count == 0)
        {
            // All clusters are within minDistance; fall back to the nearest individual tile
            // that is at least minDistance away so the hero actually has to walk somewhere.
            Vector3? fallback = GetNearestFogBeyond(unrevealedTiles, fromPosition, minDistance);
            return fallback ?? GetNearestFog(unrevealedTiles, fromPosition);
        }

        FogCluster bestCluster = validClusters.OrderBy(c => c.distanceFromPlayer).First();

        Debug.Log($"FogClusterExplorer: Targeting cluster of {bestCluster.size} tiles at {bestCluster.center}");
        return bestCluster.center;
    }

    private List<FogCluster> FindFogClusters(List<Vector3> unrevealedTiles, Vector3 fromPosition)
    {
        List<FogCluster> clusters = new List<FogCluster>();
        HashSet<Vector3> processedTiles = new HashSet<Vector3>();

        List<Vector3> samplePoints = SampleFogPoints(unrevealedTiles);

        foreach (Vector3 samplePoint in samplePoints)
        {
            if (processedTiles.Contains(samplePoint)) continue;

            List<Vector3> clusterTiles = GetNearbyFog(samplePoint, unrevealedTiles, clusterRadius);

            if (clusterTiles.Count < minClusterSize) continue;

            foreach (Vector3 tile in clusterTiles)
            {
                processedTiles.Add(tile);
            }

            Vector3 clusterCenter = CalculateCenter(clusterTiles);
            float distance = Vector3.Distance(fromPosition, clusterCenter);

            FogCluster cluster = new FogCluster
            {
                center = clusterCenter,
                size = clusterTiles.Count,
                distanceFromPlayer = distance
            };

            clusters.Add(cluster);
        }

        return clusters;
    }

    private List<Vector3> SampleFogPoints(List<Vector3> allFog)
    {
        List<Vector3> samples = new List<Vector3>();

        for (int i = 0; i < allFog.Count; i += Mathf.Max(1, (int)sampleDistance))
        {
            samples.Add(allFog[i]);
        }

        return samples;
    }

    private List<Vector3> GetNearbyFog(Vector3 center, List<Vector3> allFog, float radius)
    {
        List<Vector3> nearbyFog = new List<Vector3>();

        foreach (Vector3 fog in allFog)
        {
            if (Vector3.Distance(center, fog) <= radius)
            {
                nearbyFog.Add(fog);
            }
        }

        return nearbyFog;
    }

    private List<FogCluster> FilterClustersByHealth(List<FogCluster> clusters, float healthPercent)
    {
        if (healthPercent >= healthThreshold)
        {
            return clusters;
        }

        int maxSafeSize = Mathf.RoundToInt(minClusterSize * 1.5f);
        return clusters.Where(c => c.size <= maxSafeSize).ToList();
    }

    private Vector3? GetNearestFog(List<Vector3> fogTiles, Vector3 fromPosition)
    {
        if (fogTiles.Count == 0) return null;

        Vector3 nearest = fogTiles[0];
        float closestDist = Vector3.Distance(fromPosition, nearest);

        foreach (Vector3 fog in fogTiles)
        {
            float dist = Vector3.Distance(fromPosition, fog);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = fog;
            }
        }

        return nearest;
    }

    private Vector3? GetNearestFogBeyond(List<Vector3> fogTiles, Vector3 fromPosition, float minDistance)
    {
        Vector3? nearest = null;
        float closestDist = float.MaxValue;

        foreach (Vector3 fog in fogTiles)
        {
            float dist = Vector3.Distance(fromPosition, fog);
            if (dist >= minDistance && dist < closestDist)
            {
                closestDist = dist;
                nearest = fog;
            }
        }

        return nearest;
    }

    private Vector3 CalculateCenter(List<Vector3> positions)
    {
        if (positions.Count == 0) return Vector3.zero;

        // Compute the arithmetic mean first…
        Vector3 sum = Vector3.zero;
        foreach (Vector3 pos in positions)
            sum += pos;
        Vector3 avg = sum / positions.Count;

        // …then return the actual tile closest to that mean.
        // Returning the raw average produces fractional coordinates like (5.22, 21.83)
        // that fall between tile centres and have no walkable PathNode, causing the
        // "not on walkable tile, finding nearest" spam and unstable navigation.
        Vector3 closest = positions[0];
        float bestDist = Vector3.Distance(avg, closest);
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
        public int size;
        public float distanceFromPlayer;
    }
}
