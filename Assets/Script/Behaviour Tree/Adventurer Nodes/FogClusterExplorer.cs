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
    [SerializeField] private float clusterRadius = 10f;  // How far to look for fog clusters
    [SerializeField] private int minClusterSize = 10;    // Minimum tiles to be a cluster
    [SerializeField] private float sampleDistance = 3f;  // Distance between sample points

    [Header("Priority Settings")]
    [SerializeField] private float healthThreshold = 0.5f;  // Health required to explore large clusters

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
    /// </summary>
    public Vector3? GetBestExplorationTarget(Vector3 fromPosition, float currentHealthPercent)
    {
        if (fogManager == null) return null;

        List<Vector3> unrevealedTiles = fogManager.GetUnrevealedPositions();

        if (unrevealedTiles.Count == 0)
        {
            Debug.Log("FogClusterExplorer: No unrevealed tiles left");
            return null;
        }

        // Find fog clusters
        List<FogCluster> clusters = FindFogClusters(unrevealedTiles, fromPosition);

        if (clusters.Count == 0)
        {
            // No clusters nearby, just return nearest fog
            return GetNearestFog(unrevealedTiles, fromPosition);
        }

        // Filter based on health
        List<FogCluster> validClusters = FilterClustersByHealth(clusters, currentHealthPercent);

        if (validClusters.Count == 0)
        {
            Debug.Log("FogClusterExplorer: All clusters too dangerous for current health");
            // If health too low, explore small safe areas
            return GetNearestFog(unrevealedTiles, fromPosition);
        }

        // Get best cluster (largest if prioritizing, nearest otherwise)
        FogCluster bestCluster = validClusters.OrderBy(c => c.distanceFromPlayer).First();

        Debug.Log($"FogClusterExplorer: Targeting cluster of {bestCluster.size} tiles at {bestCluster.center}");
        return bestCluster.center;
    }

    /// <summary>
    /// Find clusters of unrevealed fog tiles.
    /// </summary>
    private List<FogCluster> FindFogClusters(List<Vector3> unrevealedTiles, Vector3 fromPosition)
    {
        List<FogCluster> clusters = new List<FogCluster>();
        HashSet<Vector3> processedTiles = new HashSet<Vector3>();

        // Sample points instead of checking every tile (performance)
        List<Vector3> samplePoints = SampleFogPoints(unrevealedTiles);

        foreach (Vector3 samplePoint in samplePoints)
        {
            if (processedTiles.Contains(samplePoint)) continue;

            // Count nearby fog tiles to determine cluster size
            List<Vector3> clusterTiles = GetNearbyFog(samplePoint, unrevealedTiles, clusterRadius);

            if (clusterTiles.Count < minClusterSize) continue;

            // Mark these tiles as processed
            foreach (Vector3 tile in clusterTiles)
            {
                processedTiles.Add(tile);
            }

            // Create cluster
            Vector3 clusterCenter = CalculateCenter(clusterTiles);
            float distance = Vector3.Distance(fromPosition, clusterCenter);

            FogCluster cluster = new FogCluster
            {
                center = clusterCenter,
                size = clusterTiles.Count,
                distanceFromPlayer = distance,
                tiles = clusterTiles
            };

            clusters.Add(cluster);
        }

        return clusters;
    }

    /// <summary>
    /// Sample fog points to reduce computation (every Nth tile).
    /// </summary>
    private List<Vector3> SampleFogPoints(List<Vector3> allFog)
    {
        List<Vector3> samples = new List<Vector3>();

        for (int i = 0; i < allFog.Count; i += Mathf.Max(1, (int)(sampleDistance)))
        {
            samples.Add(allFog[i]);
        }

        return samples;
    }

    /// <summary>
    /// Get all fog tiles within radius of a point.
    /// </summary>
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

    /// <summary>
    /// Filter clusters based on current health.
    /// Low health = avoid large clusters (might have enemies).
    /// </summary>
    private List<FogCluster> FilterClustersByHealth(List<FogCluster> clusters, float healthPercent)
    {
        if (healthPercent >= healthThreshold)
        {
            // Healthy - can explore any cluster
            return clusters;
        }

        // Low health - only explore small, safe clusters
        int maxSafeSize = Mathf.RoundToInt(minClusterSize * 1.5f);
        return clusters.Where(c => c.size <= maxSafeSize).ToList();
    }

    /// <summary>
    /// Get nearest single fog tile (fallback).
    /// </summary>
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

    /// <summary>
    /// Calculate center point of a list of positions.
    /// </summary>
    private Vector3 CalculateCenter(List<Vector3> positions)
    {
        if (positions.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (Vector3 pos in positions)
        {
            sum += pos;
        }
        return sum / positions.Count;
    }

    /// <summary>
    /// Draw clusters in Scene view for debugging.
    /// </summary>
    private void OnDrawGizmos()
    {
        // This would need to be called from Update to work properly
        // Left as exercise for visualization
    }

    private class FogCluster
    {
        public Vector3 center;
        public int size;
        public float distanceFromPlayer;
        public List<Vector3> tiles;
    }
}