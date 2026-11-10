using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using UnityEngine;

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
            // AppliesTo を経ずに呼ばれ区画が無いときは束縛を足さない
            // Without a section (called past AppliesTo), add no binding
            var current = _model.Current.Value;
            if (!current.IsPresent)
            {
                Debug.LogError($"[MachineSlotRestrictionContributor] Contribute called without a machine section (blockInstanceId={panel.BlockInstanceId}); input left unchanged");
                return input;
            }
            return input.WithMachine(current.Value.Detail.Binding);
        }
    }
}
