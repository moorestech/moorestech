using System.Collections.Generic;

namespace Game.CraftChainer.CraftNetwork.Node
{
    /// <summary>
    /// クラフト
    /// </summary>
    public class ChainerTransporter : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; }
        public List<ICraftChainerNode> ConnectTargets { get; }
        
        public void InsertItem(CraftChainerItem craftChainerItem)
        {
        }
    }
}