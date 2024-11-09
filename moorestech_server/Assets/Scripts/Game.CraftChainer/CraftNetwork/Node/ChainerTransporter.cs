using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor;

namespace Game.CraftChainer.CraftNetwork.Node
{
    /// <summary>
    /// クラフト
    /// </summary>
    public class ChainerTransporter : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; }
        public List<ICraftChainerNode> ConnectTargets { get; }
        
        public VanillaBeltConveyorComponent VanillaBeltConveyorComponent { get; private set; }
        
        public void InsertItem(CraftChainerItem craftChainerItem)
        {
            
        }
    }
}