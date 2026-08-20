using System;
using UnityEngine;

// ワールド外接をローカル空間の外接へまとめる
// Collects world-space bounds and points into one bounding box in a reference transform's local space
public class WrapperLocalBoundsAccumulator
{
    private readonly Matrix4x4 _worldToLocal;
    private Bounds _bounds;

    public WrapperLocalBoundsAccumulator(Transform localSpace)
    {
        _worldToLocal = localSpace.worldToLocalMatrix;
    }

    public bool HasPoint { get; private set; }

    // ワールドAABBの8隅を入れる。ローカル空間が回転していても取りこぼさない
    // Feeds all eight corners of a world AABB so nothing is lost when the local space is rotated
    public void AddWorldBounds(Bounds worldBounds)
    {
        for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            AddWorldPoint(new Vector3(
                (cornerIndex & 1) == 0 ? worldBounds.min.x : worldBounds.max.x,
                (cornerIndex & 2) == 0 ? worldBounds.min.y : worldBounds.max.y,
                (cornerIndex & 4) == 0 ? worldBounds.min.z : worldBounds.max.z));
    }

    private void AddWorldPoint(Vector3 worldPoint)
    {
        AddLocalPoint(_worldToLocal.MultiplyPoint3x4(worldPoint));
    }

    public void AddLocalPoint(Vector3 localPoint)
    {
        if (!HasPoint)
        {
            _bounds = new Bounds(localPoint, Vector3.zero);
            HasPoint = true;
            return;
        }

        _bounds.Encapsulate(localPoint);
    }

    public Bounds GetBounds()
    {
        if (!HasPoint) throw new InvalidOperationException("no point was accumulated");
        return _bounds;
    }
}
