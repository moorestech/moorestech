using System.Collections.Generic;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.InventoryConnectsModule;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    // ベルト形状とportはコンポーネント所有の入力であり、共有Contextには保持しない
    // Belt geometry and ports are component-owned inputs, never state stored in the shared context
    internal sealed class BeltInventoryConnectionData : ConnectorContextData
    {
        internal readonly BeltConveyorSlopeType Slope;
        internal readonly List<BeltEdge> Edges;
        internal readonly bool IsBelt;

        internal BeltInventoryConnectionData(InventoryConnects ports, BlockPositionInfo position, BeltConveyorSlopeType slope, bool isBelt)
            : base(ports.InputConnects, ports.OutputConnects, position)
        {
            Slope = slope;
            IsBelt = isBelt;
            Edges = isBelt ? BeltEdgeEndpoint.GetEdges(position, slope) : MachineInventoryEdgePorts.GetEdges(ports, position);
        }

        internal static bool TryGet(IBlock block, out BeltInventoryConnectionData data)
        {
            data = null;
            if (!block.ComponentManager.TryGetComponent<BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext>>(out var connector)) return false;
            data = connector.Data as BeltInventoryConnectionData;
            return data != null;
        }
    }
}
