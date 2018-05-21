using UnityEngine;

public class SimpleBarricade : Obstacle
{
    protected const int k_MinObstacleCount = 1;
    protected const int k_MaxObstacleCount = 2;
    protected const int k_LeftMostLaneIndex = -1;
    protected const int k_RightMostLaneIndex = 1;
    
	public override void Spawn(TrackSegment segment, float t, int l)
    {
        Vector3 position;
        Quaternion rotation;
        segment.GetPointAt(t, out position, out rotation);

        int lane = l;

        GameObject obj = Instantiate(gameObject, position, rotation);
        obj.transform.position += obj.transform.right * lane * segment.manager.laneOffset;

        obj.transform.SetParent(segment.objectRoot, true);
    }
}
