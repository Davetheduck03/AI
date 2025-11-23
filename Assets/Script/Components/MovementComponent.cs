using System.Collections.Generic;
using UnityEngine;

public class MovementComponent : UnitComponent
{
    public float movement_Speed;
    private UnitPathFollower agent;


    protected override void OnInitialize()
    {
        movement_Speed = data.Speed;
        agent = GetComponent<UnitPathFollower>();
    }

    public void OnTriggerMove(Transform self, Transform target)
    {
        PathNode start = NodeGetter.Instance.GetClosestNode(self.position);
        PathNode goal = NodeGetter.Instance.GetClosestNode(target.position);

        Debug.Log("Path Found");

        if (start == null || goal == null)
        {
            Debug.LogError("Failed to find start or goal node!");
            return;
        }

        Astar.Instance.FindPath(start, goal, (path) =>
        {
            if (path != null && path.Count > 0)
                agent.SetPath(path, movement_Speed, this);
            else
                Debug.Log("No path found!");
        });
    }
}
