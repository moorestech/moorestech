using System.Collections.Generic;
using UnitGenerator;

namespace Game.CraftChainer.CraftNetwork
{
    public interface ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; }
        
        public List<ICraftChainerNode> ConnectTargets { get; }
        
        public void InsertItem(CraftChainerItem craftChainerItem);
    }
    
    [UnitOf(typeof(int))]
    public partial struct CraftChainerNodeId { }
    
    public enum CraftChainerNodeType
    {
        CenterConsole,
        Transporter,
        
        CraftFactory,
        
        Storage,
    }
}