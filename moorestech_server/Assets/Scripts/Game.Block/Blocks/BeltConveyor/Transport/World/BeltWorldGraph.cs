using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal sealed class BeltWorldGraph
    {
        internal readonly List<BeltNetworkCell> Cells = new List<BeltNetworkCell>();
        internal readonly List<BeltNetworkConnection> Edges = new List<BeltNetworkConnection>();
        internal readonly Dictionary<int, VanillaBeltConveyorComponent> Components = new Dictionary<int, VanillaBeltConveyorComponent>();
        internal readonly Dictionary<(int, BeltDirection), BeltMachineConnection> Outputs = new Dictionary<(int, BeltDirection), BeltMachineConnection>();
    }
    internal sealed class BeltMachineConnection
    {
        internal readonly BlockInstanceId Source;
        internal readonly IBlockInventory Target;
        internal readonly ConnectedInfo Info;
        internal BeltMachineConnection(BlockInstanceId source, IBlockInventory target, ConnectedInfo info)
        { Source = source; Target = target; Info = info; }
    }
}
