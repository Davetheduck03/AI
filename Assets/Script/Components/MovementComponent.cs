using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseTK
{

    public class MovementComponent : UnitComponent
    {
        public float movement_Speed;
        private UnitPathFollower agent;
        public LayerMask nodeLayer;

        [Tooltip("Assign a Transform that represents the movement goal (e.g. the end waypoint).")]
        public Transform targetTransform;

        protected override void OnInitialize()
        {
            movement_Speed = data.Speed;
            agent = GetComponent<UnitPathFollower>();
        }

        private PathNode CheckTarget()
        {
            return NodeGetter.GetNodeBelow(targetTransform.position + Vector3.up * 0.5f, nodeLayer);
        }

        public void OnTriggerMove()
        {

            PathNode start = NodeGetter.GetNodeBelow(transform.position + Vector3.up * 0.5f, nodeLayer);
            PathNode goal = CheckTarget();

            if (start == null || goal == null) return;

            var cacheKey = (start, goal);
            List<PathNode> path;


            if (Astar.Instance.customPathCache.ContainsKey(cacheKey))
            {
                path = Astar.Instance.customPathCache[cacheKey];
                Debug.Log("Using custom path.");
                agent.SetPath(path, movement_Speed, this);
                return;
            }

            else if (Astar.Instance.generatedPathCache.ContainsKey(cacheKey))
            {
                path = Astar.Instance.generatedPathCache[cacheKey];
                Debug.Log("Using cached path.");
                agent.SetPath(path, movement_Speed, this);
                return;
            }

            else
            {
                path = Astar.Instance.FindPath(start, goal);
                Debug.Log("Computed new path.");
                agent.SetPath(path, movement_Speed, this);
            }

        }
    }
}