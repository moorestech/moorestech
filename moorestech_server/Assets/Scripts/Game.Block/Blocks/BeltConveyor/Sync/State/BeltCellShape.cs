using Core.BeltTransport;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 全量に載せるマス1つ。座標と搬送の正面方向だけを持ち、blockの個体は持たない
    // One cell in the full state; carries only its position and forward transport direction, never the block instance
    public readonly struct BeltCellShape
    {
        public readonly Vector3Int Position;
        public readonly BeltDirection Forward;

        public BeltCellShape(Vector3Int position, BeltDirection forward)
        {
            Position = position;
            Forward = forward;
        }
    }
}
