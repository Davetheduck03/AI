using UnityEngine;

/// <summary>
/// A dropped item in the world. Assign item to auto-update the sprite.
/// Triggers EquipmentComponent.TryEquipWithAssessment when an adventurer walks over it.
/// </summary>
public class WorldItem : MonoBehaviour
{
	[SerializeField] private SpriteRenderer spriteRenderer;

	private ItemSO _item;
	public ItemSO item
	{
		get => _item;
		set
		{
			_item = value;
			UpdateSprite();
		}
	}

	private void Awake()
	{
		if (spriteRenderer == null)
			spriteRenderer = GetComponent<SpriteRenderer>();

		// If item was set before Awake (e.g. assigned in Inspector), apply it now
		if (_item != null)
			UpdateSprite();
	}

	private void UpdateSprite()
	{
		if (spriteRenderer == null) return;
		spriteRenderer.sprite = (_item != null) ? _item.Icon : null;
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (_item == null) return;

		EquipmentComponent equipment = other.GetComponent<EquipmentComponent>();
		if (equipment == null) return;

		equipment.TryEquipWithAssessment(_item, transform.position);
		Destroy(gameObject);
	}
}