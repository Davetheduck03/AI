using UnityEngine;

/// <summary>
/// A dropped item in the world. Assign item to auto-update the sprite.
/// Triggers EquipmentComponent.TryEquipWithAssessment when an adventurer walks over it.
/// </summary>
public class WorldItem : MonoBehaviour
{
	[SerializeField] private SpriteRenderer spriteRenderer;

	[SerializeField] private ItemSO _item;
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

		// Unity 2D requires a Rigidbody2D on at least one object for OnTriggerEnter2D
		// to fire. Use kinematic so physics doesn't move the dropped item.
		if (GetComponent<Rigidbody2D>() == null)
		{
			var rb = gameObject.AddComponent<Rigidbody2D>();
			rb.bodyType = RigidbodyType2D.Kinematic;
			rb.simulated = true;
		}
	}

	private void OnEnable()  => WorldItemRegistry.Register(this);
	private void OnDisable() => WorldItemRegistry.Unregister(this);

	private void UpdateSprite()
	{
		if (spriteRenderer == null) return;
		spriteRenderer.sprite = (_item != null) ? _item.Icon : null;
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (_item == null) return;

		// ── Relic pickup ──────────────────────────────────────────────────
		if (_item is RelicSO relic)
		{
			RelicHolder holder = other.GetComponent<RelicHolder>();
			if (holder == null) return;   // Only heroes with RelicHolder can pick it up

			holder.PickupRelic(relic);
			Debug.Log($"[WorldItem] Relic '{relic.itemName}' picked up by {other.name}");
			Destroy(gameObject);
			return;
		}

		// ── Normal equipment pickup ───────────────────────────────────────
		EquipmentComponent equipment = other.GetComponent<EquipmentComponent>();
		if (equipment == null) return;

		bool equipped = equipment.TryEquipWithAssessment(_item, transform.position);
		Debug.Log($"[WorldItem] {_item.itemName} {(equipped ? "equipped by" : "rejected by")} {other.name}");
		Destroy(gameObject);
	}
}