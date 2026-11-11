using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 発射台・宇宙エレベーターの入れ口が受け付ける物を S4-alpha の Shift の束縛へ寄与する（Web の slotBindings と同じ。詳細の未着は寄与せず取得待ち）
    /// Contributes what the launch pad and space elevator inlets accept to S4-alpha's Shift bindings (same as the Web slotBindings; no contribution while the detail is missing, so it stays pending)
    /// </summary>
    public sealed class SpecialInletBindingContributor : IBlockSlotRestrictionContributor
    {
        private readonly ISpecialBlockDetailModel _model;

        public SpecialInletBindingContributor(ISpecialBlockDetailModel model)
        {
            _model = model;
        }

        public bool AppliesTo(OpenBlockInventoryPanel panel)
        {
            return panel.BlockType == BlockMasterElement.BlockTypeConst.RocketLaunchPad || panel.BlockType == BlockMasterElement.BlockTypeConst.SpaceElevatorBase;
        }

        public OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input)
        {
            // 束縛は詳細の Reader が本文の枠と同じ定義から組む。個体照合と未着の判定も Model の 1 か所に任せる（D3）
            // The detail Reader builds bindings from the same definition as the body slots; instance matching and missing state are left to the Model (D3)
            var detail = _model.Current.Value;
            if (detail.TryGetRocketLaunchPad(out var rocket)) return input.WithInletBindings(rocket.InletBindings);
            if (detail.TryGetSpaceElevator(out var elevator)) return input.WithInletBindings(elevator.InletBindings);
            // silent-ok: 詳細の未着・差し替え途中は寄与しない（L0-s4b が入れ口固定のブロックを取得待ちへ倒す） / alpha の Shift が取得待ちの通知（状態の取得を待っています）を出して気づく
            return input;
        }
    }
}
