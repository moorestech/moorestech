using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 開いた機械の選択レシピの束縛を Shift 配分の入力へ足す寄与
    /// The contribution that adds the open machine's selected-recipe binding to the Shift-allocation input
    /// </summary>
    public sealed class MachineSlotRestrictionContributor : IBlockSlotRestrictionContributor
    {
        private readonly IMachineSectionModel _model;

        public MachineSlotRestrictionContributor(IMachineSectionModel model)
        {
            _model = model;
        }

        // 前に開いた機械の値を別のブロックへ当てないよう、個体 ID の一致を見る
        // Match the instance ID so a previously opened machine's value never applies to another block
        public bool AppliesTo(OpenBlockInventoryPanel panel)
        {
            var current = _model.Current.Value;
            return current.IsPresent && current.Value.IsFor(panel);
        }

        public OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input)
        {
            return input.WithMachine(_model.Current.Value.Value.Detail.Binding);
        }
    }
}
