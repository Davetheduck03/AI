using System.Collections;
using UnityEngine;

/// <summary>
/// A dropped item in the world. Assign item to auto-update the sprite.
/// Triggers EquipmentComponent.TryEquipWithAssessment when an adventurer walks over it.
/// </summary>
public class WorldItem : MonoBehaviour
{
	[SerializeField] private SpriteRenderer spriteRenderer;

	// How long after spawning before the pickup trigger becomes active.
	// Prevents the dropping hero from immediately re-absorbing the item.
	private const float PickupDelay = 0.15f;

	private Collider2D _collider;
	private bool _pickupReady = false;

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

		_collider = GetComponent<Collider2D>();
	}

	private void Start()
	{
		// Disable the relic pickup trigger briefly after spawning so the hero
		// who opened the chest isn't standing on top of it when it activates.
		// (Equipment items are no longer picked up on contact — see OnTriggerEnter2D.)
		if (_collider != null)
		{
			_collider.enabled = false;
			StartCoroutine(EnablePickupAfterDelay());
		}
		else
		{
			_pickupReady = true;
		}
	}

	private IEnumerator EnablePickupAfterDelay()
	{
		yield return new WaitForSeconds(PickupDelay);
		if (_collider != null)
			_collider.enabled = true;
		_pickupReady = true;
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
		if (!_pickupReady) return;
		if (_item == null) return;

		// ── Relic pickup ──────────────────────────────────────────────────
		// Relics are not managed by the behaviour-tree item system, so they
		// are still picked up on contact.
		if (_item is RelicSO relic)
		{
			RelicHolder holder = other.GetComponent<RelicHolder>();
			if (holder == null) return;   // Only heroes with RelicHolder can pick it up

			holder.PickupRelic(relic);
			Debug.Log($"[WorldItem] Relic '{relic.itemName}' picked up by {other.name}");
			Destroy(gameObject);
			return;
		}

		// ── Normal equipment ──────────────────────────────────────────────
		// Equipment is handled exclusively by the behaviour tree:
		//   EvaluateNearbyItems → MoveTowardsTarget → PickupItem
		// Equipping on contact here ran in parallel with the BT path, causing
		// heroes to silently fill a second armour slot while walking to their
		// intended pickup target. The BT already evaluates items every tick via
		// WorldItemRegistry, so nothing is lost by skipping contact pickup.
	}
}