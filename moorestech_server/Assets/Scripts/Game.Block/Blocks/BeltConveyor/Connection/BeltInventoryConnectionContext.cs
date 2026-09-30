using System;
using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Component.ConnectionContext;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal sealed class BeltInventoryConnectionContext : IConnectorContext<IBlockInventory>
    {
        internal readonly BlockPositionInfo Position;
        internal readonly BeltConveyorSlopeType Slope;
        internal readonly IReadOnlyList<IBlockConnector> Inputs;
        internal readonly IReadOnlyList<IBlockConnector> Outputs;
        internal readonly List<BeltEdge> Edges;
        internal readonly bool IsBelt;
        private readonly IWorldBlockDatastore _world;
        private readonly List<IDisposable> _subscriptions = new();

        private BeltInventoryConnectionContext(InventoryConnects ports, BlockPositionInfo position, BeltConveyorSlopeType slope, bool isBelt)
        {
            Position = position;
            Slope = slope;
            Inputs = ports.InputConnects;
            Outputs = ports.OutputConnects;
            IsBelt = isBelt;
            Edges = isBelt ? BeltEdgeEndpoint.GetEdges(position, slope) : MachineInventoryEdgePorts.GetEdges(ports, position);
            _world = ServerContext.WorldBlockDatastore;
            SubscribeToEdgeCells(ServerContext.WorldBlockUpdateEvent);
        }

        internal static BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> Create(
            InventoryConnects ports, BlockPositionInfo position, BeltConveyorSlopeType slope)
        {
            // 購読開始前に配置情報を渡し、未登録のselfを検索しない
            // Supply placement before subscriptions start, without looking up the unregistered self
            var context = new BeltInventoryConnectionContext(ports, position, slope, true);
            return new BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>(ports.InputConnects, ports.OutputConnects, position, context);
        }

        internal static BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> CreateMachine(InventoryConnects ports, BlockPositionInfo position)
        {
            // 機械のポート面を水平ベルトとして扱う
            // Treat machine port faces as virtual flat belts
            var context = new BeltInventoryConnectionContext(ports, position, BeltConveyorSlopeType.Straight, false);
            return new BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>(ports.InputConnects, ports.OutputConnects, position, context);
        }

        public bool HandlesOverride(IBlock targetBlock) => TryGetContext(targetBlock, out var target) && (IsBelt || target.IsBelt);
        internal void ApplyOverride(IBlock removingBlock)
        {
            var current = GetCurrentConnections();
            var desired = GetOverride(removingBlock);
            // 第三者の旧接続も外してから張り直す
            // Remove obsolete third-party connections before adding their replacements
            foreach (var connection in current)
                if (!desired.Contains(connection)) connection.Source.RemoveConnection(connection.Target);
            foreach (var connection in desired)
                if (!current.Contains(connection)) connection.Source.SetConnection(connection.Target, connection.Info);
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }

        private void SubscribeToEdgeCells(IWorldBlockUpdateEvent events)
        {
            var cells = new HashSet<Vector3Int>();
            foreach (var edge in Edges)
            {
                cells.Add(edge.UpperCell(false));
                cells.Add(edge.UpperCell(true));
                cells.Add(edge.UpperCell(false) + Vector3Int.down);
                cells.Add(edge.UpperCell(true) + Vector3Int.down);
            }
            // 搬出先以外の上側候補の変化も拾う
            // Observe upper candidates as well as direct output destinations
            foreach (var cell in cells)
            {
                _subscriptions.Add(events.GetBlockPlaceEvent(cell).Subscribe(_ => ApplyOverride(null)));
                // 撤去通知時は対象がまだworldにいる
                // Removal is notified while the block is still present in the world
                _subscriptions.Add(events.GetBlockRemoveEvent(cell).Subscribe(change => ApplyOverride(change.BlockData.Block)));
            }
        }

        private HashSet<BeltEdgeConnection> GetCurrentConnections()
        {
            var connections = new HashSet<BeltEdgeConnection>();
            foreach (var edge in Edges)
            {
                var blocks = new HashSet<IBlock>();
                CollectBlock(edge.UpperCell(false), edge, blocks);
                CollectBlock(edge.UpperCell(true), edge, blocks);
                CollectBlock(edge.UpperCell(false) + Vector3Int.down, edge, blocks);
                CollectBlock(edge.UpperCell(true) + Vector3Int.down, edge, blocks);
                // 共有する4マス内の接続だけを読む
                // Read only connections between blocks sharing these four cells
                foreach (var source in blocks)
                foreach (var target in blocks)
                {
                    if (ReferenceEquals(source, target)) continue;
                    TryGetContext(source, out var sourceContext);
                    TryGetContext(target, out var targetContext);
                    if (!sourceContext.IsBelt && !targetContext.IsBelt) continue;
                    var connector = source.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();
                    var inventory = target.ComponentManager.GetComponent<IBlockInventory>();
                    if (connector.ConnectedTargets.TryGetValue(inventory, out var info))
                        connections.Add(new BeltEdgeConnection(connector, inventory, info));
                }
            }
            return connections;

            #region Internal
            void CollectBlock(Vector3Int cell, BeltEdge edge, HashSet<IBlock> blocks)
            {
                var block = _world.GetBlock(cell);
                if (block != null && TryGetContext(block, out var context) && context.Edges.Contains(edge)) blocks.Add(block);
            }
            #endregion
        }

        private List<BeltEdgeConnection> GetOverride(IBlock removingBlock)
        {
            var connections = new List<BeltEdgeConnection>();
            foreach (var edge in Edges)
                BeltEdgeConnectionResolver.Resolve(_world, edge, removingBlock, connections);
            return connections;
        }

        internal static bool TryGetContext(IBlock block, out BeltInventoryConnectionContext context)
        {
            context = null;
            if (!block.ComponentManager.TryGetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>(out var connector)) return false;
            context = connector.Context as BeltInventoryConnectionContext;
            return context != null;
        }
    }
}
