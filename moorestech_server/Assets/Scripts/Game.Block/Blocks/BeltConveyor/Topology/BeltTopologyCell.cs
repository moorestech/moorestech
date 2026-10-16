using Core.BeltTransport;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // ベルト1ブロック分のマスと、その入出力接続の不変スナップショット
    // Immutable snapshot of one belt block's cell and its input/output connections
    public sealed class BeltTopologyCell
    {
        public readonly Vector3Int Position;
        public readonly BlockInstanceId BlockInstanceId;
        public readonly IBlock Block;
        // ブロックの向きから求めた搬送の正面方向
        // Forward transport direction derived from the block direction
        public readonly BeltDirection Forward;
        public readonly int BeltSpeedPerTick;
        // 歯車を持ち、回転数で速度が変わるマスか
        // Whether the cell carries a gear and runs at a rotation-dependent speed
        public readonly bool IsGearDriven;
        // 歯車を持ち、回転数で速度が変わるマスか
        // Whether the cell carries a gear and runs at a rotation-dependent speed
        public readonly bool IsGearDriven;
        // マスタの出力コネクターが複数ある分配器か（接続数とは無関係）
        // Splitter-type belt with several master output connectors, regardless of how many are connected
        public readonly bool IsSplitter;
        // 方向値→相手マス座標の順に並んだ、件数ぴったりの配列
        // Exactly sized arrays ordered by direction value, then partner cell position
        public readonly BeltTopologyConnection[] Inputs;
        public readonly BeltTopologyConnection[] Outputs;

        public BeltTopologyCell(Vector3Int position, BlockInstanceId blockInstanceId, IBlock block, BeltDirection forward, int beltSpeedPerTick, bool isGearDriven, bool isSplitter,
            BeltTopologyConnection[] inputs, BeltTopologyConnection[] outputs)
        {
            Position = position;
            BlockInstanceId = blockInstanceId;
            Block = block;
            Forward = forward;
            BeltSpeedPerTick = beltSpeedPerTick;
            IsGearDriven = isGearDriven;
            IsSplitter = isSplitter;
            Inputs = inputs;
            Outputs = outputs;
        }
    }
}
