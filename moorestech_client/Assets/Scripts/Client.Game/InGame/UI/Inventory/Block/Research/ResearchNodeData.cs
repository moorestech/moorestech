// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
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