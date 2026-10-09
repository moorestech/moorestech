using Core.BeltTransport;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // moorestech座標（前+Z・後-Z・左-X・右+X）のマス差分をベルト方向へ変換する
    // Converts cell offsets in moorestech coordinates (Front=+Z, Back=-Z, Left=-X, Right=+X) into belt directions
    public static class BeltTopologyGeometry
    {
        // 水平1マス隣かつ高さ差1以内の差分だけを方向にできる
        // Only offsets one horizontal cell away with at most one cell of height difference map to a direction
        public static bool TryGetDirection(Vector3Int offset, out BeltDirection direction)
        {
            direction = BeltDirection.None;
            if (1 < Mathf.Abs(offset.y) || Mathf.Abs(offset.x) + Mathf.Abs(offset.z) != 1) return false;
            direction = HorizontalDirection(offset);
            return true;
        }

        // 水平の単位ベクトル(高さは無視)を方向にする。呼び出し側が単位ベクトルであることを保証する
        // Maps a horizontal unit vector (height ignored) to a direction; callers guarantee it is a unit vector
        public static BeltDirection HorizontalDirection(Vector3Int unitOffset)
        {
            if (unitOffset.z == 1) return BeltDirection.Front;
            if (unitOffset.z == -1) return BeltDirection.Back;
            return unitOffset.x == -1 ? BeltDirection.Left : BeltDirection.Right;
        }

        // 受け側から見た搬入元の向きに、送り側と受け側の高さ比較を足す
        // Combine the source direction seen from the receiver with the source-vs-receiver height comparison
        public static BeltEntryDirection GetEntryDirection(BeltDirection sourceDirectionFromReceiver, int sourceY, int receiverY)
        {
            if (receiverY < sourceY) return BeltEntryDirections.FromAbove(sourceDirectionFromReceiver);
            if (sourceY < receiverY) return BeltEntryDirections.FromBelow(sourceDirectionFromReceiver);
            return BeltEntryDirections.Level(sourceDirectionFromReceiver);
        }

        // マスの並び順はX→Y→Zの順に比べる
        // Cells are ordered by comparing X, then Y, then Z
        public static int ComparePosition(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }
    }
}
