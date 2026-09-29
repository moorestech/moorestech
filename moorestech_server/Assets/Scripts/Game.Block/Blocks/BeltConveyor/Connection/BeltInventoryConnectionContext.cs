using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Component.ConnectionContext;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Component.WorldMutation;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal sealed class BeltInventoryConnectionContext : IConnectorContext<IBlockInventory>
    {
        internal readonly BlockPositionInfo Position;
        internal readonly BeltConveyorSlopeType Slope;
        internal readonly IReadOnlyList<IBlockConnector> Inputs;
        internal readonly IReadOnlyList<IBlockConnector> Outputs;
        internal readonly List<BeltEdge> Edges;
        private readonly IWorldBlockDatastore _world;

        private BeltInventoryConnectionContext(InventoryConnects ports, BlockPositionInfo position, BeltConveyorSlopeType slope)
        {
            Position = position;
            Slope = slope;
            Inputs = ports.InputConnects;
            Outputs = ports.OutputConnects;
            Edges = BeltEdgeEndpoint.GetEdges(position, slope);
            _world = ServerContext.WorldBlockDatastore;
        }

        internal static BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> Create(
            InventoryConnects ports, BlockPositionInfo position, BeltConveyorSlopeType slope)
        {
            // 購読開始前に配置情報を渡し、未登録のselfを検索しない
            // Supply placement before subscriptions start, without looking up the unregistered self
            var context = new BeltInventoryConnectionContext(ports, position, slope);
            return new BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>(ports.InputConnects, ports.OutputConnects, position, context);
        }

        public bool HandlesOverride(IBlock targetBlock) => TryGetContext(targetBlock, out _);
        public IBlockWorldMutation CaptureWorldMutation() => new BeltEdgeConnectionMutation(this, GetOverride());

        internal List<BeltEdgeConnection> GetOverride()
        {
            var connections = new List<BeltEdgeConnection>();
            foreach (var edge in Edges)
                BeltEdgeConnectionResolver.Resolve(_world, edge, connections);
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
