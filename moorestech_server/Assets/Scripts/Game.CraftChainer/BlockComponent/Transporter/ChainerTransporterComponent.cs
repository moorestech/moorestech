using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;

namespace Game.CraftChainer.BlockComponent
{
    public class ChainerTransporterComponent : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}