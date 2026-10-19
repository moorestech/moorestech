using Core.BeltTransport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // 既存の解決済み接続1件を、所有マスから見た向きと受け側の進入方向で表す
    // One existing resolved connection, expressed as the direction from the owning cell and the entry direction at the receiver
    public readonly struct BeltTopologyConnection
    {
        // 所有マスから見た相手の水平方向
        // Horizontal direction of the partner as seen from the owning cell
        public readonly BeltDirection Direction;
        // 受け取る側のマスから見た12通りの進入方向
        // 12-way entry direction as seen from the receiving cell
        public readonly BeltEntryDirection EntryDirection;
        public readonly BeltTopologyPartnerKind PartnerKind;
        public readonly IBlock PartnerBlock;
        // ベルトなら原点、機械なら接続edgeを挟んで隣接する機械側のマス
        // Belt origin for belts, or the machine cell adjacent across the connected edge for machines
        public readonly Vector3Int PartnerCell;
        // 送り側の出力コネクターと受け側の入力コネクター、受け側のインベントリ
        // Sender's output connector, receiver's input connector and the receiver's inventory
        public readonly IBlockConnector SourceConnector;
        public readonly IBlockConnector TargetConnector;
        public readonly IBlockInventory ReceiverInventory;

        public BeltTopologyConnection(BeltDirection direction, BeltEntryDirection entryDirection, BeltTopologyPartnerKind partnerKind, IBlock partnerBlock,
            Vector3Int partnerCell, IBlockConnector sourceConnector, IBlockConnector targetConnector, IBlockInventory receiverInventory)
        {
            Direction = direction;
            EntryDirection = entryDirection;
            PartnerKind = partnerKind;
            PartnerBlock = partnerBlock;
            PartnerCell = partnerCell;
            SourceConnector = sourceConnector;
            TargetConnector = targetConnector;
            ReceiverInventory = receiverInventory;
        }
    }
}
