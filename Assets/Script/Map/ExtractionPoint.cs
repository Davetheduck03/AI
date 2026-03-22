using UnityEngine;

/// <summary>
/// Marks the extraction point (the dungeon entrance / start room).
/// DungeonSpawner repositions this to the centre of room 0 after each generation.
///
/// The hero's BT SetExtractionTarget node reads Instance.transform as its navigation target.
/// Place this GameObject in the scene — it is never destroyed between rounds.
/// </summary>
public class ExtractionPoint : MonoBehaviour
{
    public static ExtractionPoint Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Moves the extraction point to a new world position (called by DungeonSpawner).</summary>
    public void SetPosition(Vector3 worldPos)
    {
        transform.position = worldPos;
        Debug.Log($"[ExtractionPoint] Moved to {worldPos}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, "EXIT");
    }
#endif
}
