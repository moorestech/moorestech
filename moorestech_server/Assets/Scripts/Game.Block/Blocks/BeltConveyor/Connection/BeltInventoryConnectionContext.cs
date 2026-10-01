using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    public sealed class BeltInventoryConnectionContext : DefaultContext<IBlockInventory>
    {
        internal static BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext> Create(
            InventoryConnects ports, BlockPositionInfo position, BeltConveyorSlopeType slope) =>
            new(new BeltInventoryConnectionData(ports, position, slope, true));

        internal static BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext> CreateMachine(InventoryConnects ports, BlockPositionInfo position) =>
            new(new BeltInventoryConnectionData(ports, position, BeltConveyorSlopeType.Straight, false));

        public override List<Vector3Int> InitializeAndGetOverridelSubsrcibePositions(IBlockConnectorComponent<IBlockInventory> component,
            BlockPositionInfo positionInfo, ConnectorContextData data)
        {
            var positions = new HashSet<Vector3Int>();
            if (data is BeltInventoryConnectionData belt)
                foreach (var edge in belt.Edges)
                {
                    // 自分のedgeの4セルを購読し、第三者の配置でも自分だけを再計算する
                    // Subscribe to four cells per own edge so third-party placement independently recalculates this source
                    positions.Add(edge.UpperCell(false));
                    positions.Add(edge.UpperCell(true));
                    positions.Add(edge.UpperCell(false) + Vector3Int.down);
                    positions.Add(edge.UpperCell(true) + Vector3Int.down);
                }
            return new List<Vector3Int>(positions);
        }

        public override Dictionary<IBlockInventory, ConnectedInfo> GetOverride(Dictionary<IBlockInventory, ConnectedInfo> currentTarget, IBlock targetBlock,
            ConnectorContextData data, IConnectorWorldLookup world, IBlock removingBlock,
            Dictionary<IBlockInventory, ConnectedInfo> ordinaryTargets)
        {
            if (data is not BeltInventoryConnectionData belt) return new Dictionary<IBlockInventory, ConnectedInfo>(ordinaryTargets);
            var desired = new Dictionary<IBlockInventory, ConnectedInfo>();
            // 機械同士は通常接続を全て残し、ベルトを含む組だけedgeへ委譲する
            // Preserve all ordinary machine-to-machine connections and delegate pairs involving belts to edges
            if (!belt.IsBelt)
                foreach (var (target, info) in ordinaryTargets)
                    if (!BeltInventoryConnectionData.TryGet(info.TargetBlock, out var targetData) || !targetData.IsBelt)
                        desired.Add(target, info);

            var self = world.GetBlock(data.Position.OriginalPos);
            if (self == null || ReferenceEquals(self, removingBlock)) return desired;
            var connections = new List<BeltEdgeConnection>();
            foreach (var edge in belt.Edges)
                BeltEdgeConnectionResolver.Resolve(world, edge, removingBlock, connections);
            // resolverが返す両方向のうち自分がsourceの結果だけを採用する
            // Keep only results whose source is self from the resolver's two possible directions
            foreach (var connection in connections)
                if (ReferenceEquals(connection.Source, self) && !desired.ContainsKey(connection.Target))
                    desired.Add(connection.Target, connection.Info);
            return desired;
        }
    }
}
