using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry of all active WorldItems in the scene.
/// WorldItem registers/unregisters itself on enable/disable, so behaviour tree
/// nodes can query a maintained list instead of calling FindGameObjectsWithTag every tick.
/// </summary>
public static class WorldItemRegistry
{
    private static readonly List<WorldItem> _items = new List<WorldItem>();

    public static void Register(WorldItem item)
    {
        if (!_items.Contains(item))
            _items.Add(item);
    }

    public static void Unregister(WorldItem item)
    {
        _items.Remove(item);
    }

    /// <summary>Returns a snapshot of all currently registered WorldItems.</summary>
    public static IReadOnlyList<WorldItem> All => _items;
}
