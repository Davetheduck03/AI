using UnityEngine;
using TMPro;

/// <summary>
/// Displays "Player 1", "Player 2", etc. in world-space above the hero's head.
///
/// HOW TO SET UP IN THE EDITOR:
///   1. On each hero prefab, add a child GameObject (e.g. "NameLabel").
///   2. Add a Canvas component to it set to World Space, then add a
///      TextMeshPro - Text (UI) component as a grandchild.
///   3. Add this UnitNameUI script to the "NameLabel" GameObject.
///   4. Drag the TextMeshPro component into the "Name Label" field.
///   5. Position the Canvas/text just above the character sprite.
///
/// The label reads playerIndex from the parent BaseHero set by DungeonSpawner,
/// so it always reflects the correct player number regardless of which prefab is used.
/// </summary>
public class UnitNameUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameLabel;

    private void Start()
    {
        if (nameLabel == null)
        {
            Debug.LogWarning("[UnitNameUI] No TextMeshProUGUI assigned — label will not display.");
            return;
        }

        // Walk up the hierarchy to find the BaseHero (handles any nesting depth)
        var hero = GetComponentInParent<BaseHero>();
        if (hero != null)
        {
            nameLabel.text = $"Player {hero.playerIndex + 1}";
        }
        else
        {
            // Fallback: show the GameObject name so something appears in the editor
            nameLabel.text = transform.root.name;
            Debug.LogWarning($"[UnitNameUI] No BaseHero found in parent hierarchy of '{gameObject.name}'. " +
                             "Make sure this component is a descendant of a GameObject with BaseHero.");
        }
    }
}
