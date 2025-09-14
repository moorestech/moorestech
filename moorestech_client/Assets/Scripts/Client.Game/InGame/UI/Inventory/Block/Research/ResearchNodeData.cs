using Mooresmaster.Model.ResearchModule;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchNodeData
    {
        public ResearchNodeMasterElement MasterElement { get; }
        public bool IsCompleted { get; }
        
        public ResearchNodeData(ResearchNodeMasterElement masterElement, bool isCompleted)
        {
            MasterElement = masterElement;
            IsCompleted = isCompleted;
        }
    }
}