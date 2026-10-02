using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Topology
{
    // ワールドの解決済み接続（送り側中心）を反転し、ベルトのマスごとの入出力一覧を作る
    // Inverts the world's resolved source-centric connections into per-belt-cell input and output lists
    public static class BeltTopologyBuilder
    {
        public static List<BeltTopologyCell> Build(IWorldBlockDatastore world)
        {
            // 先にベルトのマスを確定し、その後に全ブロックの接続を振り分ける
            // Fix the belt cells first, then distribute every block's connections
            var forwards = new Dictionary<BlockInstanceId, BeltDirection>();
            var inputs = new Dictionary<BlockInstanceId, List<BeltTopologyConnection>>();
            var outputs = new Dictionary<BlockInstanceId, List<BeltTopologyConnection>>();
            foreach (var data in world.BlockMasterDictionary.Values) RegisterBelt(data.Block);
            foreach (var data in world.BlockMasterDictionary.Values) DistributeConnections(data.Block);

            // 配置順や辞書順に依存しないよう座標と方向で並べる
            // Sort by position and direction so the result never depends on placement or dictionary order
            var cells = new List<BeltTopologyCell>();
            foreach (var data in world.BlockMasterDictionary.Values)
            {
                var block = data.Block;
                if (!forwards.TryGetValue(block.BlockInstanceId, out var forward)) continue;
                var param = block.BlockMasterElement.BlockParam;
                var speed = param is BeltConveyorBlockParam belt ? belt.BeltSpeedPerTick : ((GearBeltConveyorBlockParam)param).BeltSpeedPerTick;
                var cellInputs = inputs[block.BlockInstanceId];
                var cellOutputs = outputs[block.BlockInstanceId];
                cellInputs.Sort(CompareConnection);
                cellOutputs.Sort(CompareConnection);
                cells.Add(new BeltTopologyCell(block.BlockPositionInfo.OriginalPos, block.BlockInstanceId, block, forward, speed, IsSplitter(param),
                    cellInputs, cellOutputs));
            }
            cells.Sort(CompareCell);
            return cells;

            #region Internal

            void RegisterBelt(IBlock block)
            {
                if (!IsBelt(block)) return;
                var position = block.BlockPositionInfo;
                // 水平姿勢でないベルトは前方向を持てないので一覧に載せない。プレイヤーが置ける通常の状態なのでログは出さない
                // A belt without a horizontal orientation has no forward direction and is left out. Players can place it normally, so no log
                if (position.BlockDirection is not (BlockDirection.North or BlockDirection.East or BlockDirection.South or BlockDirection.West) ||
                    !BeltTopologyGeometry.TryGetDirection(position.BlockDirection.ConvertLocalCell(Vector3Int.forward), out var forward)) return;
                forwards.Add(block.BlockInstanceId, forward);
                inputs.Add(block.BlockInstanceId, new List<BeltTopologyConnection>());
                outputs.Add(block.BlockInstanceId, new List<BeltTopologyConnection>());
            }

            void DistributeConnections(IBlock source)
            {
                if (!source.ComponentManager.TryGetComponent<IBlockConnectorComponent<IBlockInventory>>(out var connector)) return;
                foreach (var (receiverInventory, info) in connector.ConnectedTargets)
                {
                    var target = info.TargetBlock;
                    var sourceIsBelt = IsBelt(source);
                    var targetIsBelt = IsBelt(target);
                    // 機械同士の接続はベルトの接続図に含めない
                    // Machine-to-machine connections are not part of the belt topology
                    if (!sourceIsBelt && !targetIsBelt) continue;
                    if (sourceIsBelt && !forwards.ContainsKey(source.BlockInstanceId) || targetIsBelt && !forwards.ContainsKey(target.BlockInstanceId)) continue;

                    // 送り側から受け側へのマス差分で水平方向と高さを決める
                    // The cell offset from sender to receiver determines the horizontal direction and height
                    var sourceCell = EndpointCell(source, sourceIsBelt, info.SelfConnector);
                    var targetCell = EndpointCell(target, targetIsBelt, info.TargetConnector);
                    if (!BeltTopologyGeometry.TryGetDirection(targetCell - sourceCell, out var outputDirection))
                    {
                        Debug.LogWarning($"[BeltTopology] Connection {source.BlockInstanceId}@{sourceCell}->{target.BlockInstanceId}@{targetCell} is dropped: cells are not horizontally adjacent within one height step.");
                        continue;
                    }
                    var inputDirection = BeltDirections.Opposite(outputDirection);
                    var entryDirection = BeltTopologyGeometry.GetEntryDirection(inputDirection, sourceCell.y, targetCell.y);

                    if (sourceIsBelt)
                        outputs[source.BlockInstanceId].Add(new BeltTopologyConnection(outputDirection, entryDirection, KindOf(targetIsBelt), target, targetCell,
                            info.SelfConnector, info.TargetConnector, receiverInventory));
                    if (targetIsBelt)
                        inputs[target.BlockInstanceId].Add(new BeltTopologyConnection(inputDirection, entryDirection, KindOf(sourceIsBelt), source, sourceCell,
                            info.SelfConnector, info.TargetConnector, receiverInventory));
                }
            }

            #endregion
        }

        private static bool IsBelt(IBlock block)
        {
            var param = block.BlockMasterElement.BlockParam;
            return param is BeltConveyorBlockParam || param is GearBeltConveyorBlockParam;
        }

        private static bool IsSplitter(IBlockParam param)
        {
            var connectors = param is BeltConveyorBlockParam belt ? belt.InventoryConnectors : ((GearBeltConveyorBlockParam)param).InventoryConnectors;
            return OutputConnectorCount(connectors) > 1;
        }

        private static int OutputConnectorCount(InventoryConnects connectors)
        {
            // 未定義の出力は搬出口なしとして0個に数える
            // Undefined outputs mean no emitting port and count as zero
            return connectors.OutputConnects == null ? 0 : connectors.OutputConnects.Length;
        }

        private static Vector3Int EndpointCell(IBlock block, bool isBelt, IBlockConnector connector)
        {
            // ベルトは原点、機械は接続に使われたコネクターのoffsetが指す外周マス
            // Belts use their origin; machines use the boundary cell addressed by the connector used for this connection
            return isBelt ? block.BlockPositionInfo.OriginalPos : block.BlockPositionInfo.ConvertBlockLocalToWorldCell(connector.Offset);
        }

        private static BeltTopologyPartnerKind KindOf(bool isBelt)
        {
            return isBelt ? BeltTopologyPartnerKind.Belt : BeltTopologyPartnerKind.Machine;
        }

        private static int CompareCell(BeltTopologyCell a, BeltTopologyCell b)
        {
            return ComparePosition(a.Position, b.Position);
        }

        private static int CompareConnection(BeltTopologyConnection a, BeltTopologyConnection b)
        {
            var byDirection = ((int)a.Direction).CompareTo((int)b.Direction);
            return byDirection != 0 ? byDirection : ComparePosition(a.PartnerCell, b.PartnerCell);
        }

        private static int ComparePosition(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }
    }
}
