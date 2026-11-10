using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 選択レシピの束縛を Shift 配分の入力へ足す寄与
    /// Adds the selected recipe binding to Shift allocation
    /// </summary>
    public sealed class MachineSlotRestrictionContributor : IBlockSlotRestrictionContributor
    {
        private readonly IMachineSectionModel _model;

        public MachineSlotRestrictionContributor(IMachineSectionModel model)
        {
            _model = model;
        }

        // 前の機械の値を当てないよう個体 ID を見る
        // Match instance ID so old values never apply
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
