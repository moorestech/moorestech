using Core.BeltTransport;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
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
        // 方向値→相手マス座標の順に並んだ、件数ぴったりの配列。入力は機械入力の規則で絞り済み
        // Exactly sized arrays ordered by direction value, then partner cell position; inputs are already narrowed by the machine input rule
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

        // 送り側ブロックとコネクター対が、規則で残った入力のどれかに一致するか
        // Whether the sender block and connector pair matches one of the inputs kept by the machine input rule
        public bool AcceptsInput(BlockInstanceId sourceBlockInstanceId, IBlockConnector sourceConnector, IBlockConnector targetConnector)
        {
            for (var i = 0; i < Inputs.Length; i++)
            {
                ref readonly var input = ref Inputs[i];
                if (input.PartnerBlock.BlockInstanceId == sourceBlockInstanceId && ReferenceEquals(input.SourceConnector, sourceConnector) &&
                    ReferenceEquals(input.TargetConnector, targetConnector)) return true;
            }
            return false;
        }
    }
}
