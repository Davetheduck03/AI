using UnityEngine;

/// <summary>
/// Attach this to any unit that should reveal fog of war.
/// Automatically reveals area around the unit.
/// </summary>
public class UnitVision : MonoBehaviour
{
    [SerializeField] private float updateInterval = 0.2f;

    private FogOfWarManager fogManager;
    private float lastUpdateTime;

    private void Start()
    {
        fogManager = FindAnyObjectByType<FogOfWarManager>();

        if (fogManager == null)
        {
            Debug.LogWarning("UnitVision: No FogOfWarManager found in scene!");
        }
    }

    private void Update()
    {
        if (fogManager == null) return;

        if (Time.time - lastUpdateTime >= updateInterval)
        {
            fogManager.RevealFogAroundPosition(transform.position);
            lastUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Force immediate fog update
    /// </summary>
    public void ForceUpdateFog()
    {
        if (fogManager != null)
        {
            fogManager.RevealFogAroundPosition(transform.position);
        }
    }
}
