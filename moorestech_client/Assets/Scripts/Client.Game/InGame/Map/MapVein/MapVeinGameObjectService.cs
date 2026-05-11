using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    public class MapVeinGameObjectService
    {
        public Transform Transform { get; }
        
        public MapVeinGameObjectService(Transform transform)
        {
            Transform = transform;
        }

        // transformとboundsからMin/Max座標を計算
        // Calculate Min/Max world positions from transform and bounds
        public Vector3Int MinPosition(Bounds bounds) => new(
            Mathf.RoundToInt(Transform.position.x - bounds.size.x / 2f + bounds.center.x),
            Mathf.RoundToInt(Transform.position.y - bounds.size.y / 2f + bounds.center.y),
            Mathf.RoundToInt(Transform.position.z - bounds.size.z / 2f + bounds.center.z));

        public Vector3Int MaxPosition(Bounds bounds) => new(
            Mathf.RoundToInt(Transform.position.x + bounds.size.x / 2f + bounds.center.x),
            Mathf.RoundToInt(Transform.position.y + bounds.size.y / 2f + bounds.center.y),
            Mathf.RoundToInt(Transform.position.z + bounds.size.z / 2f + bounds.center.z));

        // サイズを最小1に丸め、偶数/奇数でcenterオフセットを調整して正規化
        // Normalize bounds: clamp size to min 1, adjust center offset for even/odd dimensions
        public static Bounds NormalizeBounds(Bounds bounds)
        {
            var size = bounds.size;
            var sizeX = size.x < 1 ? 1 : Mathf.RoundToInt(size.x);
            var sizeY = size.y < 1 ? 1 : Mathf.RoundToInt(size.y);
            var sizeZ = size.z < 1 ? 1 : Mathf.RoundToInt(size.z);
            bounds.size = new Vector3(sizeX, sizeY, sizeZ);

            var centerX = sizeX % 2f == 0 ? 0 : 0.5f;
            var centerY = sizeY % 2f == 0 ? 0 : 0.5f;
            var centerZ = sizeZ % 2f == 0 ? 0 : 0.5f;
            bounds.center = new Vector3(centerX, centerY, centerZ);

            return bounds;
        }

        // Gizmo描画用のワールド空間Boundsを返す
        // Return world-space bounds for Gizmo rendering
        public void DrowGizmo(Bounds bounds, Color color)
        {
            var gizmoBounds = new Bounds();
            gizmoBounds.SetMinMax(MinPosition(bounds), MaxPosition(bounds));

            color.a = 0.5f;
            Gizmos.color = color;
            Gizmos.DrawCube(gizmoBounds.center, gizmoBounds.size);
        }
    }
}
