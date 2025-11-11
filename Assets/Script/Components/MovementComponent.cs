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

        public void OnTriggerMove()
        {

            PathNode start = NodeGetter.GetClosestNode(transform.position, nodeLayer);
            PathNode goal = NodeGetter.GetClosestNode(targetTransform.position, nodeLayer);

            List<PathNode> path = Astar.Instance.FindPath(start, goal);
            Debug.Log("Computed new path.");
            agent.SetPath(path, movement_Speed, this);

        }
    }
}