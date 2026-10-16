using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Gear.Common;
using Game.Block.Interface.Extension;
using Game.Gear.Common;
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
            // 全ブロックを1回だけ走査し、ベルトのマスを見つけ次第登録しながら接続を溜める
            // Walk every block once, registering belt cells on first sight while collecting connections
            var cellIndexByBlock = new Dictionary<BlockInstanceId, int>();
            var works = new List<CellWork>();
            var pendings = new List<PendingConnection>();
            foreach (var data in world.BlockMasterDictionary.Values) CollectConnections(data.Block);

            // 溜めた接続をマスごとのぴったりの配列へ移し、座標順に並べる
            // Move the collected connections into exactly sized per-cell arrays, then sort cells by position
            var cells = CreateCells();
            cells.Sort(BeltTopologyOrder.Cell);
            return cells;

            #region Internal

            void CollectConnections(IBlock source)
            {
                var sourceIsBelt = IsBelt(source);
                var sourceIndex = -1;
                if (sourceIsBelt && !TryRegisterBelt(source, out sourceIndex)) return;
                if (!source.ComponentManager.TryGetComponent<IBlockConnectorComponent<IBlockInventory>>(out var connector)) return;
                var targets = connector.ConnectedTargets;
                if (targets.Count == 0) return;
                foreach (var (receiverInventory, info) in targets)
                {
                    // 機械同士の接続と、向きを持てないベルトへの接続は一覧に含めない
                    // Skip machine-to-machine connections and connections to belts that cannot have a forward direction
                    var target = info.TargetBlock;
                    var targetIsBelt = IsBelt(target);
                    if (!sourceIsBelt && !targetIsBelt) continue;
                    var targetIndex = -1;
                    if (targetIsBelt && !TryRegisterBelt(target, out targetIndex)) continue;

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
                        AddPending(sourceIndex, false, new BeltTopologyConnection(outputDirection, entryDirection, KindOf(targetIsBelt), target, targetCell,
                            info.SelfConnector, info.TargetConnector, receiverInventory));
                    if (targetIsBelt)
                        AddPending(targetIndex, true, new BeltTopologyConnection(inputDirection, entryDirection, KindOf(sourceIsBelt), source, sourceCell,
                            info.SelfConnector, info.TargetConnector, receiverInventory));
                }
            }

            bool TryRegisterBelt(IBlock block, out int index)
            {
                if (cellIndexByBlock.TryGetValue(block.BlockInstanceId, out index)) return true;
                var position = block.BlockPositionInfo;
                // 水平姿勢でないベルトは前方向を持てないので一覧に載せない。プレイヤーが置ける通常の状態なのでログは出さない
                // A belt without a horizontal orientation has no forward direction and is left out. Players can place it normally, so no log
                if (position.BlockDirection is not (BlockDirection.North or BlockDirection.East or BlockDirection.South or BlockDirection.West)) return false;
                index = works.Count;
                works.Add(new CellWork(block, BeltTopologyGeometry.HorizontalDirection(position.BlockDirection.ConvertLocalCell(Vector3Int.forward))));
                cellIndexByBlock.Add(block.BlockInstanceId, index);
                return true;
            }

            void AddPending(int cellIndex, bool isInput, BeltTopologyConnection connection)
            {
                var work = works[cellIndex];
                if (isInput) work.InputCount++;
                else work.OutputCount++;
                works[cellIndex] = work;
                pendings.Add(new PendingConnection(cellIndex, isInput, connection));
            }

            List<BeltTopologyCell> CreateCells()
            {
                var inputs = new BeltTopologyConnection[works.Count][];
                var outputs = new BeltTopologyConnection[works.Count][];
                var filled = new int[works.Count * 2];
                for (var i = 0; i < works.Count; i++)
                {
                    inputs[i] = works[i].InputCount == 0 ? Array.Empty<BeltTopologyConnection>() : new BeltTopologyConnection[works[i].InputCount];
                    outputs[i] = works[i].OutputCount == 0 ? Array.Empty<BeltTopologyConnection>() : new BeltTopologyConnection[works[i].OutputCount];
                }
                foreach (var pending in pendings)
                {
                    var slot = pending.CellIndex * 2 + (pending.IsInput ? 0 : 1);
                    var array = pending.IsInput ? inputs[pending.CellIndex] : outputs[pending.CellIndex];
                    array[filled[slot]++] = pending.Connection;
                }

                var result = new List<BeltTopologyCell>(works.Count);
                for (var i = 0; i < works.Count; i++)
                {
                    var block = works[i].Block;
                    var param = block.BlockMasterElement.BlockParam;
                    // マスタは秒単位。整数に換算できることは読み込み時の検証で保証済み
                    // The master holds seconds; load-time validation guarantees a whole per-tick value
                    var speedPerSecond = ((IBeltSpeedParam)param).BeltSpeedPerSecond;
                    var speed = BeltSpeedConversion.ToPerTick(speedPerSecond);
                    Array.Sort(inputs[i], BeltTopologyOrder.Connection);
                    Array.Sort(outputs[i], BeltTopologyOrder.Connection);
                    // 歯車を持つマスは組が回転数から速度を決める。segmentの結合条件にも使う
                    // A cell with a gear gets its speed from the rotation; the chain rule uses it too
                    var isGearDriven = block.ExistsComponent<IGearEnergyTransformer>();
                    result.Add(new BeltTopologyCell(block.BlockPositionInfo.OriginalPos, block.BlockInstanceId, block, works[i].Forward, speed, isGearDriven, IsSplitter(param),
                        inputs[i], outputs[i]));
                }
                return result;
            }

            bool IsBelt(IBlock block)
            {
                return block.BlockMasterElement.BlockParam is IBeltSpeedParam;
            }

            bool IsSplitter(IBlockParam param)
            {
                var connectors = ((IInventoryConnectors)param).InventoryConnectors;
                return 1 < OutputConnectorCount(connectors);
            }

            int OutputConnectorCount(InventoryConnects connectors)
            {
                // 未定義の出力は搬出口なしとして0個に数える
                // Undefined outputs mean no emitting port and count as zero
                return connectors.OutputConnects == null ? 0 : connectors.OutputConnects.Length;
            }

            Vector3Int EndpointCell(IBlock block, bool isBelt, IBlockConnector connector)
            {
                // ベルトは原点、機械は接続に使われたコネクターのoffsetが指す外周マス
                // Belts use their origin; machines use the boundary cell addressed by the connector used for this connection
                return isBelt ? block.BlockPositionInfo.OriginalPos : block.BlockPositionInfo.ConvertBlockLocalToWorldCell(connector.Offset);
            }

            BeltTopologyPartnerKind KindOf(bool isBelt)
            {
                return isBelt ? BeltTopologyPartnerKind.Belt : BeltTopologyPartnerKind.Machine;
            }

            #endregion
        }

        // 構築中のマス1つ分。入出力の件数だけ数え、接続本体は共有の一時リストに置く
        // One cell under construction; it only counts inputs and outputs while the connections live in one shared buffer
        private struct CellWork
        {
            public readonly IBlock Block;
            public readonly BeltDirection Forward;
            public int InputCount;
            public int OutputCount;

            public CellWork(IBlock block, BeltDirection forward)
            {
                Block = block;
                Forward = forward;
                InputCount = 0;
                OutputCount = 0;
            }
        }

        private readonly struct PendingConnection
        {
            public readonly int CellIndex;
            public readonly bool IsInput;
            public readonly BeltTopologyConnection Connection;

            public PendingConnection(int cellIndex, bool isInput, BeltTopologyConnection connection)
            {
                CellIndex = cellIndex;
                IsInput = isInput;
                Connection = connection;
            }
        }
    }
}
