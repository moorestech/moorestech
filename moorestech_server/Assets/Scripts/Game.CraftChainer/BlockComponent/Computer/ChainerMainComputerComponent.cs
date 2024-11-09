using Game.Block.Interface.Component;
using Game.CraftChainer.CraftNetwork;

namespace Game.CraftChainer.BlockComponent.Computer
{
    public class ChainerMainComputerComponent : ICraftChainerNode
    {
        public bool IsDestroy { get; }
        public void Destroy()
        {
            throw new System.NotImplementedException();
        }
        public string SaveKey { get; }
        public string GetSaveState()
        {
            throw new System.NotImplementedException();
        }
        public CraftChainerNodeId NodeId { get; }
    }
}