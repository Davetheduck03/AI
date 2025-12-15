using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple Lootable script for manual UI setup.
/// Create the canvas and fill bar manually in Unity Editor.
/// </summary>
public class Lootable : MonoBehaviour
{
    [Header("Loot Settings")]
    [SerializeField] private float lootDuration = 2f;
    [SerializeField] private GameObject lootReward;

    [Header("UI References - Assign Manually")]
    [SerializeField] private GameObject lootBarUI;
    [SerializeField] private Image fillImage;

    public static event Action<Lootable> OnLootComplete;

    private Coroutine lootCoroutine;
    public bool isLooting = false;
    public bool isLooted = false;

    private void Start()
    {
        HideLootBar();
    }

    public void Loot()
    {
        if (isLooted || isLooting) return;

        Debug.Log($"[Lootable] Starting loot on {gameObject.name}");
        lootCoroutine = StartCoroutine(LootCoroutine());
    }

    public void CancelLoot()
    {
        if (lootCoroutine != null)
        {
            StopCoroutine(lootCoroutine);
            lootCoroutine = null;
        }

        isLooting = false;
        HideLootBar();
        Debug.Log("[Lootable] Loot cancelled");
    }

    private IEnumerator LootCoroutine()
    {
        isLooting = true;
        ShowLootBar();

        float elapsed = 0f;

        while (elapsed < lootDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lootDuration);

            if (fillImage != null)
            {
                fillImage.fillAmount = progress;
            }

            yield return null;
        }

        // Ensure fill is complete
        if (fillImage != null)
        {
            fillImage.fillAmount = 1f;
        }

        yield return new WaitForSeconds(0.2f);

        CompleteLoot();
    }

    private void CompleteLoot()
    {
        isLooting = false;
        isLooted = true;

        HideLootBar();

        OnLootComplete?.Invoke(this);

        if (lootReward != null)
        {
            Instantiate(lootReward, transform.position, Quaternion.identity);
        }

        Debug.Log($"[Lootable] {gameObject.name} looted and destroyed");
        Destroy(gameObject);
    }

    private void ShowLootBar()
    {
        if (lootBarUI != null)
        {
            lootBarUI.SetActive(true);
            Debug.Log("[Lootable] Loot bar shown");
        }
        else
        {
            Debug.LogWarning("[Lootable] Loot Bar UI not assigned!");
        }
    }

    private void HideLootBar()
    {
        if (lootBarUI != null)
        {
            lootBarUI.SetActive(false);
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }
    }

    private void OnDestroy()
    {
        if (lootCoroutine != null)
        {
            StopCoroutine(lootCoroutine);
        }
    }

    // Test in editor
    [ContextMenu("Test Loot")]
    private void TestLoot()
    {
        Loot();
    }
}