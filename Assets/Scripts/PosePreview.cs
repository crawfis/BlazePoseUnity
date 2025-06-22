using UnityEngine;

public class PosePreview : MonoBehaviour
{
    public BoundingBox boundingBox;
    public BoundingCircle boundingCircle;
    public Keypoint[] keyPoints;

    public void SetActive(bool active)
    {
        gameObject?.SetActive(active);
    }

    public void SetBoundingBox(bool active, Vector3 position, Vector2 size)
    {
        boundingBox?.Set(active, position, size);
    }

    public void SetBoundingCircle(bool active, Vector3 position, float radius)
    {
        boundingCircle?.Set(active, position, radius);
    }

    public void SetKeyPoint(int index, bool active, Vector3 position)
    {
        keyPoints[index]?.Set(active, position);
    }
}
