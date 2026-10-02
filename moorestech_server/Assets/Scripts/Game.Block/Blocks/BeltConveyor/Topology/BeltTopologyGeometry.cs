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
            if (offset.z == 1) direction = BeltDirection.Front;
            else if (offset.z == -1) direction = BeltDirection.Back;
            else if (offset.x == -1) direction = BeltDirection.Left;
            else direction = BeltDirection.Right;
            return true;
        }

        // 受け側から見た搬入元の向きに、送り側と受け側の高さ比較を足す
        // Combine the source direction seen from the receiver with the source-vs-receiver height comparison
        public static BeltEntryDirection GetEntryDirection(BeltDirection sourceDirectionFromReceiver, int sourceY, int receiverY)
        {
            if (receiverY < sourceY) return BeltEntryDirections.FromAbove(sourceDirectionFromReceiver);
            if (sourceY < receiverY) return BeltEntryDirections.FromBelow(sourceDirectionFromReceiver);
            return BeltEntryDirections.Level(sourceDirectionFromReceiver);
        }
    }
}
