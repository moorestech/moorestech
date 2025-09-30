using Game.Research;
using Mooresmaster.Model.ResearchModule;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchNodeData
    {
        public ResearchNodeMasterElement MasterElement { get; }
        public ResearchNodeState State { get; }
        
        public ResearchNodeData(ResearchNodeMasterElement masterElement, ResearchNodeState state)
        {
            MasterElement = masterElement;
            State = state;
        }
    }
}