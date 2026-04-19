using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseEnemy : BaseUnit
{
    // Auto-attach the fog-hiding component so every enemy in the dungeon is
    // invisible while standing on an unexplored tile.  Using Start (not Awake)
    // ensures BaseUnit.Awake has already run and UnitComponent setup is complete.
    private void Start()
    {
        if (GetComponent<EnemyFogVisibility>() == null)
            gameObject.AddComponent<EnemyFogVisibility>();
    }
}
