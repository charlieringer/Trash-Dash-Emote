using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// This defines a "piece" of the track. This is attached to the prefab and contains data such as what obstacles can spawn on it.
/// It also defines places on the track where obstacles can spawn. The prefab is placed into a ThemeData list.
/// </summary>
public class TrackSegment : MonoBehaviour
{
    public Transform pathParent;
    public TrackManager manager;

	public Transform objectRoot;
	public Transform collectibleTransform;

    public Obstacle[] possibleObstacles;

	public bool hasLowBarrier;
	public bool hasHighBarrier;

	public int[] obstaclesPresent;

	public int coinLane;
    public float worldLength { get { return m_WorldLength; } }

    protected float m_WorldLength;

    void OnEnable()
    {
        UpdateWorldLength();

		GameObject obj = new GameObject("ObjectRoot");
		obj.transform.SetParent(transform);
		objectRoot = obj.transform;

		obj = new GameObject("Collectibles");
		obj.transform.SetParent(objectRoot);
		collectibleTransform = obj.transform;
    }

    // Same as GetPointAt but using an interpolation parameter in world units instead of 0 to 1.
    public void GetPointAtInWorldUnit(float wt, out Vector3 pos, out Quaternion rot)
    {
        float t = wt / m_WorldLength;
        GetPointAt(t, out pos, out rot);
    }


	// Interpolation parameter t is clamped between 0 and 1.
	public void GetPointAt(float t, out Vector3 pos, out Quaternion rot)
    {
        float clampedT = Mathf.Clamp01(t);
        float scaledT = (pathParent.childCount - 1) * clampedT;
        int index = Mathf.FloorToInt(scaledT);
        float segmentT = scaledT - index;

        Transform orig = pathParent.GetChild(index);
        if (index == pathParent.childCount - 1)
        {
            pos = orig.position;
            rot = orig.rotation;
            return;
        }

        Transform target = pathParent.GetChild(index + 1);

        pos = Vector3.Lerp(orig.position, target.position, segmentT);
        rot = Quaternion.Lerp(orig.rotation, target.rotation, segmentT);
    }

    protected void UpdateWorldLength()
    {
        m_WorldLength = 0;

        for (int i = 1; i < pathParent.childCount; ++i)
        {
            Transform orig = pathParent.GetChild(i - 1);
            Transform end = pathParent.GetChild(i);

            Vector3 vec = end.position - orig.position;
            m_WorldLength += vec.magnitude;
        }
    }

	public void Cleanup()
	{
		while(collectibleTransform.childCount > 0)
		{
			Transform t = collectibleTransform.GetChild(0);
			t.SetParent(null);
            Coin.coinPool.Free(t.gameObject);
		}

		Destroy(gameObject);
	}
}

