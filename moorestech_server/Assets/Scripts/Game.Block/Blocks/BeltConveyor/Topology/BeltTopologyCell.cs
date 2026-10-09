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
        // マスタの出力コネクターが複数ある分配器か（接続数とは無関係）
        // Splitter-type belt with several master output connectors, regardless of how many are connected
        public readonly bool IsSplitter;
        // 方向値→相手マス座標の順に並んだ、件数ぴったりの配列
        // Exactly sized arrays ordered by direction value, then partner cell position
        public readonly BeltTopologyConnection[] Inputs;
        public readonly BeltTopologyConnection[] Outputs;

        public BeltTopologyCell(Vector3Int position, BlockInstanceId blockInstanceId, IBlock block, BeltDirection forward, int beltSpeedPerTick, bool isSplitter,
            BeltTopologyConnection[] inputs, BeltTopologyConnection[] outputs)
        {
            Position = position;
            BlockInstanceId = blockInstanceId;
            Block = block;
            Forward = forward;
            BeltSpeedPerTick = beltSpeedPerTick;
            IsSplitter = isSplitter;
            Inputs = inputs;
            Outputs = outputs;
        }
    }
}
