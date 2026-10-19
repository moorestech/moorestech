using Client.Game.InGame.DistanceCulling;
using UnityEngine;

namespace Client.Tests
{
    /// <summary>
    /// - 範囲内外の通知回数を記録
    /// - 最後の通知値を保持
    /// - Record within/outside-range notification counts
    /// - Keep the last notification value
    /// </summary>
    internal sealed class FakeCullingTarget : IDistanceCullingTarget
    {
        public int OnCount;
        public int OffCount;
        public bool? Last;
        public int TotalCalls => OnCount + OffCount;

        public void SetWithinRange(bool withinRange)
        {
            if (withinRange) OnCount++;
            else OffCount++;
            Last = withinRange;
        }
    }

    internal static class CullingBounds
    {
        // グリッドセル(x,z)を所有する単一チャンク内に収まる微小AABB（境界跨ぎを避けるため最小角から正方向へ）
        // Tiny AABB inside the single chunk that owns cell (x,z) (grows from the min corner to avoid straddling borders)
        public static Bounds Point(float x, float z)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(x, 0f, z), new Vector3(x + 0.01f, 0f, z + 0.01f));
            return bounds;
        }

        public static Bounds Span(float minX, float minZ, float maxX, float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, maxZ));
            return bounds;
        }
    }
}
