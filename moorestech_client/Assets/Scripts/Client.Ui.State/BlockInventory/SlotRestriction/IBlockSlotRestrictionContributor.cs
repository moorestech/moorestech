using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;

namespace Client.Ui.State.BlockInventory.SlotRestriction
{
    /// <summary>
    /// Shift 配分の宛先の束縛の寄与の口（機械の選択レシピは S4-machine、ロケット・宇宙エレベーターの入れ口は S4-special が足す）
    /// Contribution port for Shift-allocation bindings (S4-machine adds the selected recipe, S4-special the rocket and space-elevator inlets)
    /// </summary>
    public interface IBlockSlotRestrictionContributor
    {
        bool AppliesTo(OpenBlockInventoryPanel panel);
        OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input);
    }
}
